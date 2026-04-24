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
                return HttpNotFound("ไม่มีรายการ");

            var files = new List<KeyValuePair<string, string>>();
            // KeyValuePair<absolutePath, entryName>
            // entryName = "Stkcode/filename.jpg" เพื่อให้ zip มี folder structure

            foreach (var stkcode in stkcodes)
            {
                // folder จริงบน server ~/Uploads/{Stkcode}/
                string stkFolder = Server.MapPath(
                    string.Format("~/Uploads/{0}/", stkcode));

                if (!System.IO.Directory.Exists(stkFolder))
                    continue;

                // หยิบทุกไฟล์รูปใน folder นั้น
                var imageFiles = System.IO.Directory.GetFiles(stkFolder);
                foreach (var filePath in imageFiles)
                {
                    string fileName = System.IO.Path.GetFileName(filePath);
                    // entryName มี folder ด้วย → Stkcode/filename.jpg
                    string entryName = stkcode + "/" + fileName;
                    files.Add(new KeyValuePair<string, string>(filePath, entryName));
                }
            }

            if (files.Count == 0)
                return HttpNotFound("ไม่พบไฟล์รูปภาพ กรุณา Sync ก่อน Download");

            using (var ms = new System.IO.MemoryStream())
            {
                using (var zip = new System.IO.Compression.ZipArchive(
                           ms, System.IO.Compression.ZipArchiveMode.Create, leaveOpen: true))
                {
                    foreach (var file in files)
                    {
                        var entry = zip.CreateEntry(file.Value,
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
                string zipName = string.Format("Images_{0:yyyyMMdd_HHmmss}.zip",
                                  DateTime.Now);
                return File(zipBytes, "application/zip", zipName);
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

    }
}