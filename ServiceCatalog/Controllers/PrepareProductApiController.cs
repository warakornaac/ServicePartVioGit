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
                                VerifyStatus = reader["VerifyStatus"] as string,
                                VerifyStatusRemark = reader["VerifyStatusRemark"] as string,
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

        public JsonResult GetProductApiListVerifyCount()
        {
            string message = string.Empty;
            List<string> list = new List<string>();
            int pendingCount = 0;
            int totalResult = 0;
            string conString = ConfigurationManager.ConnectionStrings["ServiceCatalogDB"].ConnectionString;
            string usr = Session["UserID"].ToString();
            try
            {
                using (SqlConnection conn = new SqlConnection(conString))
                {
                    using (SqlCommand cmd = new SqlCommand("P_Update_ProductApi", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@inUser", usr);
                        cmd.Parameters.AddWithValue("@inSTKCOD", "");
                        conn.Open();
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                pendingCount = Convert.ToInt32(reader["PendingCount"]);
                                totalResult = Convert.ToInt32(reader["TotalResult"]);
                            }
                        }
                    }
                }
                message  = "Y";
            }
            catch(Exception ex)
            {
                message = ex.Message;
            }

            return Json(new { message = message, pendingCount = pendingCount, totalResult= totalResult }, JsonRequestBehavior.AllowGet);
        }

        public JsonResult UpdateProductAPI()
        {
            string message = string.Empty;
            List<VIO_VehicleSegment> listVehicelSeg = new List<VIO_VehicleSegment>();
            string conString = ConfigurationManager.ConnectionStrings["ServiceCatalogDB"].ConnectionString;
            string usr = Session["UserID"].ToString();
            try
            {
                using (SqlConnection conn = new SqlConnection(conString))
                {
                    using (SqlCommand cmd = new SqlCommand("P_Update_ProductApi", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@inUser", usr);
                        cmd.Parameters.AddWithValue("@inSTKCOD", "");

                        SqlParameter outResult = new SqlParameter("@outResult", SqlDbType.VarChar, 200);
                        outResult.Direction = ParameterDirection.Output;
                        cmd.Parameters.Add(outResult);

                        conn.Open();
                        cmd.ExecuteNonQuery();

                        message = outResult.Value.ToString();

                        if (string.IsNullOrEmpty(message))
                        {
                            message = "Y";
                        }
                    }
                }
            }
            catch(Exception ex)
            {
                message = ex.Message;
            }
            return Json(new { message = message }, JsonRequestBehavior.AllowGet);
        }
    }
}