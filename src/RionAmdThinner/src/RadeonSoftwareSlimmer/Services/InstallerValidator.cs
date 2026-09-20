using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using RadeonSoftwareSlimmer.Models;
using RadeonSoftwareSlimmer.ViewModels;

namespace RadeonSoftwareSlimmer.Services
{
    /// <summary>
    /// Read-only checks on a candidate installer: SHA-256, Authenticode trust (WinVerifyTrust),
    /// and whether the signing publisher is AMD. Never modifies the file.
    /// </summary>
    public sealed class InstallerValidator
    {
        public DriverInstallerInfo Inspect(string filePath, string downloadSource = null, DateTime? downloadedAt = null)
        {
            var info = new DriverInstallerInfo
            {
                FilePath = filePath,
                DownloadSource = downloadSource,
                DownloadedAt = downloadedAt,
            };

            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                info.SignatureStatus = SignatureStatus.Error;
                return info;
            }

            info.FileSizeBytes = new FileInfo(filePath).Length;
            info.OriginalHashSha256 = Sha256(filePath);
            InspectSignature(filePath, info);
            InferVersionFromName(filePath, info);

            info.IsOfficialAmdSource =
                IsOfficialAmdUrl(downloadSource)
                || info.SignatureStatus == SignatureStatus.Valid && IsAmdPublisher(info.Publisher);

            return info;
        }

        internal static bool IsOfficialAmdUrl(string source) =>
            Uri.TryCreate(source, UriKind.Absolute, out var uri)
            && uri.Scheme == Uri.UriSchemeHttps
            && string.IsNullOrEmpty(uri.UserInfo)
            && (uri.IdnHost.Equals("amd.com", StringComparison.OrdinalIgnoreCase)
                || uri.IdnHost.EndsWith(".amd.com", StringComparison.OrdinalIgnoreCase));

        public static string Sha256(string filePath)
        {
            using (var sha = SHA256.Create())
            using (var fs = File.OpenRead(filePath))
            {
                return BitConverter.ToString(sha.ComputeHash(fs)).Replace("-", "").ToLowerInvariant();
            }
        }

        private static void InspectSignature(string filePath, DriverInstallerInfo info)
        {
            try
            {
                using var cert = X509Certificate.CreateFromSignedFile(filePath);
                using var cert2 = new X509Certificate2(cert);
                info.Publisher = cert2.GetNameInfo(X509NameType.SimpleName, false);

                int trust = WinVerifyTrust(filePath);
                if (trust == 0)
                    info.SignatureStatus = IsAmdPublisher(info.Publisher)
                        ? SignatureStatus.Valid
                        : SignatureStatus.ValidButNotAmd;
                else
                    info.SignatureStatus = SignatureStatus.Invalid;
            }
            catch (CryptographicException)
            {
                info.SignatureStatus = SignatureStatus.Unsigned;
            }
            catch (Exception ex)
            {
                StaticViewModel.AddDebugMessage(ex, "Signature check failed for " + filePath);
                info.SignatureStatus = SignatureStatus.Error;
            }
        }

        // Exact certificate simple names shared by inspection and every execution gate.
        // Current AMD packages use the unsuffixed name; do not use substring matching.
        internal static bool IsAmdPublisher(string publisher) =>
            string.Equals(publisher, "Advanced Micro Devices", StringComparison.OrdinalIgnoreCase)
            || string.Equals(publisher, "Advanced Micro Devices, Inc.", StringComparison.OrdinalIgnoreCase)
            || string.Equals(publisher, "Advanced Micro Devices Inc.", StringComparison.OrdinalIgnoreCase)
            || string.Equals(publisher, "ATI Technologies Inc.", StringComparison.OrdinalIgnoreCase);

        private static void InferVersionFromName(string filePath, DriverInstallerInfo info)
        {
            string name = Path.GetFileNameWithoutExtension(filePath);
            var m = System.Text.RegularExpressions.Regex.Match(name, @"(\d{2}\.\d{1,2}\.\d{1,2})");
            if (m.Success) info.PackageVersion = m.Value;
        }

        // ---- WinVerifyTrust P/Invoke -------------------------------------------------

        private static readonly Guid WINTRUST_ACTION_GENERIC_VERIFY_V2 =
            new Guid("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");

        [DllImport("wintrust.dll", ExactSpelling = true, SetLastError = false, CharSet = CharSet.Unicode)]
        private static extern int WinVerifyTrust(IntPtr hwnd, ref Guid pgActionID, IntPtr pWVTData);

        private static int WinVerifyTrust(string fileName)
        {
            const uint WTD_UI_NONE = 2;
            const uint WTD_REVOKE_NONE = 0;
            const uint WTD_CHOICE_FILE = 1;
            const uint WTD_STATEACTION_VERIFY = 1;
            const uint WTD_STATEACTION_CLOSE = 2;

            var fileInfo = new WINTRUST_FILE_INFO
            {
                cbStruct = (uint)Marshal.SizeOf<WINTRUST_FILE_INFO>(),
                pcwszFilePath = fileName,
            };
            IntPtr pFile = Marshal.AllocHGlobal((int)fileInfo.cbStruct);
            Marshal.StructureToPtr(fileInfo, pFile, false);

            var data = new WINTRUST_DATA
            {
                cbStruct = (uint)Marshal.SizeOf<WINTRUST_DATA>(),
                dwUIChoice = WTD_UI_NONE,
                fdwRevocationChecks = WTD_REVOKE_NONE,
                dwUnionChoice = WTD_CHOICE_FILE,
                pFile = pFile,
                dwStateAction = WTD_STATEACTION_VERIFY,
            };
            IntPtr pData = Marshal.AllocHGlobal((int)data.cbStruct);
            Marshal.StructureToPtr(data, pData, false);

            Guid action = WINTRUST_ACTION_GENERIC_VERIFY_V2;
            int result;
            try
            {
                result = WinVerifyTrust(IntPtr.Zero, ref action, pData);

            }
            finally
            {
                try
                {
                    // VERIFY writes the state handle into native memory. Preserve that handle
                    // for CLOSE instead of overwriting it with the initial managed zero value.
                    data = Marshal.PtrToStructure<WINTRUST_DATA>(pData);
                    data.dwStateAction = WTD_STATEACTION_CLOSE;
                    Marshal.StructureToPtr(data, pData, false);
                    WinVerifyTrust(IntPtr.Zero, ref action, pData);
                }
                finally
                {
                    Marshal.DestroyStructure<WINTRUST_FILE_INFO>(pFile);
                    Marshal.FreeHGlobal(pFile);
                    Marshal.FreeHGlobal(pData);
                }
            }
            return result;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WINTRUST_FILE_INFO
        {
            public uint cbStruct;
            public string pcwszFilePath;
            public IntPtr hFile;
            public IntPtr pgKnownSubject;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WINTRUST_DATA
        {
            public uint cbStruct;
            public IntPtr pPolicyCallbackData;
            public IntPtr pSIPClientData;
            public uint dwUIChoice;
            public uint fdwRevocationChecks;
            public uint dwUnionChoice;
            public IntPtr pFile;
            public uint dwStateAction;
            public IntPtr hWVTStateData;
            public IntPtr pwszURLReference;
            public uint dwProvFlags;
            public uint dwUIContext;
        }
    }
}
