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
using ServiceCatalog.Models;
using System.IO.Compression;

namespace ServiceCatalog.Controllers
{
    // Controllers/ItemImagesController.cs
    public class ItemImagesController : Controller
    {
        // GET: ItemImages
        public ActionResult Index()
        {
            List<SelectListItem> listProductName = new List<SelectListItem>();

            using (SqlConnection connection = new SqlConnection(ConfigurationManager.ConnectionStrings["ServiceCatalogDB"].ConnectionString))
            {
                connection.Open();
                var command = new SqlCommand("P_Search_Product_Name", connection);
                command.CommandType = CommandType.StoredProcedure;
                command.Parameters.AddWithValue("@inUserId", "warakorn.pra");
                var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    listProductName.Add(new SelectListItem
                    {
                        Value = reader["PROD"].ToString(),
                        Text = $"{reader["PROD"]}/{reader["PRODNAM"]}"
                    });
                }
            }
            @ViewBag.listProductName = listProductName;
            return View("Index", new
            {
                @ViewBag.listProductName
            });
        }
        // ดึงลิสท์ภาพ → คืน Partial View
        public ActionResult GetListProductImage(
            string stkcode, string company,
            string prodCode, string sec, string stockGroup)
        {
            var list = new GetProductImageList().Get(stkcode);
            ViewBag.ListProductImage = list;
            return PartialView("_ListProductImage");
        }

        // ดาวน์โหลดรูปจาก URL (รองรับอนาคต)
        public async Task<ActionResult> DownloadImage(string url, string filename)
        {
            if (string.IsNullOrEmpty(url))
                return new HttpStatusCodeResult(400, "URL is required");

            try
            {
                using (var httpClient = new HttpClient())
                {
                    var bytes = await httpClient.GetByteArrayAsync(url);

                    // ตรวจ extension จาก filename หรือ url
                    var ext = Path.GetExtension(filename)?.ToLower() ?? ".jpg";
                    var contentType = GetImageContentType(ext);

                    return File(bytes, contentType, filename);
                }
            }
            catch (Exception ex)
            {
                return new HttpStatusCodeResult(500, "Download failed: " + ex.Message);
            }
        }

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
    }
}