using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace TPMSimpleModMaker
{
    internal static class BetaSupportService
    {
        public static string LogsFolder
        {
            get { return AppInfo.LogsFolder; }
        }

        public static string SupportBundlesFolder
        {
            get { return Path.Combine(AppInfo.LocalDataRoot, "SupportBundles"); }
        }

        public static string WriteCrashReport(Exception exception, string origin)
        {
            Directory.CreateDirectory(LogsFolder);
            string path = Path.Combine(LogsFolder, "crash_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".txt");
            StringBuilder text = new StringBuilder();
            text.AppendLine("Memento Maker crash report");
            text.AppendLine("=========================");
            text.AppendLine("Created: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss zzz"));
            text.AppendLine("Origin: " + (string.IsNullOrEmpty(origin) ? "Unknown" : origin));
            text.AppendLine();
            text.AppendLine(AppInfo.ProductName + " " + AppInfo.DisplayVersion);
            text.AppendLine(AppInfo.CreatorCredit);
            text.AppendLine(AppInfo.AiAssistanceCredit);
            text.AppendLine("OS: " + Environment.OSVersion);
            text.AppendLine(".NET runtime: " + Environment.Version);
            text.AppendLine();
            text.AppendLine(exception == null ? "No exception details were supplied." : exception.ToString());
            File.WriteAllText(path, text.ToString(), Encoding.UTF8);
            return path;
        }

        public static void CreateSupportBundle(
            string destinationZip,
            AppSettings settings,
            string privateEnvironmentPath,
            string workerStatus,
            string buildLog)
        {
            if (string.IsNullOrWhiteSpace(destinationZip))
                throw new ArgumentException("A destination ZIP path is required.", "destinationZip");

            string parent = Path.GetDirectoryName(destinationZip);
            if (!string.IsNullOrEmpty(parent))
                Directory.CreateDirectory(parent);
            if (File.Exists(destinationZip))
                File.Delete(destinationZip);

            string temp = Path.Combine(Path.GetTempPath(), "MementoMakerSupport_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temp);
            try
            {
                File.WriteAllText(Path.Combine(temp, "diagnostics.txt"),
                    AppInfo.BuildDiagnostics(settings, privateEnvironmentPath, workerStatus) + Environment.NewLine,
                    Encoding.UTF8);

                File.WriteAllText(Path.Combine(temp, "build_log.txt"),
                    string.IsNullOrEmpty(buildLog) ? "Build Log was empty when the bundle was created." : buildLog,
                    Encoding.UTF8);

                StringBuilder readme = new StringBuilder();
                readme.AppendLine("MEMENTO MAKER BETA SUPPORT BUNDLE");
                readme.AppendLine("=================================");
                readme.AppendLine();
                readme.AppendLine("Created by SeanB");
                readme.AppendLine("Developed with assistance from ChatGPT by OpenAI");
                readme.AppendLine();
                readme.AppendLine("This bundle is intended for troubleshooting Memento Maker issues.");
                readme.AppendLine("It does NOT include source artwork, generated textures or built mod content.");
                readme.AppendLine("It may contain local file paths, mod names/IDs and Steam Workshop item IDs.");
                readme.AppendLine("Review the bundle before sharing it if you prefer.");
                File.WriteAllText(Path.Combine(temp, "README.txt"), readme.ToString(), Encoding.UTF8);

                CopyIfExists(AppInfo.WorkerLogPath, Path.Combine(temp, "unity_worker.log"));
                CopyIfExists(Path.Combine(AppInfo.LogsFolder, "reliability.log"), Path.Combine(temp, "reliability.log"));
                CopyIfExists(Path.Combine(AppInfo.LocalDataRoot, "settings.json"), Path.Combine(temp, "settings.json"));
                CopyIfExists(Path.Combine(AppInfo.LocalDataRoot, "Environment", "environment.json"), Path.Combine(temp, "environment.json"));

                string crashFolder = LogsFolder;
                if (Directory.Exists(crashFolder))
                {
                    string[] crashes = Directory.GetFiles(crashFolder, "crash_*.txt");
                    Array.Sort(crashes, StringComparer.OrdinalIgnoreCase);
                    int start = Math.Max(0, crashes.Length - 3);
                    for (int i = start; i < crashes.Length; i++)
                        CopyIfExists(crashes[i], Path.Combine(temp, Path.GetFileName(crashes[i])));
                }

                File.WriteAllText(Path.Combine(temp, "project_summary.txt"), BuildProjectSummary(), Encoding.UTF8);

                ZipFile.CreateFromDirectory(temp, destinationZip, CompressionLevel.Optimal, false);
            }
            finally
            {
                try { if (Directory.Exists(temp)) Directory.Delete(temp, true); }
                catch { }
            }
        }

        private static string BuildProjectSummary()
        {
            StringBuilder text = new StringBuilder();
            text.AppendLine("Memento Maker project summary");
            text.AppendLine("=============================");
            try
            {
                ProjectLibraryService library = new ProjectLibraryService();
                List<ModProjectRecord> projects = library.LoadAll();
                text.AppendLine("Project count: " + projects.Count);
                text.AppendLine();
                foreach (ModProjectRecord project in projects)
                {
                    if (project == null) continue;
                    text.AppendLine("Name: " + Safe(project.Name));
                    text.AppendLine("Item Mod ID: " + Safe(project.ModId));
                    text.AppendLine("Template: " + Safe(project.Template));
                    text.AppendLine("Last built: " + Safe(project.LastBuiltUtc));
                    text.AppendLine("Last edited: " + Safe(project.LastEditedUtc));
                    text.AppendLine("Pending changes: " + project.PendingChanges);
                    text.AppendLine("Workshop ID: " + Safe(project.WorkshopPublishedFileId));
                    text.AppendLine("Workshop link state: " + Safe(project.WorkshopLinkState));
                    text.AppendLine();
                }
            }
            catch (Exception ex)
            {
                text.AppendLine("Project summary could not be created: " + ex.Message);
            }
            return text.ToString();
        }

        private static string Safe(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "(none)" : value;
        }

        private static void CopyIfExists(string source, string destination)
        {
            try
            {
                if (File.Exists(source))
                    File.Copy(source, destination, true);
            }
            catch { }
        }
    }
}
