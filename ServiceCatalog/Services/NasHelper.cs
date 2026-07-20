using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Principal;

namespace ServiceCatalog.Services
{
    public class NasConnection : IDisposable
    {
        // ── Win32 API ──────────────────────────────────────────────
        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool LogonUser(
            string lpszUsername,
            string lpszDomain,
            string lpszPassword,
            int dwLogonType,
            int dwLogonProvider,
            out IntPtr phToken);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        private static extern bool CloseHandle(IntPtr handle);

        [DllImport("mpr.dll")]
        private static extern int WNetCancelConnection2(
            string name, int flags, bool force);

        private const int LOGON32_LOGON_NEW_CREDENTIALS = 9;
        private const int LOGON32_PROVIDER_WINNT50 = 3;

        private WindowsImpersonationContext _impersonationContext;
        private IntPtr _tokenHandle = IntPtr.Zero;

        public NasConnection(string uncPath, string username,
                             string password, string domain = "")
        {
            // ── Force disconnect session เก่า ──
            try { WNetCancelConnection2(uncPath, 0, true); }
            catch { /* ignore */ }

            // ── Logon ด้วย NAS credential ──
            bool ok = LogonUser(
                username,
                string.IsNullOrEmpty(domain) ? "." : domain,
                password,
                LOGON32_LOGON_NEW_CREDENTIALS,
                LOGON32_PROVIDER_WINNT50,
                out _tokenHandle);

            if (!ok)
            {
                int err = Marshal.GetLastWin32Error();
                throw new Win32Exception(err,
                    string.Format("LogonUser ล้มเหลว (error {0})", err));
            }

            // ── Impersonate ──
            var identity = new WindowsIdentity(_tokenHandle);
            _impersonationContext = identity.Impersonate();

            System.Diagnostics.Trace.TraceInformation(
                "[NAS] Impersonating as: " + username);
        }

        public static void ForceDisconnect(string uncPath)
        {
            try { WNetCancelConnection2(uncPath, 0, true); }
            catch { /* ignore */ }
        }

        public void Dispose()
        {
            try
            {
                _impersonationContext?.Undo();
                _impersonationContext = null;
            }
            catch { }

            if (_tokenHandle != IntPtr.Zero)
            {
                CloseHandle(_tokenHandle);
                _tokenHandle = IntPtr.Zero;
            }
        }
    }
}