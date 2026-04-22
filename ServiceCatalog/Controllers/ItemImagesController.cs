using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
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

namespace ServiceCatalog.Controllers
{
    // Controllers/ItemImagesController.cs
    public class ItemImagesController : Controller
    {
        // GET: ItemImages
        //public ActionResult Index()
        //{
        //    List<SelectListItem> listProductName = new List<SelectListItem>();

        //    using (SqlConnection connection = new SqlConnection(ConfigurationManager.ConnectionStrings["ServiceCatalogDB"].ConnectionString))
        //    {
        //        connection.Open();
        //        var command = new SqlCommand("P_Search_Product_Name", connection);
        //        command.CommandType = CommandType.StoredProcedure;
        //        command.Parameters.AddWithValue("@inStkcode", string.Empty);
        //        command.Parameters.AddWithValue("@inCompany", string.Empty);
        //        command.Parameters.AddWithValue("@inProdCode", string.Empty);
        //        var reader = command.ExecuteReader();
        //        while (reader.Read())
        //        {
        //            listProductName.Add(new SelectListItem
        //            {
        //                Value = reader["PROD"].ToString(),
        //                Text = $"{reader["PROD"]}/{reader["PRODNAM"]}"
        //            });
        //        }
        //    }
        //    @ViewBag.listProductName = listProductName;
        //    return View("Index", new
        //    {
        //        @ViewBag.listProductName
        //    });
        //}
        //// ดึงลิสท์ภาพ → คืน Partial View
        //public ActionResult GetListProductImage(
        //    string stkcode, string company,
        //    string prodCode, string sec, string stockGroup)
        //{
        //    var list = new GetProductImageList().Get(stkcode);
        //    ViewBag.ListProductImage = list;
        //    return PartialView("_ListProductImage");
        //}

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
                //var command = new SqlCommand("P_Search_Product_Image", connection);
                //command.CommandType = CommandType.StoredProcedure;
                //command.Parameters.AddWithValue("@inProdCode", ProdCode ?? string.Empty);
                //command.Parameters.AddWithValue("@inCompany", Company ?? string.Empty);
                //command.Parameters.AddWithValue("@inStkcode", Stkcode ?? string.Empty);

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
                            SeqImage = Convert.IsDBNull(reader["SeqImage"])
                                          ? 0 : Convert.ToInt32(reader["SeqImage"]),
                            Filename = reader["Filename"].ToString(),
                            Url = reader["Url"].ToString()
                        });
                    }
                }
            }

            ViewBag.ListProductImage = list;
            return PartialView("_ListProductImage");
        }

        // ดาวน์โหลดรูปจาก URL (รองรับอนาคต)
        //public async Task<ActionResult> DownloadImage(string url, string filename)
        //{
        //    if (string.IsNullOrEmpty(url))
        //        return new HttpStatusCodeResult(400, "URL is required");

        //    try
        //    {
        //        using (var httpClient = new HttpClient())
        //        {
        //            var bytes = await httpClient.GetByteArrayAsync(url);

        //            // ตรวจ extension จาก filename หรือ url
        //            var ext = Path.GetExtension(filename)?.ToLower() ?? ".jpg";
        //            var contentType = GetImageContentType(ext);

        //            return File(bytes, contentType, filename);
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        return new HttpStatusCodeResult(500, "Download failed: " + ex.Message);
        //    }
        //}

        // ดาวน์โหลดหลายรูปพร้อมกันเป็น ZIP (รองรับอนาคต)
        public async Task<ActionResult> DownloadImagesAsZip(string stkcode, string company)
        {
            var images = new GetProductImageList().Get(Stkcode: stkcode);

            if (!images.Any())
                return new HttpStatusCodeResult(404, "No images found");

            using (var httpClient = new HttpClient())
            using (var memStream = new MemoryStream())
            {
                using (var archive = new ZipArchive(memStream, ZipArchiveMode.Create, true))
                {
                    foreach (var img in images)
                    {
                        try
                        {
                            var bytes = await httpClient.GetByteArrayAsync(img.Url);
                            var entry = archive.CreateEntry(img.Filename, CompressionLevel.Fastest);
                            using (var entryStream = entry.Open())
                                await entryStream.WriteAsync(bytes, 0, bytes.Length);
                        }
                        catch { /* ข้ามรูปที่โหลดไม่ได้ */ }
                    }
                }

                memStream.Position = 0;
                return File(
                    memStream.ToArray(),
                    "application/zip",
                    $"Images_{stkcode}_{DateTime.Now:yyyyMMdd_HHmmss}.zip"
                );
            }
        }

        private string GetImageContentType(string ext)
        {
            switch (ext)
            {
                case ".jpg":
                case ".jpeg":
                    return "image/jpeg";
                case ".png":
                    return "image/png";
                case ".gif":
                    return "image/gif";
                case ".webp":
                    return "image/webp";
                default:
                    return "application/octet-stream";
            }
        }

        public async Task<ActionResult> DownloadImage(string url, string filename)
        {
            if (string.IsNullOrEmpty(url))
                return new HttpStatusCodeResult(400, "URL is required");

            try
            {
                // ✅ ถ้าเป็น Local Path บน Server
                if (!url.StartsWith("http"))
                {
                    var localPath = Server.MapPath(url);
                    if (!System.IO.File.Exists(localPath))
                        return new HttpStatusCodeResult(404, "File not found: " + localPath);

                    var ext = Path.GetExtension(filename)?.ToLower() ?? ".jpg";
                    return File(localPath, GetImageContentType(ext), filename);
                }

                // ✅ ถ้าเป็น External URL
                using (var httpClient = new HttpClient())
                {
                    httpClient.Timeout = TimeSpan.FromSeconds(30);

                    // ดู error จริงๆ
                    var response = await httpClient.GetAsync(url);
                    if (!response.IsSuccessStatusCode)
                        return new HttpStatusCodeResult(
                            (int)response.StatusCode,
                            $"Remote server returned: {response.StatusCode} for URL: {url}"
                        );

                    var bytes = await response.Content.ReadAsByteArrayAsync();
                    var ext = Path.GetExtension(filename)?.ToLower() ?? ".jpg";
                    return File(bytes, GetImageContentType(ext), filename);
                }
            }
            catch (TaskCanceledException)
            {
                return new HttpStatusCodeResult(408, $"Request timeout for URL: {url}");
            }
            catch (HttpRequestException ex)
            {
                return new HttpStatusCodeResult(500, $"Network error: {ex.Message} | URL: {url}");
            }
            catch (Exception ex)
            {
                return new HttpStatusCodeResult(500, $"Download failed: {ex.Message} | URL: {url}");
            }
        }
        public async Task<ActionResult> TestConnection()
        {
            try
            {
                using (var client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(10);
                    var response = await client.GetAsync(
                        "https://digital-assets.tecalliance.services/images/800/8c903b3fb8d9daf3b60735844fbfff044b6b273d.jpg"
                    );
                    return Content($"Status: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                return Content($"Error: {ex.Message}");
            }
        }
        public ActionResult ProxyImage(string url, string filename)
        {
            if (string.IsNullOrEmpty(url))
                return new HttpStatusCodeResult(400, "URL is required");

            try
            {
                using (var webClient = new System.Net.WebClient())
                {
                    webClient.Headers.Add("User-Agent",
                        "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
                    webClient.Headers.Add("Referer",
                        "https://digital-assets.tecalliance.services/");

                    var bytes = webClient.DownloadData(url);
                    var ext = Path.GetExtension(filename)?.ToLower() ?? ".jpg";

                    return File(bytes, GetImageContentType(ext), filename);
                }
            }
            catch (Exception ex)
            {
                return Content("Proxy Error: " + ex.Message);
            }
        }
    }
}