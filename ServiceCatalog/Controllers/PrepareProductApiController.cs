using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using System.Web.Mvc;
using ServiceCatalog.Models;

namespace ServiceCatalog.Controllers
{
    public class PrepareProductApiController : Controller
    {
        public async Task<ActionResult> Index()
        {
            var result = new List<PrepareProductApiModel>();

            try
            {
                using (var conn = new SqlConnection(ConfigurationManager.AppSettings["ServiceCatalogDB"]))
                using (var cmd = new SqlCommand("P_Get_ProductApi_List", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;

                    await conn.OpenAsync();
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            result.Add(new PrepareProductApiModel
                            {
                                Stkcode = reader["Stkcode"] as string,
                                BrandId = reader["BrandId"] == DBNull.Value ? 0 : Convert.ToInt32(reader["BrandId"]),
                                BrandName = reader["BrandName"] as string,
                                ProductCompetitorCount = reader["ProductCompetitorCount"]?.ToString(),
                                ProductCompetitorDate = reader["ProductCompetitorDate"] == DBNull.Value ? DateTime.MinValue : Convert.ToDateTime(reader["ProductCompetitorDate"]),
                                ProductLinkageCount = reader["ProductLinkageCount"]?.ToString(),
                                ProductLinkageDate = reader["ProductLinkageDate"] == DBNull.Value ? DateTime.MinValue : Convert.ToDateTime(reader["ProductLinkageDate"]),
                                ApiStatus = reader["ApiStatus"] as string,
                                ApiStatusRemark = reader["ApiStatusRemark"] as string,
                                InsertedBy = reader["InsertedBy"] as string,
                                InsertedDate = reader["InsertedDate"] == DBNull.Value ? DateTime.MinValue : Convert.ToDateTime(reader["InsertedDate"])
                            });
                        }
                    }
                }

                ViewBag.ListProduct = result;
                ViewBag.countRowImport = result.Count;
            }
            catch (Exception ex)
            {
                ViewBag.ListProduct = new List<PrepareProductApiModel>();
                ViewBag.countRowImport = 0;
                ViewBag.ErrorMessage = ex.Message;
            }

            return View();
        }
    }
}