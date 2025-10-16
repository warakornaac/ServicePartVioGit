using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Data;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using ServiceCatalog.Models;
using System.Web.Services.Description;

namespace ServiceCatalog.Controllers
{
    public class ApprovePMController : Controller
    {
        // GET: ApprovePM
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
            return View();
        }


        public ActionResult LoadWaitingApproval(string PROD = "")
        {
            List<LinkageApproveChangeViewModel> result = new List<LinkageApproveChangeViewModel>();
            string message = string.Empty;

            try
            {
                using (SqlConnection conn = new SqlConnection(ConfigurationManager.ConnectionStrings["ServiceCatalogDB"].ConnectionString))
                {
                    conn.Open();
                    using (SqlCommand cmd = new SqlCommand("P_Get_Approval_VIO", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@inPRD", PROD);
                        cmd.Parameters.AddWithValue("@inKtype", "");
                        cmd.Parameters.AddWithValue("@inTrutype", "");
                        cmd.Parameters.AddWithValue("@inUser", "Thiraphon.pra");

                        SqlDataReader reader = cmd.ExecuteReader();
                        while (reader.Read())
                        {
                            var aprList = new VIO_Approave
                            {
                                MarketSegment = reader["MarketSegment"] != DBNull.Value ? reader["MarketSegment"].ToString() : string.Empty,
                                VehicleSegment = reader["VehicleSegment"] != DBNull.Value ? reader["VehicleSegment"].ToString() : string.Empty,
                                Maker = reader["Maker"] != DBNull.Value ? reader["Maker"].ToString() : string.Empty,
                                Model = reader["Model"] != DBNull.Value ? reader["Model"].ToString() : string.Empty,
                                Body = reader["Body"] != DBNull.Value ? reader["Body"].ToString() : string.Empty,
                                BodyType = reader["BodyType"] != DBNull.Value ? reader["BodyType"].ToString() : string.Empty,
                                EngineType = reader["EngineType"] != DBNull.Value ? reader["EngineType"].ToString() : string.Empty,
                                Strokes = reader["Strokes"] != DBNull.Value ? reader["Strokes"].ToString() : string.Empty,
                                Old_Ktype = reader["Old_Ktype"] != DBNull.Value ? reader["Old_Ktype"].ToString() : string.Empty,
                                New_Ktype = reader["New_Ktype"] != DBNull.Value ? reader["New_Ktype"].ToString() : string.Empty,
                                Old_DriveType = reader["Old_DriveType"] != DBNull.Value ? reader["Old_DriveType"].ToString() : string.Empty,
                                New_DriveType = reader["New_DriveType"] != DBNull.Value ? reader["New_DriveType"].ToString() : string.Empty,
                                Old_YearFrom = reader["Old_YearFrom"] != DBNull.Value ? reader["Old_YearFrom"].ToString() : string.Empty,
                                New_YearFrom = reader["New_YearFrom"] != DBNull.Value ? reader["New_YearFrom"].ToString() : string.Empty,
                                Old_YearTo = reader["Old_YearTo"] != DBNull.Value ? reader["Old_YearTo"].ToString() : string.Empty,
                                New_YearTo = reader["New_YearTo"] != DBNull.Value ? reader["New_YearTo"].ToString() : string.Empty,
                                Old_TruType = reader["Old_TruType"] != DBNull.Value ? reader["Old_TruType"].ToString() : string.Empty,
                                New_TruType = reader["New_TruType"] != DBNull.Value ? reader["New_TruType"].ToString() : string.Empty
                            };

                            var kTyp = aprList.New_Ktype;
                            var truTyp = aprList.New_TruType;
                            result.Add(new LinkageApproveChangeViewModel
                            {
                                apprvVIO = aprList,
                                itemLink = LoadDetailLink(kTyp, truTyp)
                            });
                        }


                    }

                }
            }
            catch (Exception ex)
            {
                message = ex.Message;
            }
            @ViewBag.messageError = message;
            @ViewBag.LinkageApproveChangeList = result;
            return PartialView("_WaitApproveList", new
            {
                @ViewBag.messageError,
                @ViewBag.LinkageApproveChangeList
            });
        }
        public List<ListLinkage> LoadDetailLink(string Ktype, string TruType)
        {
            var list = new List<ListLinkage>();
            using (SqlConnection conn = new SqlConnection(ConfigurationManager.ConnectionStrings["ServiceCatalogDB"].ConnectionString))
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand("P_Get_Approval_ItemList", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@inKtype", Ktype);
                    cmd.Parameters.AddWithValue("@inTrutype", TruType);
                    cmd.Parameters.AddWithValue("@inUser", "thiraphon.pra");

                    SqlDataReader reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        list.Add(new ListLinkage
                        {
                            Company = reader["Company"] != DBNull.Value ? reader["Company"].ToString().Trim() : string.Empty,
                            STKCOD = reader["STKCOD"] != DBNull.Value ? reader["STKCOD"].ToString().Trim() : string.Empty,
                            STKDES = reader["STKDES"] != DBNull.Value ? reader["STKDES"].ToString().Trim() : string.Empty,
                            KType = reader["KType"] != DBNull.Value ? reader["KType"].ToString().Trim() : string.Empty,
                            TruType = reader["TruType"] != DBNull.Value ? reader["TruType"].ToString().Trim() : string.Empty
                        });
                    }
                }
            }

            return list;
        }

        public JsonResult ApprovalVIO(string Ktype, string TruType, string OldTruType, string Flag)
        {
            string message = string.Empty;
            string respone = string.Empty;
            try
            {
                using (SqlConnection conn = new SqlConnection(ConfigurationManager.ConnectionStrings["ServiceCatalogDB"].ConnectionString))
                {
                    conn.Open();
                    using (SqlCommand cmd = new SqlCommand("P_Save_Approve_VIO", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@inKtype", Ktype);
                        cmd.Parameters.AddWithValue("@inTrutype", TruType);
                        cmd.Parameters.AddWithValue("@inOldTrutype", OldTruType);
                        cmd.Parameters.AddWithValue("@inFlag", flag);
                        cmd.Parameters.AddWithValue("@inUser", "Thiraphon.pra");

                        cmd.ExecuteNonQuery();
                    }
                }
                respone = "Y";
            }
            catch (Exception ex)
            {
                message = ex.Message;
                respone = "N";
            }


            return Json(new { message = message, respone = respone }, JsonRequestBehavior.AllowGet);
        }
    }
}