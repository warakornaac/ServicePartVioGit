using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Data;
using System.IO;
using ServiceCatalog.Models;
using ServiceCatalog.Data;
using ClosedXML.Excel;

namespace ServiceCatalog.Controllers
{
    public class ItemProductController : Controller
    {
        // GET: ItemProduct
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
            return View("IndexItemProduct", new
            {
                @ViewBag.listProductName

            });
        }
        public ActionResult GetListItemProduct(string Company, string SecCode, string StockGroup, string ProdCode, string Stkcode, string BrandId, string RowNumber, string ApiStatus, string Calldate)
        {
            var SearchItemProduct = new List<StoredSearchItemProductsModel>();

            SearchItemProduct = new SearchItemProduct().SearchItem(Company, SecCode, StockGroup, ProdCode, Stkcode, BrandId, RowNumber, ApiStatus, Calldate);
            @ViewBag.listSearchItemProduct = SearchItemProduct;
            return PartialView("_ListItemProduct", new
            {
                @ViewBag.listSearchItemProduct
            });
        }
        public ActionResult getItemProductDeatil(string Stkcode)
        {
            DateTime? startDate = null;
            DateTime? endDate = null;

            var ListProductDescription = new List<ProductDescriptionModel>();
            var ListProductSpec = new List<ProductSpecModel>();

            string message = string.Empty;
            try
            {

                ListProductDescription = new GetProductDescriptionList().Get(Stkcode);
                ListProductSpec = new GetProductSpecList().Get(Stkcode);
               
                ViewBag.ListProductDescription = ListProductDescription;
                ViewBag.ListProductSpec = ListProductSpec;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                ViewBag.ListProductDescription = new List<object>();
                ViewBag.ListProductSpec = new List<object>();

            }
            ViewBag.Message = message;
            return PartialView("_DeatilItemProduct", new
            {
                @ViewBag.Message,
                @ViewBag.ListProductDescription,
                @ViewBag.ListProductSpec
            });
        }
        //Get stored
        private DataTable GetDataFromStore(string storeName, string inSheet, string prodCode, string sec, string stockGroup, string company, string stkcode, string status, string dateCall)
        {
            var dt = new DataTable();

            using (var conn = new SqlConnection(ConfigurationManager.ConnectionStrings["ServiceCatalogDB"].ConnectionString))
            using (var cmd = new SqlCommand(storeName, conn))
            {
                cmd.CommandType = CommandType.StoredProcedure;

                //  parameter @inSheet
                cmd.Parameters.AddWithValue("@inSheet", inSheet ?? string.Empty);
                cmd.Parameters.AddWithValue("@inProdCode", prodCode ?? string.Empty);
                cmd.Parameters.AddWithValue("@inSec", sec ?? string.Empty);
                cmd.Parameters.AddWithValue("@inStockGroup", stockGroup ?? string.Empty);
                cmd.Parameters.AddWithValue("@inCompany", company ?? string.Empty);
                cmd.Parameters.AddWithValue("@inStkcode", stkcode ?? string.Empty);
                cmd.Parameters.AddWithValue("@inStatus", status ?? string.Empty);
                cmd.Parameters.AddWithValue("@inDateCall", dateCall ?? string.Empty);

                using (var adapter = new SqlDataAdapter(cmd))
                {
                    adapter.Fill(dt);
                }
            }

            return dt;
        }
        public ActionResult ExportProductItem(string prodCode, string sec, string stockGroup, string company, string stkcode, string status, string dateCall)
        {
            string storedName = "P_Export_Product_Api";
            var listProductApi = GetDataFromStore(storedName, "Product_Api", prodCode, sec, stockGroup, company, stkcode, status, dateCall);
            var listProductDescription = GetDataFromStore(storedName, "Product_Description", prodCode, sec, stockGroup, company, stkcode, status, dateCall);
            var listProductSpec = GetDataFromStore(storedName, "Product_Spec", prodCode, sec, stockGroup, company, stkcode, status, dateCall);
            var listProductCompetitor = GetDataFromStore(storedName, "Product_Competitor", prodCode, sec, stockGroup, company, stkcode, status, dateCall);
            var listProductOem = GetDataFromStore(storedName, "Product_Oem", prodCode, sec, stockGroup, company, stkcode, status, dateCall);
            var listProductImage = GetDataFromStore(storedName, "Product_Image", prodCode, sec, stockGroup, company, stkcode, status, dateCall);
            var listProductLinkage = GetDataFromStore(storedName, "Product_Linkage", prodCode, sec, stockGroup, company, stkcode, status, dateCall);

            using (var workbook = new XLWorkbook())
            {
                var worksheet1 = workbook.Worksheets.Add("Product_Api");
                var worksheet2 = workbook.Worksheets.Add("Product_Description");
                var worksheet3 = workbook.Worksheets.Add("Product_Spec");
                var worksheet4 = workbook.Worksheets.Add("Product_Competitor");
                var worksheet5 = workbook.Worksheets.Add("Product_Oem");
                var worksheet6 = workbook.Worksheets.Add("Product_Image");
                var worksheet7 = workbook.Worksheets.Add("Product_Linkage");
                // ========= Sheet 1: Product_Api =========
                for (int i = 0; i < listProductApi.Columns.Count; i++)
                {
                    worksheet1.Cell(1, i + 1).Value = listProductApi.Columns[i].ColumnName;
                }
                for (int i = 0; i < listProductApi.Rows.Count; i++)
                {
                    for (int j = 0; j < listProductApi.Columns.Count; j++)
                    {
                        worksheet1.Cell(i + 2, j + 1).SetValue(listProductApi.Rows[i][j].ToString());
                    }
                }
                // ========= Sheet 2: Product_Description =========
                for (int i = 0; i < listProductDescription.Columns.Count; i++)
                {
                    worksheet2.Cell(1, i + 1).Value = listProductDescription.Columns[i].ColumnName;
                }
                for (int i = 0; i < listProductDescription.Rows.Count; i++)
                {
                    for (int j = 0; j < listProductDescription.Columns.Count; j++)
                    {
                        worksheet2.Cell(i + 2, j + 1).SetValue(listProductDescription.Rows[i][j].ToString());
                    }
                }
                // ========= Sheet 3: Product_Spec =========
                for (int i = 0; i < listProductSpec.Columns.Count; i++)
                {
                    worksheet3.Cell(1, i + 1).Value = listProductSpec.Columns[i].ColumnName;
                }
                for (int i = 0; i < listProductSpec.Rows.Count; i++)
                {
                    for (int j = 0; j < listProductSpec.Columns.Count; j++)
                    {
                        worksheet3.Cell(i + 2, j + 1).SetValue(listProductSpec.Rows[i][j].ToString());
                    }
                }
                // ========= Sheet 4: Product_Competitor =========
                for (int i = 0; i < listProductCompetitor.Columns.Count; i++)
                {
                    worksheet4.Cell(1, i + 1).Value = listProductCompetitor.Columns[i].ColumnName;
                }
                for (int i = 0; i < listProductCompetitor.Rows.Count; i++)
                {
                    for (int j = 0; j < listProductCompetitor.Columns.Count; j++)
                    {
                        worksheet4.Cell(i + 2, j + 1).SetValue(listProductCompetitor.Rows[i][j].ToString());
                    }
                }      
                // ========= Sheet 5: Product_Oem =========
                for (int i = 0; i < listProductOem.Columns.Count; i++)
                {
                    worksheet5.Cell(1, i + 1).Value = listProductOem.Columns[i].ColumnName;
                }
                for (int i = 0; i < listProductOem.Rows.Count; i++)
                {
                    for (int j = 0; j < listProductOem.Columns.Count; j++)
                    {
                        worksheet5.Cell(i + 2, j + 1).SetValue(listProductOem.Rows[i][j].ToString());
                    }
                }
                // ========= Sheet 6: Product_Image =========
                for (int i = 0; i < listProductImage.Columns.Count; i++)
                {
                    worksheet6.Cell(1, i + 1).Value = listProductImage.Columns[i].ColumnName;
                }
                for (int i = 0; i < listProductImage.Rows.Count; i++)
                {
                    for (int j = 0; j < listProductImage.Columns.Count; j++)
                    {
                        worksheet6.Cell(i + 2, j + 1).SetValue(listProductImage.Rows[i][j].ToString());
                    }
                }
                // ========= Sheet 7: Product_Image =========
                for (int i = 0; i < listProductLinkage.Columns.Count; i++)
                {
                    worksheet7.Cell(1, i + 1).Value = listProductLinkage.Columns[i].ColumnName;
                }
                for (int i = 0; i < listProductLinkage.Rows.Count; i++)
                {
                    for (int j = 0; j < listProductLinkage.Columns.Count; j++)
                    {
                        worksheet7.Cell(i + 2, j + 1).SetValue(listProductLinkage.Rows[i][j].ToString());
                    }
                }

                // Save to stream and return file
                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);
                    stream.Position = 0;

                    return File(stream.ToArray(),
                        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                        "ProductApiExport_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".xlsx");
                }
            }
        }
    }
}