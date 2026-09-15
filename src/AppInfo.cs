using System;
using System.IO;
using System.Text;

namespace TPMSimpleModMaker
{
    internal static class AppInfo
    {
        public const string ProductName = "Memento Maker";
        public const string DisplayVersion = "0.9.9 Beta";
        public const string FileVersion = "0.9.9.0";
        public const string CreatorCredit = "Created by SeanB";
        public const string AiAssistanceCredit = "Developed with assistance from ChatGPT by OpenAI";

        public static string ApplicationDirectory
        {
            get { return AppDomain.CurrentDomain.BaseDirectory; }
        }

        public static string LocalDataRoot
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MementoMaker"); }
        }

        public static string WorkerLogPath
        {
            get { return Path.Combine(LocalDataRoot, "Worker", "unity_worker.log"); }
        }

        public static string LogsFolder
        {
            get { return Path.Combine(LocalDataRoot, "Logs"); }
        }

        public static string InstallerLogsFolder
        {
            get { return Path.Combine(LogsFolder, "Installer"); }
        }

        public static string JobsFolder
        {
            get { return Path.Combine(LocalDataRoot, "Jobs"); }
        }
        public static string BuildDiagnostics(AppSettings settings, string privateEnvironmentPath, string workerStatus)
        {
            StringBuilder text = new StringBuilder();
            text.AppendLine(ProductName + " " + DisplayVersion);
            text.AppendLine(CreatorCredit);
            text.AppendLine(AiAssistanceCredit);
            text.AppendLine("Automation: " + EnvironmentService.AutomationVersion);
            text.AppendLine("Settings format: " + SettingsService.CurrentSettingsFormatVersion);
            text.AppendLine("Project format: " + ProjectLibraryService.CurrentProjectFormatVersion);
            text.AppendLine("Environment marker format: " + EnvironmentService.MarkerFormatVersion);
            text.AppendLine("OS: " + Environment.OSVersion);
            text.AppendLine("64-bit OS: " + Environment.Is64BitOperatingSystem);
            text.AppendLine("64-bit process: " + Environment.Is64BitProcess);
            text.AppendLine(".NET runtime: " + Environment.Version);
            text.AppendLine();
            text.AppendLine("Unity: " + Value(settings == null ? null : settings.UnityExePath));
            text.AppendLine("SDK: " + Value(settings == null ? null : settings.SdkZipPath));
            text.AppendLine("Private environment: " + Value(privateEnvironmentPath));
            text.AppendLine("Output folder: " + Value(settings == null ? null : settings.LastOutputFolder));
            text.AppendLine("Modders Name: " + Value(settings == null ? null : settings.ModdersName));
            text.AppendLine("Persistent Unity worker: " + ((settings == null || settings.KeepUnityWorkerRunning != false) ? "Enabled" : "Disabled"));
            text.AppendLine("Worker status: " + Value(workerStatus));
            text.AppendLine();
            text.AppendLine("Data folder: " + LocalDataRoot);
            text.AppendLine("Worker log: " + WorkerLogPath);
            text.AppendLine("Reliability log: " + Path.Combine(LogsFolder, "reliability.log"));
            text.AppendLine("Logs folder: " + LogsFolder);
            text.AppendLine("Installer logs: " + InstallerLogsFolder);
            return text.ToString().TrimEnd();
        }

        private static string Value(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "Not configured" : value;
        }
    }
}
