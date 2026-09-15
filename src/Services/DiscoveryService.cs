using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;

namespace TPMSimpleModMaker
{
    internal sealed class DiscoveryService
    {
        public const string RequiredUnityVersion = "2020.3.47f1";
        public const string ModdingSdkAppId = "3457760";

        public string FindUnity(string savedPath)
        {
            if (IsUnityPathValid(savedPath))
                return Path.GetFullPath(savedPath);

            List<string> candidates = new List<string>();
            string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            if (!string.IsNullOrEmpty(programFiles))
                candidates.Add(Path.Combine(programFiles, "Unity", "Hub", "Editor", RequiredUnityVersion, "Editor", "Unity.exe"));

            string programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            if (!string.IsNullOrEmpty(programFilesX86))
                candidates.Add(Path.Combine(programFilesX86, "Unity", "Hub", "Editor", RequiredUnityVersion, "Editor", "Unity.exe"));

            foreach (string candidate in candidates)
            {
                if (IsUnityPathValid(candidate))
                    return Path.GetFullPath(candidate);
            }

            // Unity Hub folders occasionally carry a suffix. Search only the expected Hub editor root.
            string hubRoot = Path.Combine(programFiles, "Unity", "Hub", "Editor");
            if (Directory.Exists(hubRoot))
            {
                try
                {
                    foreach (string directory in Directory.GetDirectories(hubRoot, RequiredUnityVersion + "*"))
                    {
                        string candidate = Path.Combine(directory, "Editor", "Unity.exe");
                        if (IsUnityPathValid(candidate))
                            return Path.GetFullPath(candidate);
                    }
                }
                catch
                {
                }
            }

            return null;
        }

