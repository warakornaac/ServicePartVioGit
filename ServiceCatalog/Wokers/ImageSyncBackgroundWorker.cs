using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Hosting;
using ServiceCatalog.Services;

namespace ServiceCatalog.Workers
{
    public class ImageSyncBackgroundWorker : IRegisteredObject
    {
        private static ImageSyncBackgroundWorker _instance;
        private static readonly object _lock = new object();

        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private readonly ImageSyncService _service;
        private readonly SemaphoreSlim _limiter = new SemaphoreSlim(5, 5);
        private readonly string _connectionString;

        private const int BatchSize = 20;
        private const int PollIntervalMs = 15000;

        private ImageSyncBackgroundWorker(string connectionString)
        {
            _connectionString = connectionString;
            _service = new ImageSyncService(connectionString);
        }

        public static void Start(string connectionString)
        {
            lock (_lock)
            {
                if (_instance != null) return;
                _instance = new ImageSyncBackgroundWorker(connectionString);
                HostingEnvironment.RegisterObject(_instance);
                Task.Run(() => _instance.RunAsync(_instance._cts.Token));
            }
        }

        public void Stop(bool immediate)
        {
            _cts.Cancel();
            HostingEnvironment.UnregisterObject(this);
        }

        // ── Main loop ─────────────────────────────────────────────
        private async Task RunAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    // ดึง QUEUE batch จาก DB โดยตรง (ไม่ผ่าน Service)
                    var batch = await GetQueuedBatchAsync(BatchSize);

                    if (batch.Count > 0)
                    {
                        var tasks = new Task[batch.Count];
                        for (int i = 0; i < batch.Count; i++)
                        {
                            var job = batch[i];
                            tasks[i] = ProcessWithThrottleAsync(job, token);
                        }
                        await Task.WhenAll(tasks);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (SqlException sqlEx)
                {
                    System.Diagnostics.Trace.TraceError(
                        "[ImageSyncWorker] SQL: " + sqlEx.Message);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.TraceError(
                        "[ImageSyncWorker] " + ex.Message);
                }

                try { await Task.Delay(PollIntervalMs, token); }
                catch (OperationCanceledException) { break; }
            }
        }

        private async Task ProcessWithThrottleAsync(
            ProductImageJob job, CancellationToken token)
        {
            await _limiter.WaitAsync(token);
            try { await _service.ProcessItemAsync(job); }
            finally { _limiter.Release(); }
        }

        // ── Query QUEUE items โดยตรงจาก DB ───────────────────────
        private async Task<List<ProductImageJob>> GetQueuedBatchAsync(int batchSize)
        {
            var list = new List<ProductImageJob>();

            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                using (var cmd = new SqlCommand(@"
                    SELECT TOP (@BatchSize)
                           Stkcode, SeqImage, Filename, Url, RetryCount
                    FROM   Product_Image
                    WHERE  Status     = 'QUEUE'
                      AND  RetryCount < 3
                    ORDER BY RetryCount ASC", conn))
                {
                    cmd.Parameters.AddWithValue("@BatchSize", batchSize);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            list.Add(new ProductImageJob
                            {
                                Stkcode = reader.GetString(0),
                                SeqImage = reader.GetInt32(1),
                                Filename = reader.IsDBNull(2)
                                             ? null : reader.GetString(2),
                                Url = reader.GetString(3),
                                RetryCount = reader.GetInt32(4)
                            });
                        }
                    }
                }
            }

            return list;
        }
    }
}