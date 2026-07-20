using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace ServiceCatalog.Services
{
    public class ImageSyncService
    {
        private readonly string _connectionString;
        private readonly string _nasPath;
        private readonly string _nasUser;
        private readonly string _nasPassword;
        private readonly string _nasDomain;

        public ImageSyncService(string connectionString)
        {
            _connectionString = connectionString;
            _nasPath = ConfigurationManager.AppSettings["NasPath"];
            _nasUser = ConfigurationManager.AppSettings["NasUser"];
            _nasPassword = ConfigurationManager.AppSettings["NasPassword"];
            _nasDomain = ConfigurationManager.AppSettings["NasDomain"] ?? "";
        }

        // ─────────────────────────────────────────────────────────
        // PUBLIC: SyncOne
        // ─────────────────────────────────────────────────────────
        public async Task<SyncResult> SyncOneAsync(string stkcode, int seqImage)
        {
            var item = await GetItemAsync(stkcode, seqImage);
            if (item == null)
                return SyncResult.Fail("ไม่พบข้อมูลใน Database");

            return await ProcessItemAsync(item);
        }

        // ─────────────────────────────────────────────────────────
        // PUBLIC: SyncAll
        // ─────────────────────────────────────────────────────────
        public async Task<SyncAllResult> SyncAllAsync(List<ProductImageJob> jobs)
        {
            int success = 0;
            int fail = 0;

            foreach (var job in jobs)
            {
                var result = await ProcessItemAsync(job);
                if (result.Success) success++;
                else fail++;
            }

            return new SyncAllResult
            {
                SuccessCount = success,
                FailCount = fail
            };
        }

        // ─────────────────────────────────────────────────────────
        // PUBLIC: GetJobsByKeys
        // ─────────────────────────────────────────────────────────
        public async Task<List<ProductImageJob>> GetJobsByKeysAsync(
            List<SyncKey> keys)
        {
            var list = new List<ProductImageJob>();
            if (keys == null || keys.Count == 0) return list;

            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                foreach (var key in keys)
                {
                    using (var cmd = new SqlCommand(@"
                        SELECT Stkcode, SeqImage, Filename, Url, RetryCount
                        FROM   Product_Image
                        WHERE  Stkcode  = @Stkcode
                          AND  SeqImage = @SeqImage", conn))
                    {
                        cmd.Parameters.AddWithValue("@Stkcode", key.Stkcode);
                        cmd.Parameters.AddWithValue("@SeqImage", key.SeqImage);

                        using (var r = await cmd.ExecuteReaderAsync())
                        {
                            if (await r.ReadAsync())
                            {
                                list.Add(new ProductImageJob
                                {
                                    Stkcode = r.GetString(0),
                                    SeqImage = r.GetInt32(1),
                                    Filename = r.IsDBNull(2) ? null : r.GetString(2),
                                    Url = r.GetString(3),
                                    RetryCount = r.GetInt32(4)
                                });
                            }
                        }
                    }
                }
            }

            return list;
        }

        // ─────────────────────────────────────────────────────────
        // PUBLIC: ProcessItem — บันทึกไฟล์ลง NAS/{Stkcode}/
        // ─────────────────────────────────────────────────────────
        public async Task<SyncResult> ProcessItemAsync(ProductImageJob job)
        {
            await SetStatusAsync(job.Stkcode, job.SeqImage, "PROCESSING");
            try
            {
                System.Diagnostics.Trace.TraceInformation(
                    string.Format("[Sync] START Stkcode={0} SeqImage={1}",
                    job.Stkcode, job.SeqImage));

                using (new NasConnection(_nasPath, _nasUser, _nasPassword, _nasDomain))
                {
                    // สร้าง folder ตาม Stkcode บน NAS
                    string stkFolder = Path.Combine(
                        _nasPath, SanitizeFolderName(job.Stkcode));

                    if (!Directory.Exists(stkFolder))
                        Directory.CreateDirectory(stkFolder);

                    // Resolve filename
                    var uri = new Uri(job.Url);
                    string filename = Path.GetFileName(uri.LocalPath);
                    if (string.IsNullOrWhiteSpace(filename))
                        filename = Guid.NewGuid().ToString("N") + ".jpg";
                    filename = Path.GetFileName(filename);

                    string localPath = Path.Combine(stkFolder, filename);

                    System.Diagnostics.Trace.TraceInformation(
                        "[Sync] Save to: " + localPath);

                    // TLS + Download
                    System.Net.ServicePointManager.SecurityProtocol =
                        System.Net.SecurityProtocolType.Tls12;

                    using (var http = new HttpClient())
                    {
                        http.Timeout = TimeSpan.FromSeconds(15);
                        var res = await http.GetAsync(job.Url);

                        if (!res.IsSuccessStatusCode)
                        {
                            string reason = string.Format("HTTP {0}: {1}",
                                (int)res.StatusCode, res.ReasonPhrase);
                            int retry = job.RetryCount + 1;
                            string status = retry >= 3 ? "FAILED" : "PENDING";
                            await SetFailedAsync(job.Stkcode, job.SeqImage,
                                                 status, retry, reason);
                            return SyncResult.Fail(reason);
                        }

                        byte[] bytes = await res.Content.ReadAsByteArrayAsync();
                        System.IO.File.WriteAllBytes(localPath, bytes);

                        System.Diagnostics.Trace.TraceInformation(
                            string.Format("[Sync] Downloaded {0} bytes → {1}",
                            bytes.Length, localPath));
                    }

                    await SetSuccessAsync(job.Stkcode, job.SeqImage, filename, localPath);

                    System.Diagnostics.Trace.TraceInformation(
                        "[Sync] SUCCESS: " + localPath);

                    return SyncResult.Ok(filename);
                }
            }
            catch (Exception ex)
            {
                string error = ex.Message;
                if (ex.InnerException != null)
                    error += " | " + ex.InnerException.Message;

                System.Diagnostics.Trace.TraceError("[Sync] FAILED: " + error);

                int retry = job.RetryCount + 1;
                string status = retry >= 3 ? "FAILED" : "PENDING";
                await SetFailedAsync(job.Stkcode, job.SeqImage, status, retry, error);
                return SyncResult.Fail(error);
            }
        }
        // ─────────────────────────────────────────────────────────
        // PRIVATE: DB helpers
        // ─────────────────────────────────────────────────────────
        private async Task<ProductImageJob> GetItemAsync(
            string stkcode, int seqImage)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                using (var cmd = new SqlCommand(@"
                    SELECT Stkcode, SeqImage, Filename, Url, RetryCount
                    FROM   Product_Image
                    WHERE  Stkcode  = @Stkcode
                      AND  SeqImage = @SeqImage", conn))
                {
                    cmd.Parameters.AddWithValue("@Stkcode", stkcode);
                    cmd.Parameters.AddWithValue("@SeqImage", seqImage);

                    using (var r = await cmd.ExecuteReaderAsync())
                    {
                        if (!await r.ReadAsync()) return null;
                        return new ProductImageJob
                        {
                            Stkcode = r.GetString(0),
                            SeqImage = r.GetInt32(1),
                            Filename = r.IsDBNull(2) ? null : r.GetString(2),
                            Url = r.GetString(3),
                            RetryCount = r.GetInt32(4)
                        };
                    }
                }
            }
        }

        private async Task SetStatusAsync(
            string stkcode, int seqImage, string status)
        {
            await ExecAsync(@"
                UPDATE Product_Image
                SET    Status        = @Status,
                       LastAttemptAt = GETDATE()
                WHERE  Stkcode  = @Stkcode
                  AND  SeqImage = @SeqImage",
                new SqlParameter("@Status", status),
                new SqlParameter("@Stkcode", stkcode),
                new SqlParameter("@SeqImage", seqImage));
        }

        private async Task SetSuccessAsync(
            string stkcode, int seqImage,
            string filename, string filePath)
        {
            await ExecAsync(@"
                UPDATE Product_Image
                SET    Status        = 'SUCCESS',
                       Filename      = @Filename,
                       FilePath      = @FilePath,
                       LastAttemptAt = GETDATE(),
                       ErrorMessage  = NULL
                WHERE  Stkcode  = @Stkcode
                  AND  SeqImage = @SeqImage",
                new SqlParameter("@Filename", filename),
                new SqlParameter("@FilePath", filePath),
                new SqlParameter("@Stkcode", stkcode),
                new SqlParameter("@SeqImage", seqImage));
        }

        private async Task SetFailedAsync(
            string stkcode, int seqImage,
            string status, int retryCount, string error)
        {
            await ExecAsync(@"
                UPDATE Product_Image
                SET    Status        = @Status,
                       RetryCount    = @RetryCount,
                       LastAttemptAt = GETDATE(),
                       ErrorMessage  = @Error
                WHERE  Stkcode  = @Stkcode
                  AND  SeqImage = @SeqImage",
                new SqlParameter("@Status", status),
                new SqlParameter("@RetryCount", retryCount),
                new SqlParameter("@Error", (object)error ?? DBNull.Value),
                new SqlParameter("@Stkcode", stkcode),
                new SqlParameter("@SeqImage", seqImage));
        }

        private async Task ExecAsync(string sql, params SqlParameter[] parameters)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddRange(parameters);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }

        private static string SanitizeFolderName(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name.Trim();
        }
    }

    // ── DTOs ──────────────────────────────────────────────────────
    public class ProductImageJob
    {
        public string Stkcode { get; set; }
        public int SeqImage { get; set; }
        public string Filename { get; set; }
        public string Url { get; set; }
        public int RetryCount { get; set; }
    }

    public class SyncKey
    {
        public string Stkcode { get; set; }
        public int SeqImage { get; set; }
    }

    public class SyncResult
    {
        public bool Success { get; private set; }
        public string Message { get; private set; }

        public static SyncResult Ok(string filename)
        {
            return new SyncResult { Success = true, Message = filename };
        }
        public static SyncResult Fail(string reason)
        {
            return new SyncResult { Success = false, Message = reason };
        }
    }

    public class SyncAllResult
    {
        public int SuccessCount { get; set; }
        public int FailCount { get; set; }
    }
}