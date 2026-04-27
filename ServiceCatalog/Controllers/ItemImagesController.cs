using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using ClosedXML.Excel;
using ServiceCatalog.Data;
using System.IO.Compression;
using ServiceCatalog.Models;
using ServiceCatalog.Services;

namespace ServiceCatalog.Controllers
{
    // Controllers/ItemImagesController.cs
    public class ItemImagesController : Controller
    {
        public ActionResult Index()
        {
            List<SelectListItem> listProductName = new List<SelectListItem>();

            using (SqlConnection connection = new SqlConnection(
                ConfigurationManager.ConnectionStrings["ServiceCatalogDB"].ConnectionString))
            {
                connection.Open();
                var command = new SqlCommand("P_Search_Product_Name", connection);
                command.CommandType = CommandType.StoredProcedure;

                // ✅ แก้ parameter ให้ตรงกับ SP จริง
                command.Parameters.AddWithValue("@inUserId", User.Identity.Name);
                command.Parameters.AddWithValue("@inFlag", string.Empty);

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        listProductName.Add(new SelectListItem
                        {
                            Value = reader["PROD"].ToString(),
                            Text = $"{reader["PROD"]}/{reader["PRODNAM"]}"
                        });
                    }
                }
            }

            ViewBag.listProductName = listProductName;
            return View("Index");
        }

        // ✅ เปลี่ยนมาเรียก P_Search_Product_Image แทน GetProductImageList()
        [HttpPost]
        public ActionResult GetListProductImage(
            string Stkcode, string Company,
            string ProdCode, string SecCode, string StockGroup,
            string BrandId, string RowNumber,
            string ApiStatus, string CallDate)
        {
            var list = new List<ProductImageViewModel>();

            using (SqlConnection connection = new SqlConnection(
                ConfigurationManager.ConnectionStrings["ServiceCatalogDB"].ConnectionString))
            {
                connection.Open();

                var command = new SqlCommand("P_Search_Product_Image", connection);
                command.CommandType = CommandType.StoredProcedure;
                command.CommandTimeout = 120; // ✅ เพิ่มเป็น 120 วินาที (default คือ 30)
                command.Parameters.AddWithValue("@inProdCode", ProdCode ?? string.Empty);
                command.Parameters.AddWithValue("@inCompany", Company ?? string.Empty);
                command.Parameters.AddWithValue("@inStkcode", Stkcode ?? string.Empty);

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        list.Add(new ProductImageViewModel
                        {
                            Company = reader["Company"].ToString(),
                            Stkcode = reader["Stkcode"].ToString(),
                            Stkdesc = reader["Stkdesc"].ToString(),
                            BrandId = reader["BrandId"].ToString(),
                            BrandName = reader["BrandName"].ToString(),
                            Category = reader["Category"].ToString(),
                            ProductCode = reader["ProductCode"].ToString(),
                            ProductName = reader["ProductName"].ToString(),
                            SeqImage = Convert.IsDBNull(reader["SeqImage"]) ? 0 : Convert.ToInt32(reader["SeqImage"]),
                            Filename = reader["Filename"].ToString(),
                            Url = reader["Url"].ToString(),

                            // ── Safe read ไม่ crash แม้ SP ไม่ได้ return column เหล่านี้ ──
                            Status = HasColumn(reader, "Status")
                                            ? reader["Status"].ToString() : "PENDING",
                            FilePath = HasColumn(reader, "FilePath")
                                            ? reader["FilePath"].ToString() : null,
                            RetryCount = HasColumn(reader, "RetryCount")
                                            ? Convert.ToInt32(reader["RetryCount"]) : 0
                        });
                    }
                }
            }

            ViewBag.ListProductImage = list;
            return PartialView("_ListProductImage");
        }

        // ── SyncOne: โหลดรูปเดียว ลง ~/Uploads/{Stkcode}/ ──────────────
        [HttpPost]
        public async Task<ActionResult> SyncOne(string stkcode, int seqImage)
        {
            if (string.IsNullOrWhiteSpace(stkcode))
                return Json(new { success = false, message = "stkcode is required." });

            var result = await GetSyncService().SyncOneAsync(stkcode, seqImage);
            return Json(new { success = result.Success, message = result.Message });
        }

        // ── SyncAll: โหลดทุกรูปใน List ที่ส่งมาจาก JS ─────────────────
        [HttpPost]
        public async Task<ActionResult> SyncAll(List<SyncKey> items)
        {
            if (items == null || items.Count == 0)
                return Json(new { success = false, message = "ไม่มีรายการที่จะ Sync" });

            var service = GetSyncService();
            var jobs = await service.GetJobsByKeysAsync(items);

            if (jobs.Count == 0)
                return Json(new { success = false, message = "ไม่พบข้อมูลใน Database" });

            var result = await service.SyncAllAsync(jobs);

            return Json(new
            {
                success = result.FailCount == 0,
                successCount = result.SuccessCount,
                failCount = result.FailCount,
                message = string.Format("สำเร็จ {0} รายการ, ล้มเหลว {1} รายการ",
                               result.SuccessCount, result.FailCount)
            });
        }

        // ── DownloadFromServer ──────────────────────────────────────────
        [HttpGet]
        public async Task<ActionResult> DownloadFromServer(string stkcode, int seqImage)
        {
            string filePath = await GetFilePathFromDbAsync(stkcode, seqImage);

            if (string.IsNullOrWhiteSpace(filePath))
                return HttpNotFound("ยังไม่มีไฟล์บน Server กรุณา Sync ก่อน");

            string absolutePath = Server.MapPath(filePath);
            if (!System.IO.File.Exists(absolutePath))
                return HttpNotFound("ไฟล์ถูกลบออกจาก Server กรุณา Sync ใหม่");

            string filename = System.IO.Path.GetFileName(absolutePath);
            string mimeType = MimeMapping.GetMimeMapping(filename);
            return File(absolutePath, mimeType, filename);
        }

        // ── DownloadAllZip ──────────────────────────────────────────────
        [HttpPost]
        public ActionResult DownloadAllZip(List<string> stkcodes)
        {
            if (stkcodes == null || stkcodes.Count == 0)
                return Json(new { success = false, message = "ไม่มีรายการ Stkcode" });

            string nasPath = ConfigurationManager.AppSettings["NasPath"];
            string nasUser = ConfigurationManager.AppSettings["NasUser"];
            string nasPassword = ConfigurationManager.AppSettings["NasPassword"];
            string nasDomain = ConfigurationManager.AppSettings["NasDomain"] ?? ".";

            var files = new List<KeyValuePair<string, string>>();
            var sb = new System.Text.StringBuilder();

            try
            {
                using (new NasConnection(nasPath, nasUser, nasPassword, nasDomain))
                {
                    sb.AppendLine("NAS Connected OK");

                    // ── ดึง FilePath จาก DB ──
                    using (var conn = new SqlConnection(
                        ConfigurationManager.ConnectionStrings["ServiceCatalogDB"]
                            .ConnectionString))
                    {
                        conn.Open();

                        foreach (var stkcode in stkcodes)
                        {
                            using (var cmd = new SqlCommand(@"
                        SELECT Filename, FilePath
                        FROM   Product_Image
                        WHERE  Status   = 'SUCCESS'
                          AND  FilePath IS NOT NULL
                          AND  Stkcode  = @Stkcode", conn))
                            {
                                cmd.Parameters.AddWithValue("@Stkcode", stkcode);

                                using (var reader = cmd.ExecuteReader())
                                {
                                    while (reader.Read())
                                    {
                                        string filePath = reader["FilePath"].ToString();
                                        string fileName = reader["Filename"].ToString();

                                        sb.AppendLine("Check: " + filePath);

                                        if (System.IO.File.Exists(filePath))
                                        {
                                            string entryName = stkcode + "/" + fileName;
                                            files.Add(new KeyValuePair<string, string>(
                                                filePath, entryName));
                                            sb.AppendLine("  → OK");
                                        }
                                        else
                                        {
                                            sb.AppendLine("  → NOT FOUND");
                                        }
                                    }
                                }
                            }
                        }
                    }

                    if (files.Count == 0)
                        return Json(new
                        {
                            success = false,
                            message = "ไม่พบไฟล์รูปภาพบน NAS\n\nLog:\n" + sb.ToString()
                        });

                    // ── สร้าง Zip ──
                    var ms = new MemoryStream();
                    using (var zip = new System.IO.Compression.ZipArchive(
                               ms, System.IO.Compression.ZipArchiveMode.Create,
                               leaveOpen: true))
                    {
                        foreach (var file in files)
                        {
                            var entry = zip.CreateEntry(
                                file.Value,
                                System.IO.Compression.CompressionLevel.Fastest);

                            using (var entryStream = entry.Open())
                            using (var fileStream = System.IO.File.OpenRead(file.Key))
                            {
                                fileStream.CopyTo(entryStream);
                            }
                        }
                    }

                    ms.Position = 0;
                    byte[] zipBytes = ms.ToArray();
                    ms.Dispose();

                    string zipName = string.Format(
                        "Images_{0:yyyyMMdd_HHmmss}.zip", DateTime.Now);

                    return base.File(zipBytes, "application/zip", zipName);
                }
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message = "Error: " + ex.Message +
                              (ex.InnerException != null
                                  ? " | " + ex.InnerException.Message : "") +
                              "\n\nLog:\n" + sb.ToString()
                });
            }
        }

        // ── Helpers ─────────────────────────────────────────────────────
        private ImageSyncService GetSyncService()
        {
            return new ImageSyncService(
                ConfigurationManager.ConnectionStrings["ServiceCatalogDB"].ConnectionString);
        }

        private async Task<string> GetFilePathFromDbAsync(string stkcode, int seqImage)
        {
            using (var conn = new SqlConnection(
                ConfigurationManager.ConnectionStrings["ServiceCatalogDB"].ConnectionString))
            {
                await conn.OpenAsync();
                using (var cmd = new SqlCommand(@"
            SELECT FilePath FROM Product_Image
            WHERE  Stkcode  = @Stkcode
              AND  SeqImage = @SeqImage", conn))
                {
                    cmd.Parameters.AddWithValue("@Stkcode", stkcode);
                    cmd.Parameters.AddWithValue("@SeqImage", seqImage);
                    var result = await cmd.ExecuteScalarAsync();
                    return (result == null || result == DBNull.Value)
                           ? null : result.ToString();
                }
            }
        }

        private static bool HasColumn(System.Data.IDataReader reader, string columnName)
        {
            for (int i = 0; i < reader.FieldCount; i++)
                if (reader.GetName(i).Equals(columnName,
                    StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        [HttpGet]
        public ActionResult TestNas()
        {
            var sb = new System.Text.StringBuilder();
            string nasPath = ConfigurationManager.AppSettings["NasPath"];
            string nasUser = ConfigurationManager.AppSettings["NasUser"];
            string nasPassword = ConfigurationManager.AppSettings["NasPassword"];
            string nasDomain = ConfigurationManager.AppSettings["NasDomain"] ?? ".";

            sb.AppendLine("NasPath:   " + nasPath);
            sb.AppendLine("NasUser:   " + nasUser);
            sb.AppendLine("NasDomain: " + nasDomain);
            sb.AppendLine("---");

            try
            {
                using (new NasConnection(nasPath, nasUser, nasPassword, nasDomain))
                {
                    sb.AppendLine("✅ Impersonation OK");
                    sb.AppendLine("Current user: " +
                        System.Security.Principal.WindowsIdentity.GetCurrent().Name);

                    if (Directory.Exists(nasPath))
                        sb.AppendLine("✅ NAS Path exists");
                    else
                        sb.AppendLine("❌ NAS Path NOT found");

                    // Write test
                    string testFile = Path.Combine(nasPath, "_test_.txt");
                    try
                    {
                        System.IO.File.WriteAllText(testFile, "ok");
                        System.IO.File.Delete(testFile);
                        sb.AppendLine("✅ Write permission OK");
                    }
                    catch (Exception ex)
                    {
                        sb.AppendLine("❌ Write FAILED: " + ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine("❌ FAILED: " + ex.Message);
                if (ex.InnerException != null)
                    sb.AppendLine("   Inner: " + ex.InnerException.Message);
            }

            return Content(sb.ToString(), "text/plain; charset=utf-8");
        }
    }
}