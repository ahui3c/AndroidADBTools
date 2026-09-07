using System;
using System.Collections.Generic;

namespace AndroidADBTools
{
    public sealed class InstalledAppInfo
    {
        public string DisplayName { get; set; }
        public string PackageName { get; set; }
        public string ApkPath { get; set; }
        public string InstallerPackage { get; set; }
        public string Status { get; set; }
        public bool StatusIsError { get; set; }
        public bool IsUninstallBlocked { get; set; }
        public string RestrictionReason { get; set; }

        public InstalledAppInfo()
        {
            DisplayName = "";
            PackageName = "";
            ApkPath = "";
            InstallerPackage = "";
            Status = "已安裝";
            RestrictionReason = "";
        }
    }

    public static class InstalledAppParser
    {
        public static List<InstalledAppInfo> Parse(string output)
        {
            Dictionary<string, InstalledAppInfo> found =
                new Dictionary<string, InstalledAppInfo>(StringComparer.OrdinalIgnoreCase);
            string[] lines = (output ?? "").Replace("\r", "").Split('\n');
            foreach (string rawLine in lines)
            {
                string line = (rawLine ?? "").Trim();
                if (!line.StartsWith("package:", StringComparison.OrdinalIgnoreCase)) continue;

                string value = line.Substring(8).Trim();
                string installer = "";
                int installerIndex = value.IndexOf(" installer=", StringComparison.OrdinalIgnoreCase);
                if (installerIndex >= 0)
                {
                    installer = value.Substring(installerIndex + 11).Trim();
                    int extraFieldIndex = installer.IndexOf(' ');
                    if (extraFieldIndex >= 0) installer = installer.Substring(0, extraFieldIndex);
                    value = value.Substring(0, installerIndex).Trim();
                }

                int separator = value.LastIndexOf('=');
                if (separator <= 0 || separator >= value.Length - 1) continue;
                string apkPath = value.Substring(0, separator).Trim();
                string packageName = value.Substring(separator + 1).Trim();
                if (!IsValidPackageName(packageName)) continue;
                if (String.Equals(installer, "null", StringComparison.OrdinalIgnoreCase)) installer = "";

                found[packageName] = new InstalledAppInfo
                {
                    PackageName = packageName,
                    ApkPath = apkPath,
                    InstallerPackage = installer
                };
            }

            List<InstalledAppInfo> result = new List<InstalledAppInfo>(found.Values);
            result.Sort(delegate(InstalledAppInfo left, InstalledAppInfo right)
            {
                return StringComparer.OrdinalIgnoreCase.Compare(left.PackageName, right.PackageName);
            });
            return result;
        }

        public static bool IsValidPackageName(string packageName)
        {
            if (String.IsNullOrWhiteSpace(packageName) || packageName.Length > 255) return false;
            if (packageName.IndexOf('.') <= 0 || packageName.EndsWith(".", StringComparison.Ordinal)) return false;
            foreach (char character in packageName)
            {
                if (!(Char.IsLetterOrDigit(character) || character == '.' || character == '_')) return false;
            }
            return true;
        }
    }
}
