using ClosedXML.Excel;
using My.Lib;
using ServiceCatalog.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.OleDb;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace ServiceCatalog.Controllers
{
    public class UploadFileExcelController : Controller
    {
        // GET: UploadFileExcel
        public ActionResult Index()
        {
            return View();
        }

        public ActionResult LinkageUpload()
        {
            return View();
        }
        public ActionResult UploadLinkage(HttpPostedFileBase fileInput, string Stkcode)
        {
            string filePath = string.Empty;
            string txtMessage = string.Empty;
            string txtStatus = string.Empty;
            var countRowImport = 0;
            var countStatusSuccess = 0;
            var countStatusFail = 0;
            var insertedBy = Session["UserID"]?.ToString() ?? "";
            List<StoreImportLinkgeUpload> listLinkage = new List<StoreImportLinkgeUpload>();

            try
            {
                if (fileInput == null)
                    throw new Exception("กรุณาเลือกไฟล์ก่อนอัปโหลด");

                string extension = Path.GetExtension(fileInput.FileName).ToLower();
                if (extension != ".xls" && extension != ".xlsx")
                    throw new Exception("กรุณาอัปโหลดไฟล์ Excel (.xls หรือ .xlsx) เท่านั้น");

                // ----- บันทึกไฟล์ชั่วคราว -----
                string path = Server.MapPath("~/FileExcelUpload/");
                if (!Directory.Exists(path))
                    Directory.CreateDirectory(path);

                filePath = Path.Combine(path, Path.GetFileName(fileInput.FileName));
                if (System.IO.File.Exists(filePath))
                    System.IO.File.Delete(filePath);

                fileInput.SaveAs(filePath);
                System.Threading.Thread.Sleep(100);

                // ----- เปิด DB Connection -----
                var connectionString = Utils.GetConfig("ServiceCatalogDB");
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    // ----- เปิด Excel ด้วย OleDb -----
                    string conString = $@"Provider=Microsoft.ACE.OLEDB.12.0;Data Source={filePath};Extended Properties=Excel 12.0;Persist Security Info=False";

                    using (OleDbConnection excelConn = new OleDbConnection(conString))
                    {
                        excelConn.Open();

                        // อ่าน sheet Vehicle
                        using (OleDbCommand excelCmd = new OleDbCommand("SELECT * FROM [Vehicle$]", excelConn))
                        using (OleDbDataReader dReader = excelCmd.ExecuteReader())
                        {
                            while (dReader.Read())
                            {
                                // ใช้ index แทนชื่อ column เพื่อหลีกเลี่ยงปัญหา "No." (dot) และ case
                                // Col 0=No.  Col 9=Ktype  Col 10=Trutype
                                string colNo = dReader[0]?.ToString()?.Trim() ?? "";
                                string colKtype = dReader[9]?.ToString()?.Trim() ?? "";
                                string colTrutype = dReader[10]?.ToString()?.Trim() ?? "";

                                // ข้ามแถวที่ No. ว่าง
                                if (string.IsNullOrWhiteSpace(colNo))
                                    continue;

                                countRowImport++;

                                using (SqlCommand cmdUpload = new SqlCommand("P_Upload_Linkage_Excel", connection))
                                {
                                    cmdUpload.CommandType = CommandType.StoredProcedure;
                                    cmdUpload.Parameters.AddWithValue("@Stkcode", colNo);
                                    cmdUpload.Parameters.AddWithValue("@KType", colKtype);
                                    cmdUpload.Parameters.AddWithValue("@TruType", colTrutype);
                                    cmdUpload.Parameters.AddWithValue("@InsertedBy", insertedBy);

                                    // OUTPUT parameter — อ่านได้หลัง Reader ปิดเท่านั้น
                                    SqlParameter outStatus = new SqlParameter("@outGenstatus", SqlDbType.NVarChar, 100)
                                    {
                                        Direction = ParameterDirection.Output
                                    };
                                    cmdUpload.Parameters.Add(outStatus);

                                    // ใช้ ExecuteReader เพื่ออ่าน result set จาก SP
                                    using (SqlDataReader dr = cmdUpload.ExecuteReader())
                                    {
                                        while (dr.Read())
                                        {
                                            string statusImport = dr["StatusImport"]?.ToString() ?? "N";
                                            string errorImport = dr["ErrorImport"]?.ToString() ?? "";

                                            if (statusImport == "Y")
                                                countStatusSuccess++;
                                            else
                                                countStatusFail++;

                                            listLinkage.Add(new StoreImportLinkgeUpload()
                                            {
                                                Stkcode = dr["Stkcode"]?.ToString() ?? "",
                                                SeqLinkage = Convert.ToInt32(dr["SeqLinkage"]),
                                                KType = dr["KType"]?.ToString() ?? "",
                                                TruType = dr["TruType"]?.ToString() ?? "",
                                                InsertedBy = dr["InsertedBy"]?.ToString() ?? "",
                                                InsertedDate = dr["InsertedDate"]?.ToString() ?? "",
                                                UpdatedBy = dr["UpdatedBy"]?.ToString() ?? "",
                                                UpdatedDate = dr["UpdatedDate"]?.ToString() ?? "",
                                                StatusImport = statusImport,
                                                ErrorImport = errorImport
                                            });

                                            // Debug log
                                            System.Diagnostics.Debug.WriteLine(
                                                $"[Row {countRowImport}] Stkcode={dr["Stkcode"]} | " +
                                                $"KType={dr["KType"]} | " +
                                                $"StatusImport={statusImport} | " +
                                                $"Error={errorImport}"
                                            );
                                        }
                                    }
                                    // อ่าน OUTPUT parameter หลัง Reader ปิดแล้ว
                                    string outGenstatus = outStatus.Value?.ToString() ?? "";
                                    System.Diagnostics.Debug.WriteLine($"[Row {countRowImport}] outGenstatus={outGenstatus}");
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                txtMessage = $"{ex.Message} / {ex.Source} / {ex.HResult}";
                System.Diagnostics.Debug.WriteLine($"[UploadLinkage ERROR] {txtMessage}");
            }
            finally
            {
                // ลบไฟล์ชั่วคราวหลังประมวลผลเสร็จ
                if (!string.IsNullOrEmpty(filePath) && System.IO.File.Exists(filePath))
                {
                    try { System.IO.File.Delete(filePath); }
                    catch { /* ไม่ต้องทำอะไรถ้าลบไม่ได้ */ }
                }
            }

            ViewBag.status = txtStatus;
            ViewBag.message = txtMessage;
            ViewBag.listLinkage = listLinkage;
            ViewBag.countStatusSuccess = countStatusSuccess;
            ViewBag.countStatusFail = countStatusFail;
            ViewBag.countRowImport = countRowImport;

            return PartialView("_listLinkageUpload");
        }

        public ActionResult CompetitorUpload()
        {
            return View();
        }

        public ActionResult UploadCompetitor(HttpPostedFileBase fileInput, string Stkcode)
        {
            string filePath = string.Empty;
            string exerror = string.Empty;
            string txtMessage = string.Empty;
            string txtStatus = string.Empty;
            var countRowImport = 0;
            var countStatusSuccess = 0;
            var countStatusFail = 0;
            var insertedBy = Session["UserID"]?.ToString() ?? "";
            List<StoreImportCompetitorUpload> listCompetitor = new List<StoreImportCompetitorUpload>();

            try
            {
                if (fileInput != null)
                {
                    string path = Server.MapPath("~/FileExcelUpload/");

                    if (!Directory.Exists(path))
                    {
                        Directory.CreateDirectory(path);
                    }

                    filePath = path + Path.GetFileName(fileInput.FileName);

                    if (System.IO.File.Exists(filePath))
                    {
                        System.IO.File.Delete(filePath);
                    }

                    string extension = Path.GetExtension(fileInput.FileName);
                    fileInput.SaveAs(filePath);
                    System.Threading.Thread.Sleep(100);

                    string conString = string.Empty;
                    var connectionString = Utils.GetConfig("ServiceCatalogDB");
                    using (SqlConnection Connection = new SqlConnection(connectionString))
                    {
                        Connection.Open();

                        // Set connection string based on file extension
                        switch (extension.ToLower())
                        {
                            case ".xls":
                            case ".xlsx":
                                break;

                            default:
                                throw new Exception("กรุณาอัปโหลดไฟล์ Excel (.xls หรือ .xlsx)");
                        }
                        //conString = @"Provider=Microsoft.ACE.OLEDB.12.0;Data Source=" + filePath + ";Extended Properties='Excel 12.0 Xml;HDR=YES'";
                        conString = @"Provider=Microsoft.ACE.OLEDB.12.0;Data Source=" + filePath + ";Extended Properties=Excel 12.0;Persist Security Info=False";

                        // Using OleDbConnection and OleDbDataReader to read the Excel file
                        using (OleDbConnection excelConnection = new OleDbConnection(conString))
                        {
                            excelConnection.Open();
                            string query = "Select * from [Competitor$]";
                            using (OleDbCommand cmd = new OleDbCommand(query, excelConnection))
                            {
                                using (OleDbDataReader dReader = cmd.ExecuteReader())
                                {
                                    while (dReader.Read())
                                    {
                                        if (!string.IsNullOrWhiteSpace(dReader.GetValue(1)?.ToString()))
                                        //if (dReader.GetValue(2).ToString() != "")
                                        {
                                            // Using SQL Command to insert data into the database
                                            using (SqlCommand cmdUpload = new SqlCommand("P_Upload_Competitor_Excel", Connection))
                                            {
                                                cmdUpload.Connection = Connection;
                                                cmdUpload.CommandType = CommandType.StoredProcedure;
                                                cmdUpload.Parameters.AddWithValue("@Stkcode", dReader.GetValue(0).ToString());
                                                cmdUpload.Parameters.AddWithValue("@PartNo", dReader.GetValue(1).ToString());
                                                cmdUpload.Parameters.AddWithValue("@BrandName", dReader.GetValue(2).ToString());
                                                cmdUpload.Parameters.AddWithValue("@InsertedBy", insertedBy);
                                                //cmdUpload.Parameters.AddWithValue("@InsertedBy", Session["UserID"]?.ToString() ?? "");
                                                SqlParameter returnValue = new SqlParameter("@outGenstatus", SqlDbType.NVarChar, 100);
                                                returnValue.Direction = System.Data.ParameterDirection.Output;
                                                cmdUpload.Parameters.Add(returnValue);
                                                using (SqlDataReader dr = cmdUpload.ExecuteReader())
                                                {
                                                    // debug ดู column names
                                                    for (int i = 0; i < dr.FieldCount; i++)
                                                    {
                                                        System.Diagnostics.Debug.WriteLine($"Col[{i}] = {dr.GetName(i)}");
                                                    }

                                                    while (dr.Read())
                                                    {
                                                        System.Diagnostics.Debug.WriteLine(
                                                            $"Stkcode={dr["Stkcode"]} | " +
                                                            $"InsertedBy={dr["InsertedBy"]} | " +
                                                            $"StatusImport={dr["StatusImport"]}"
                                                        );
                                                        countRowImport++;
                                                        if (dr["StatusImport"].ToString() == "Y")
                                                        {
                                                            countStatusSuccess++;
                                                        }
                                                        if (dr["StatusImport"].ToString() == "N")
                                                        {
                                                            countStatusFail++;
                                                        }

                                                        listCompetitor.Add(new StoreImportCompetitorUpload()
                                                        {
                                                            Stkcode = dr["Stkcode"].ToString(),
                                                            SeqCompetitor = Convert.ToInt32(dr["SeqCompetitor"]),
                                                            PartNo = dr["PartNo"].ToString(),
                                                            BrandName = dr["BrandName"].ToString(),
                                                            InsertedBy = dr["InsertedBy"].ToString(),
                                                            InsertedDate = dr["InsertedDate"].ToString(),
                                                            UpdatedBy = dr["UpdatedBy"].ToString(),
                                                            UpdatedDate = dr["UpdatedDate"].ToString(),
                                                            StatusImport = dr["StatusImport"].ToString(),
                                                            ErrorImport = dr["ErrorImport"].ToString()
                                                        });
                                                    }
                                                }
                                                cmdUpload.Dispose();
                                            }
                                        }
                                    }
                                }
                            }
                        }

                        Connection.Close();
                    }
                }
            }
            catch (Exception ex)
            {
                txtMessage = ex.Message + '/' + ex.Source + '/' + ex.HelpLink + '/' + ex.HResult;
            }

            ViewBag.status = txtStatus;
            ViewBag.message = txtMessage;
            ViewBag.listCompetitor = listCompetitor;
            ViewBag.countStatusSuccess = countStatusSuccess;
            ViewBag.countStatusFail = countStatusFail;
            ViewBag.countRowImport = countRowImport;

            return PartialView("_listCompetitor");
        }

        public ActionResult OEUpload()
        {
            return View();
        }
        public ActionResult UploadOE(HttpPostedFileBase fileInput, string Stkcode)
        {
            string filePath = string.Empty;
            string exerror = string.Empty;
            string txtMessage = string.Empty;
            string txtStatus = string.Empty;
            var countRowImport = 0;
            var countStatusSuccess = 0;
            var countStatusFail = 0;
            var insertedBy = Session["UserID"]?.ToString() ?? "";
            List<StoreImportOEUpload> listOem = new List<StoreImportOEUpload>();

            try
            {
                if (fileInput != null)
                {
                    string path = Server.MapPath("~/FileExcelUpload/");

                    if (!Directory.Exists(path))
                    {
                        Directory.CreateDirectory(path);
                    }

                    filePath = path + Path.GetFileName(fileInput.FileName);

                    if (System.IO.File.Exists(filePath))
                    {
                        System.IO.File.Delete(filePath);
                    }

                    string extension = Path.GetExtension(fileInput.FileName);
                    fileInput.SaveAs(filePath);
                    System.Threading.Thread.Sleep(100);

                    string conString = string.Empty;
                    var connectionString = Utils.GetConfig("ServiceCatalogDB");
                    using (SqlConnection Connection = new SqlConnection(connectionString))
                    {
                        Connection.Open();

                        // Set connection string based on file extension
                        switch (extension.ToLower())
                        {
                            case ".xls":
                            case ".xlsx":
                                break;

                            default:
                                throw new Exception("กรุณาอัปโหลดไฟล์ Excel (.xls หรือ .xlsx)");
                        }
                        //conString = @"Provider=Microsoft.ACE.OLEDB.12.0;Data Source=" + filePath + ";Extended Properties='Excel 12.0 Xml;HDR=YES'";
                        conString = @"Provider=Microsoft.ACE.OLEDB.12.0;Data Source=" + filePath + ";Extended Properties=Excel 12.0;Persist Security Info=False";

                        // Using OleDbConnection and OleDbDataReader to read the Excel file
                        using (OleDbConnection excelConnection = new OleDbConnection(conString))
                        {
                            excelConnection.Open();
                            string query = "Select * from [OE$]";
                            using (OleDbCommand cmd = new OleDbCommand(query, excelConnection))
                            {
                                using (OleDbDataReader dReader = cmd.ExecuteReader())
                                {
                                    while (dReader.Read())
                                    {
                                        if (!string.IsNullOrWhiteSpace(dReader.GetValue(1)?.ToString()))
                                        //if (dReader.GetValue(2).ToString() != "")
                                        {
                                            // Using SQL Command to insert data into the database
                                            using (SqlCommand cmdUpload = new SqlCommand("P_Upload_Oem_Excel", Connection))
                                            {
                                                cmdUpload.Connection = Connection;
                                                cmdUpload.CommandType = CommandType.StoredProcedure;
                                                cmdUpload.Parameters.AddWithValue("@Stkcode", dReader.GetValue(0).ToString());
                                                cmdUpload.Parameters.AddWithValue("@OemNumber", dReader.GetValue(1).ToString());
                                                cmdUpload.Parameters.AddWithValue("@MakerName", dReader.GetValue(2).ToString());
                                                cmdUpload.Parameters.AddWithValue("@InsertedBy", insertedBy);
                                                SqlParameter returnValue = new SqlParameter("@outGenstatus", SqlDbType.NVarChar, 100);
                                                returnValue.Direction = System.Data.ParameterDirection.Output;
                                                cmdUpload.Parameters.Add(returnValue);
                                                using (SqlDataReader dr = cmdUpload.ExecuteReader())
                                                {
                                                    // debug ดู column names
                                                    for (int i = 0; i < dr.FieldCount; i++)
                                                    {
                                                        System.Diagnostics.Debug.WriteLine($"Col[{i}] = {dr.GetName(i)}");
                                                    }

                                                    while (dr.Read())
                                                    {
                                                        System.Diagnostics.Debug.WriteLine(
                                                            $"Stkcode={dr["Stkcode"]} | " +
                                                            $"InsertedBy={dr["InsertedBy"]} | " +
                                                            $"StatusImport={dr["StatusImport"]}"
                                                        );
                                                        countRowImport++;
                                                        if (dr["StatusImport"].ToString() == "Y")
                                                        {
                                                            countStatusSuccess++;
                                                        }
                                                        if (dr["StatusImport"].ToString() == "N")
                                                        {
                                                            countStatusFail++;
                                                        }

                                                        listOem.Add(new StoreImportOEUpload()
                                                        {
                                                            Stkcode = dr["Stkcode"].ToString(),
                                                            SeqOem = Convert.ToInt32(dr["SeqOem"]),
                                                            OemNumber = dr["OemNumber"].ToString(),
                                                            MakerName = dr["MakerName"].ToString(),
                                                            InsertedBy = dr["InsertedBy"].ToString(),
                                                            InsertedDate = dr["InsertedDate"].ToString(),
                                                            UpdatedBy = dr["UpdatedBy"].ToString(),
                                                            UpdatedDate = dr["UpdatedDate"].ToString(),
                                                            StatusImport = dr["StatusImport"].ToString(),
                                                            ErrorImport = dr["ErrorImport"].ToString()
                                                        });
                                                    }
                                                }
                                                cmdUpload.Dispose();
                                            }
                                        }
                                    }
                                }
                            }
                        }

                        Connection.Close();
                    }
                }
            }
            catch (Exception ex)
            {
                txtMessage = ex.Message + '/' + ex.Source + '/' + ex.HelpLink + '/' + ex.HResult;
            }

            ViewBag.status = txtStatus;
            ViewBag.message = txtMessage;
            ViewBag.listOem = listOem;
            ViewBag.countStatusSuccess = countStatusSuccess;
            ViewBag.countStatusFail = countStatusFail;
            ViewBag.countRowImport = countRowImport;

            return PartialView("_listOEUpload");
        }

        [HttpGet]
        public ActionResult FittingUpload()
        {
            return View();
        }
        [HttpPost]
        public ActionResult FittingUpload(HttpPostedFileBase fileInput, string Stkcode)
        {
            string filePath = string.Empty;
            string txtMessage = string.Empty;
            string txtStatus = string.Empty;
            var countRowImport = 0;
            var countStatusSuccess = 0;
            var countStatusFail = 0;
            var insertedBy = Session["UserID"]?.ToString() ?? "";
            List<StoreImportFittingUpload> listFitting = new List<StoreImportFittingUpload>();

            try
            {
                if (fileInput == null)
                    throw new Exception("กรุณาเลือกไฟล์ก่อนอัปโหลด");

                string extension = Path.GetExtension(fileInput.FileName).ToLower();
                if (extension != ".xls" && extension != ".xlsx")
                    throw new Exception("กรุณาอัปโหลดไฟล์ Excel (.xls หรือ .xlsx) เท่านั้น");

                // ----- บันทึกไฟล์ชั่วคราว -----
                string path = Server.MapPath("~/FileExcelUpload/");
                if (!Directory.Exists(path))
                    Directory.CreateDirectory(path);

                filePath = Path.Combine(path, Path.GetFileName(fileInput.FileName));
                if (System.IO.File.Exists(filePath))
                    System.IO.File.Delete(filePath);

                fileInput.SaveAs(filePath);
                System.Threading.Thread.Sleep(100);

                // ----- เปิด DB Connection -----
                var connectionString = Utils.GetConfig("ServiceCatalogDB");
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    // ----- เปิด Excel ด้วย OleDb -----
                    string conString = $@"Provider=Microsoft.ACE.OLEDB.12.0;Data Source={filePath};Extended Properties=Excel 12.0;Persist Security Info=False";

                    using (OleDbConnection excelConn = new OleDbConnection(conString))
                    {
                        excelConn.Open();

                        // อ่าน sheet Vehicle
                        using (OleDbCommand excelCmd = new OleDbCommand("SELECT * FROM [Master$]", excelConn))
                        using (OleDbDataReader dReader = excelCmd.ExecuteReader())
                        {
                            while (dReader.Read())
                            {
                                // ใช้ index แทนชื่อ column เพื่อหลีกเลี่ยงปัญหา "No." (dot) และ case
                                string colStkcode = dReader[2]?.ToString()?.Trim() ?? "";
                                string colAxis = dReader[9]?.ToString()?.Trim() ?? "";
                                string colSide = dReader[10]?.ToString()?.Trim() ?? "";
                                string colLevel = dReader[11]?.ToString()?.Trim() ?? "";
                                string colDirection = dReader[12]?.ToString()?.Trim() ?? "";

                                // ข้ามแถวที่ No. ว่าง
                                if (string.IsNullOrWhiteSpace(colStkcode))
                                    continue;

                                countRowImport++;

                                using (SqlCommand cmdUpload = new SqlCommand("P_Upload_Fitting_Excel", connection))
                                {
                                    cmdUpload.CommandType = CommandType.StoredProcedure;
                                    cmdUpload.Parameters.AddWithValue("@Stkcode", colStkcode);
                                    cmdUpload.Parameters.AddWithValue("@Axis", colAxis);
                                    cmdUpload.Parameters.AddWithValue("@Side", colSide);
                                    cmdUpload.Parameters.AddWithValue("@Level", colLevel);
                                    cmdUpload.Parameters.AddWithValue("@Direction", colDirection);
                                    cmdUpload.Parameters.AddWithValue("@InsertedBy", insertedBy);

                                    // OUTPUT parameter — อ่านได้หลัง Reader ปิดเท่านั้น
                                    SqlParameter outStatus = new SqlParameter("@outGenstatus", SqlDbType.NVarChar, 100)
                                    {
                                        Direction = ParameterDirection.Output
                                    };
                                    cmdUpload.Parameters.Add(outStatus);

                                    // ใช้ ExecuteReader เพื่ออ่าน result set จาก SP
                                    using (SqlDataReader dr = cmdUpload.ExecuteReader())
                                    {
                                        while (dr.Read())
                                        {
                                            string statusImport = dr["StatusImport"]?.ToString() ?? "N";
                                            string errorImport = dr["ErrorImport"]?.ToString() ?? "";

                                            if (statusImport == "Y")
                                                countStatusSuccess++;
                                            else
                                                countStatusFail++;

                                            listFitting.Add(new StoreImportFittingUpload()
                                            {
                                                Stkcode = dr["Stkcode"]?.ToString() ?? "",
                                                Axis = dr["Axis"]?.ToString() ?? "",
                                                Side = dr["Side"]?.ToString() ?? "",
                                                Level = dr["Level"]?.ToString() ?? "",
                                                Direction = dr["Direction"]?.ToString() ?? "",
                                                InsertedBy = dr["InsertedBy"]?.ToString() ?? "",
                                                InsertedDate = dr["InsertedDate"]?.ToString() ?? "",
                                                UpdatedBy = dr["UpdatedBy"]?.ToString() ?? "",
                                                UpdatedDate = dr["UpdatedDate"]?.ToString() ?? "",
                                                StatusImport = statusImport,
                                                ErrorImport = errorImport
                                            });

                                            // Debug log
                                            System.Diagnostics.Debug.WriteLine(
                                                $"[Row {countRowImport}] Stkcode={dr["Stkcode"]} | " +
                                                $"StatusImport={statusImport} | " +
                                                $"Error={errorImport}"
                                            );
                                        }
                                    }
                                    // อ่าน OUTPUT parameter หลัง Reader ปิดแล้ว
                                    string outGenstatus = outStatus.Value?.ToString() ?? "";
                                    System.Diagnostics.Debug.WriteLine($"[Row {countRowImport}] outGenstatus={outGenstatus}");
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                txtMessage = $"{ex.Message} / {ex.Source} / {ex.HResult}";
                System.Diagnostics.Debug.WriteLine($"[UploadFitting ERROR] {txtMessage}");
            }
            finally
            {
                // ลบไฟล์ชั่วคราวหลังประมวลผลเสร็จ
                if (!string.IsNullOrEmpty(filePath) && System.IO.File.Exists(filePath))
                {
                    try { System.IO.File.Delete(filePath); }
                    catch { /* ไม่ต้องทำอะไรถ้าลบไม่ได้ */ }
                }
            }

            ViewBag.status = txtStatus;
            ViewBag.message = txtMessage;
            ViewBag.listFitting = listFitting;
            ViewBag.countStatusSuccess = countStatusSuccess;
            ViewBag.countStatusFail = countStatusFail;
            ViewBag.countRowImport = countRowImport;

            return PartialView("_listFittingUpload");
        }

    }
}