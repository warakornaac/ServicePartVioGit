using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using ServiceCatalog.Models; // Ensure this using directive is present

namespace ServiceCatalog.Controllers
{
    public class ManageUserController : Controller
    {
        // GET: ManageUser
        private string connectionString = ConfigurationManager.ConnectionStrings["ServiceCatalogDB"].ConnectionString;

        // GET: /ManageUser/
        public ActionResult Index()
        {
            List<UsrGrp> userList = new List<UsrGrp>();

            try
            {
                SqlConnection Connection = new SqlConnection(connectionString);
                using (Connection)
                {
                    string query = @"SELECT [ID],[UserID],[UserName],[Password],[UserType],[Company],[Department]
                                    ,[Email],[Slmcod],[InsertedDate],[InsertedBy],[UpdatedDate],[UpdatedBy]
                                   FROM [dbo].[UsrTbl]";

                    using (SqlCommand cmd = new SqlCommand(query, Connection))
                    {
                        Connection.Open();
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                UsrGrp user = new UsrGrp
                                {
                                    ID = reader.GetInt32(reader.GetOrdinal("ID")),
                                    UserID = reader.IsDBNull(reader.GetOrdinal("UserID")) ? null : reader.GetString(reader.GetOrdinal("UserID")),
                                    UserName = reader.IsDBNull(reader.GetOrdinal("UserName")) ? null : reader.GetString(reader.GetOrdinal("UserName")),
                                    Password = reader.IsDBNull(reader.GetOrdinal("Password")) ? null : reader.GetString(reader.GetOrdinal("Password")),
                                    UserType = reader.IsDBNull(reader.GetOrdinal("UserType")) ? 0 : reader.GetInt32(reader.GetOrdinal("UserType")),
                                    Company = reader.IsDBNull(reader.GetOrdinal("Company")) ? null : reader.GetString(reader.GetOrdinal("Company")),
                                    Email = reader.IsDBNull(reader.GetOrdinal("Email")) ? null : reader.GetString(reader.GetOrdinal("Email")),
                                    Slmcod = reader.IsDBNull(reader.GetOrdinal("Slmcod")) ? null : reader.GetString(reader.GetOrdinal("Slmcod")),
                                    Department = reader.IsDBNull(reader.GetOrdinal("Department")) ? null : reader.GetString(reader.GetOrdinal("Department")),
                                    InsertedDate = reader.IsDBNull(reader.GetOrdinal("InsertedDate")) ? DateTime.MinValue : reader.GetDateTime(reader.GetOrdinal("InsertedDate")),
                                    InsertedBy = reader.IsDBNull(reader.GetOrdinal("InsertedBy")) ? null : reader.GetString(reader.GetOrdinal("InsertedBy")),
                                    UpdatedDate = reader.IsDBNull(reader.GetOrdinal("UpdatedDate")) ? DateTime.MinValue : reader.GetDateTime(reader.GetOrdinal("UpdatedDate")),
                                    UpdatedBy = reader.IsDBNull(reader.GetOrdinal("UpdatedBy")) ? null : reader.GetString(reader.GetOrdinal("UpdatedBy"))
                                };

                                userList.Add(user);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ViewBag.ErrorMessage = "Something went wrong: " + ex.Message;
            }

            return View(userList);
        }

        // GET: GetUserList for DataTables/jsGrid
        public JsonResult GetUserList()
        {
            List<UsrGrp> userList = new List<UsrGrp>();

            try
            {
                SqlConnection Connection = new SqlConnection(connectionString);
                using (Connection)
                {
                    string query = @"SELECT [ID],[UserID],[UserName],[Password],[UserType],[Company],[Department]
                                    ,[Email],[Slmcod],[InsertedDate],[InsertedBy],[UpdatedDate],[UpdatedBy]
                                   FROM [dbo].[UsrTbl]";

                    using (SqlCommand cmd = new SqlCommand(query, Connection))
                    {
                        Connection.Open();
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                UsrGrp user = new UsrGrp
                                {
                                    ID = reader.GetInt32(reader.GetOrdinal("ID")),
                                    UserID = reader.IsDBNull(reader.GetOrdinal("UserID")) ? null : reader.GetString(reader.GetOrdinal("UserID")),
                                    UserName = reader.IsDBNull(reader.GetOrdinal("UserName")) ? null : reader.GetString(reader.GetOrdinal("UserName")),
                                    Password = reader.IsDBNull(reader.GetOrdinal("Password")) ? null : reader.GetString(reader.GetOrdinal("Password")),
                                    UserType = reader.IsDBNull(reader.GetOrdinal("UserType")) ? 0 : reader.GetInt32(reader.GetOrdinal("UserType")),
                                    Company = reader.IsDBNull(reader.GetOrdinal("Company")) ? null : reader.GetString(reader.GetOrdinal("Company")),
                                    Email = reader.IsDBNull(reader.GetOrdinal("Email")) ? null : reader.GetString(reader.GetOrdinal("Email")),
                                    Slmcod = reader.IsDBNull(reader.GetOrdinal("Slmcod")) ? null : reader.GetString(reader.GetOrdinal("Slmcod")),
                                    Department = reader.IsDBNull(reader.GetOrdinal("Department")) ? null : reader.GetString(reader.GetOrdinal("Department")),
                                    InsertedDate = reader.IsDBNull(reader.GetOrdinal("InsertedDate")) ? DateTime.MinValue : reader.GetDateTime(reader.GetOrdinal("InsertedDate")),
                                    InsertedBy = reader.IsDBNull(reader.GetOrdinal("InsertedBy")) ? null : reader.GetString(reader.GetOrdinal("InsertedBy")),
                                    UpdatedDate = reader.IsDBNull(reader.GetOrdinal("UpdatedDate")) ? DateTime.MinValue : reader.GetDateTime(reader.GetOrdinal("UpdatedDate")),
                                    UpdatedBy = reader.IsDBNull(reader.GetOrdinal("UpdatedBy")) ? null : reader.GetString(reader.GetOrdinal("UpdatedBy"))
                                };

                                userList.Add(user);
                            }
                        }
                    }
                }

                return Json(new { success = true, data = userList }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpPost]
        public JsonResult Create(UsrGrp user)
        {
            try
            {
                using (SqlConnection Connection = new SqlConnection(connectionString))
                {
                    using (SqlCommand cmd = new SqlCommand("P_AddUserAD", Connection))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;

                        // Trim the incoming user id before passing to the stored procedure
                        var trimmedUser = (user?.UserID ?? string.Empty).Trim();
                        cmd.Parameters.AddWithValue("@inUser", string.IsNullOrEmpty(trimmedUser) ? (object)DBNull.Value : trimmedUser);
                        cmd.Parameters.AddWithValue("@instatus", (object)user.UserType ?? DBNull.Value);

                        SqlParameter outGenStatusParam = new SqlParameter("@outGenstatus", SqlDbType.NVarChar, 100)
                        {
                            Direction = ParameterDirection.Output
                        };
                        cmd.Parameters.Add(outGenStatusParam);

                        Connection.Open();
                        cmd.ExecuteNonQuery();

                        string statusMessage = outGenStatusParam.Value != DBNull.Value
                            ? outGenStatusParam.Value.ToString()
                            : "";

                        if (statusMessage == "Success")
                        {
                            return Json(new { success = true, message = "Added successfully." });
                        }
                        else
                        {
                            return Json(new { success = false, message = statusMessage });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        // GET: Get user by ID
        [HttpGet]
        public JsonResult GetUserById(int id)
        {
            try
            {
                using (SqlConnection Connection = new SqlConnection(connectionString))
                {
                    string query = @"SELECT [ID],[UserID],[UserName],[Password],[UserType],[Company],[Department]
                                    ,[Email],[Slmcod],[InsertedDate],[InsertedBy],[UpdatedDate],[UpdatedBy]
                                   FROM [dbo].[UsrTbl]
                                    WHERE [ID] = @ID";

                    using (SqlCommand cmd = new SqlCommand(query, Connection))
                    {
                        cmd.Parameters.AddWithValue("@ID", id);
                        Connection.Open();

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                UsrGrp user = new UsrGrp
                                {
                                    ID = reader.GetInt32(reader.GetOrdinal("ID")),
                                    UserID = reader.IsDBNull(reader.GetOrdinal("UserID")) ? null : reader.GetString(reader.GetOrdinal("UserID")),
                                    UserName = reader.IsDBNull(reader.GetOrdinal("UserName")) ? null : reader.GetString(reader.GetOrdinal("UserName")),
                                    Password = reader.IsDBNull(reader.GetOrdinal("Password")) ? null : reader.GetString(reader.GetOrdinal("Password")),
                                    UserType = reader.IsDBNull(reader.GetOrdinal("UserType")) ? 0 : reader.GetInt32(reader.GetOrdinal("UserType")),
                                    Company = reader.IsDBNull(reader.GetOrdinal("Company")) ? null : reader.GetString(reader.GetOrdinal("Company")),
                                    Email = reader.IsDBNull(reader.GetOrdinal("Email")) ? null : reader.GetString(reader.GetOrdinal("Email")),
                                    Slmcod = reader.IsDBNull(reader.GetOrdinal("Slmcod")) ? null : reader.GetString(reader.GetOrdinal("Slmcod")),
                                    Department = reader.IsDBNull(reader.GetOrdinal("Department")) ? null : reader.GetString(reader.GetOrdinal("Department")),
                                    InsertedDate = reader.IsDBNull(reader.GetOrdinal("InsertedDate")) ? DateTime.MinValue : reader.GetDateTime(reader.GetOrdinal("InsertedDate")),
                                    InsertedBy = reader.IsDBNull(reader.GetOrdinal("InsertedBy")) ? null : reader.GetString(reader.GetOrdinal("InsertedBy")),
                                    UpdatedDate = reader.IsDBNull(reader.GetOrdinal("UpdatedDate")) ? DateTime.MinValue : reader.GetDateTime(reader.GetOrdinal("UpdatedDate")),
                                    UpdatedBy = reader.IsDBNull(reader.GetOrdinal("UpdatedBy")) ? null : reader.GetString(reader.GetOrdinal("UpdatedBy"))
                                };

                                return Json(new { success = true, data = user }, JsonRequestBehavior.AllowGet);
                            }
                        }
                    }
                }

                return Json(new { success = false, message = "Not found" }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        // POST: Update user
        [HttpPost]
        public JsonResult Update(UsrGrp user)
        {
            try
            {
                using (SqlConnection Connection = new SqlConnection(connectionString))
                {
                    using (SqlCommand cmd = new SqlCommand("P_UpdateUserAD", Connection))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;

                        // ส่ง UsrID เป็นตัวอ้างอิง และ UsrTyp เป็นค่าที่จะเปลี่ยน
                        cmd.Parameters.AddWithValue("@inUser", user.UserID);
                        cmd.Parameters.AddWithValue("@instatus", user.UserType);
                        cmd.Parameters.AddWithValue("@Slmcod", (object)user.Slmcod ?? DBNull.Value);

                        SqlParameter outParam = new SqlParameter("@outGenstatus", SqlDbType.NVarChar, 100)
                        { Direction = ParameterDirection.Output };
                        cmd.Parameters.Add(outParam);

                        Connection.Open();
                        cmd.ExecuteNonQuery();

                        string result = outParam.Value.ToString();
                        if (result == "Update Success")
                        {
                            return Json(new { success = true, message = "Updated successfully." });
                        }
                        return Json(new { success = false, message = result });
                    }
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // POST: Delete user
        [HttpPost]
        public JsonResult Delete(int id)
        {
            try
            {
                SqlConnection Connection = new SqlConnection(connectionString);
                using (Connection)
                {
                    string query = @"DELETE FROM [dbo].[UsrTbl] WHERE [ID] = @ID";
                    using (SqlCommand cmd = new SqlCommand(query, Connection))
                    {
                        cmd.Parameters.AddWithValue("@ID", id);
                        Connection.Open();
                        int result = cmd.ExecuteNonQuery();

                        if (result > 0)
                        {
                            return Json(new { success = true, message = "Deleted successfully." });
                        }
                        else
                        {
                            return Json(new { success = false, message = "Deleted failed." });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }
    }
}