        public string FindUnityHub()
        {
            List<string> candidates = new List<string>();
            string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            if (!string.IsNullOrEmpty(programFiles))
                candidates.Add(Path.Combine(programFiles, "Unity Hub", "Unity Hub.exe"));
            if (!string.IsNullOrEmpty(programFilesX86))
                candidates.Add(Path.Combine(programFilesX86, "Unity Hub", "Unity Hub.exe"));
            if (!string.IsNullOrEmpty(localAppData))
                candidates.Add(Path.Combine(localAppData, "Programs", "Unity Hub", "Unity Hub.exe"));

            foreach (string candidate in candidates)
            {
                if (IsUnityHubPathValid(candidate))
                    return Path.GetFullPath(candidate);
            }

            string found = FindUnityHubFromUninstall(Registry.CurrentUser, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
            if (!string.IsNullOrEmpty(found)) return found;
            found = FindUnityHubFromUninstall(Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
            if (!string.IsNullOrEmpty(found)) return found;
            found = FindUnityHubFromUninstall(Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall");
            return found;
        }

        public bool IsUnityHubPathValid(string path)
        {
            return !string.IsNullOrEmpty(path) && File.Exists(path) &&
                string.Equals(Path.GetFileName(path), "Unity Hub.exe", StringComparison.OrdinalIgnoreCase);
        }

        private string FindUnityHubFromUninstall(RegistryKey root, string subKeyPath)
        {
            try
            {
                using (RegistryKey uninstall = root.OpenSubKey(subKeyPath))
                {
                    if (uninstall == null) return null;
                    foreach (string name in uninstall.GetSubKeyNames())
                    {
                        using (RegistryKey app = uninstall.OpenSubKey(name))
                        {
                            if (app == null) continue;
                            string displayName = Convert.ToString(app.GetValue("DisplayName"));
                            if (string.IsNullOrEmpty(displayName) || displayName.IndexOf("Unity Hub", StringComparison.OrdinalIgnoreCase) < 0)
                                continue;

                            string installLocation = Convert.ToString(app.GetValue("InstallLocation"));
                            if (!string.IsNullOrEmpty(installLocation))
                            {
                                string candidate = Path.Combine(installLocation.Trim('"'), "Unity Hub.exe");
                                if (IsUnityHubPathValid(candidate)) return Path.GetFullPath(candidate);
                            }

                            string displayIcon = Convert.ToString(app.GetValue("DisplayIcon"));
                            if (!string.IsNullOrEmpty(displayIcon))
                            {
                                string candidate = displayIcon.Trim().Trim('"');
                                int comma = candidate.IndexOf(',');
                                if (comma > 0) candidate = candidate.Substring(0, comma).Trim().Trim('"');
                                if (IsUnityHubPathValid(candidate)) return Path.GetFullPath(candidate);
                            }
                        }
                    }
                }
            }
            catch { }
            return null;
        }

        public string FindSdkZip(string savedPath)
        {
            if (IsSdkZipValid(savedPath))
                return Path.GetFullPath(savedPath);

            HashSet<string> checkedLibraries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string steamRoot in GetSteamRoots())
            {
                foreach (string libraryRoot in GetSteamLibraries(steamRoot))
                {
                    checkedLibraries.Add(libraryRoot);
                    string manifest = Path.Combine(libraryRoot, "steamapps", "appmanifest_" + ModdingSdkAppId + ".acf");
                    if (!File.Exists(manifest))
                        continue;

                    string installDir = ParseAcfValue(File.ReadAllText(manifest), "installdir");
                    if (string.IsNullOrEmpty(installDir))
                        continue;

                    string installRoot = Path.Combine(libraryRoot, "steamapps", "common", installDir);
                    string directZip = Path.Combine(installRoot, "ModdingProject.zip");
                    if (IsSdkZipValid(directZip))
                        return Path.GetFullPath(directZip);

                    try
                    {
                        string[] found = Directory.GetFiles(installRoot, "ModdingProject.zip", SearchOption.AllDirectories);
                        if (found.Length > 0 && IsSdkZipValid(found[0]))
                            return Path.GetFullPath(found[0]);
                    }
                    catch
                    {
                    }
                }
            }

            // Fallback for SDK installs whose Steam app manifest cannot be identified: scan only
            // the first level of each Steam common folder for the official ModdingProject.zip.
            foreach (string libraryRoot in checkedLibraries)
            {
                string commonRoot = Path.Combine(libraryRoot, "steamapps", "common");
                if (!Directory.Exists(commonRoot))
                    continue;
                try
                {
                    foreach (string installRoot in Directory.GetDirectories(commonRoot))
                    {
                        string candidate = Path.Combine(installRoot, "ModdingProject.zip");
                        if (IsSdkZipValid(candidate))
                            return Path.GetFullPath(candidate);
                    }
                }
                catch
                {
                }
            }

            return null;
        }

        public bool IsUnityPathValid(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path) ||
                !string.Equals(Path.GetFileName(path), "Unity.exe", StringComparison.OrdinalIgnoreCase))
                return false;

            try
            {
                FileVersionInfo version = FileVersionInfo.GetVersionInfo(path);
                string productVersion = version == null ? null : version.ProductVersion;
                if (!string.IsNullOrEmpty(productVersion) &&
                    productVersion.StartsWith(RequiredUnityVersion, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            catch { }

            // Fallback for Unity installations whose Windows version resource is unavailable.
            string normalised = path.Replace('/', '\\');
            return normalised.IndexOf("\\" + RequiredUnityVersion + "\\Editor\\Unity.exe", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public bool IsSdkZipValid(string path)
        {
            return !string.IsNullOrEmpty(path) && File.Exists(path) &&
                string.Equals(Path.GetFileName(path), "ModdingProject.zip", StringComparison.OrdinalIgnoreCase);
        }

        private IEnumerable<string> GetSteamRoots()
        {
            HashSet<string> roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            AddIfDirectory(roots, ReadRegistryString(Registry.CurrentUser, @"Software\Valve\Steam", "SteamPath"));
            AddIfDirectory(roots, ReadRegistryString(Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath"));
            AddIfDirectory(roots, ReadRegistryString(Registry.LocalMachine, @"SOFTWARE\Valve\Steam", "InstallPath"));

            string programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            AddIfDirectory(roots, Path.Combine(programFilesX86, "Steam"));

            return roots;
        }

        private IEnumerable<string> GetSteamLibraries(string steamRoot)
        {
            HashSet<string> libraries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            AddIfDirectory(libraries, steamRoot);

            string vdfPath = Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf");
            if (File.Exists(vdfPath))
            {
                try
                {
                    string text = File.ReadAllText(vdfPath);
                    MatchCollection matches = Regex.Matches(text, "\\\"path\\\"\\s*\\\"([^\\\"]+)\\\"", RegexOptions.IgnoreCase);
                    foreach (Match match in matches)
                    {
                        string path = match.Groups[1].Value.Replace("\\\\", "\\");
                        AddIfDirectory(libraries, path);
                    }
                }
                catch
                {
                }
            }

            return libraries;
        }

        private static string ParseAcfValue(string text, string key)
        {
            Match match = Regex.Match(text, "\\\"" + Regex.Escape(key) + "\\\"\\s*\\\"([^\\\"]*)\\\"", RegexOptions.IgnoreCase);
            return match.Success ? match.Groups[1].Value : null;
        }

        private static string ReadRegistryString(RegistryKey root, string subKey, string valueName)
        {
            try
            {
                using (RegistryKey key = root.OpenSubKey(subKey))
                {
                    if (key == null)
                        return null;
                    object value = key.GetValue(valueName);
                    return value == null ? null : value.ToString();
                }
            }
            catch
            {
                return null;
            }
        }

        private static void AddIfDirectory(HashSet<string> set, string path)
        {
            if (string.IsNullOrEmpty(path))
                return;

            try
            {
                string full = Path.GetFullPath(path.Replace('/', Path.DirectorySeparatorChar));
                if (Directory.Exists(full))
                    set.Add(full);
            }
            catch
            {
            }
        }
    }
}
