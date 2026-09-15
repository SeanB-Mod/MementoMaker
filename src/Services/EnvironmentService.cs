using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace TPMSimpleModMaker
{
    internal sealed class EnvironmentService
    {
        public const string AutomationVersion = "9J-29";
        public const int MarkerFormatVersion = 2;

        private readonly string _appBaseDirectory;
        private readonly string _rootDirectory;
        private readonly string _projectDirectory;
        private readonly string _markerPath;

        public EnvironmentService()
        {
            _appBaseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            _rootDirectory = Path.Combine(AppInfo.LocalDataRoot, "Environment");
            _projectDirectory = Path.Combine(_rootDirectory, "MuseumModding");
            _markerPath = Path.Combine(_rootDirectory, "environment.json");
        }

        public string ProjectDirectory { get { return _projectDirectory; } }
        public string MarkerPath { get { return _markerPath; } }

        public EnvironmentHealthReport Inspect(string sdkZipPath)
        {
            EnvironmentHealthReport report = new EnvironmentHealthReport();
            report.ProjectValid = IsUnityProject(_projectDirectory);
            report.AutomationPresent = report.ProjectValid && HasRequiredAutomation(_projectDirectory);
            report.AutomationCurrent = false;
            report.SdkChanged = false;

            if (!report.ProjectValid)
            {
                report.Status = Directory.Exists(_projectDirectory) ? "Damaged" : "Missing";
                report.Message = Directory.Exists(_projectDirectory)
                    ? "Private environment is incomplete or damaged. Use Check & Repair to recreate it."
                    : "Private environment has not been created yet. Use Check & Repair to prepare it.";
                report.NeedsRepair = true;
                report.NeedsRebuild = true;
                report.IsUsable = false;
                return report;
            }

            EnvironmentMarker marker = ReadMarker();
            string currentFingerprint = ComputeAutomationFingerprint();
            bool installedAutomationComplete = report.AutomationPresent && InstalledAutomationContainsSourceFiles();
            report.AutomationCurrent = installedAutomationComplete && marker != null &&
                marker.MarkerFormatVersion == MarkerFormatVersion &&
                string.Equals(marker.AutomationVersion, AutomationVersion, StringComparison.Ordinal) &&
                !string.IsNullOrEmpty(marker.AutomationFingerprint) &&
                string.Equals(marker.AutomationFingerprint, currentFingerprint, StringComparison.OrdinalIgnoreCase);

            if (!string.IsNullOrEmpty(sdkZipPath) && File.Exists(sdkZipPath))
                report.SdkChanged = HasSdkChanged(marker, sdkZipPath);

            report.IsUsable = report.ProjectValid && report.AutomationPresent;

            // An SDK replacement takes precedence over automation repair. Repairing only
            // Memento Maker files must never rewrite the marker and accidentally accept a
            // changed official SDK without the user's explicit Rebuild Environment action.
            if (report.SdkChanged)
            {
                report.Status = "SdkChanged";
                report.Message = "The selected ModdingProject.zip has changed since this environment was created. Rebuild Environment is recommended before building or publishing.";
                report.NeedsRebuild = true;
                return report;
            }

            if (!report.AutomationPresent)
            {
                report.Status = "RepairRequired";
                report.Message = "Memento Maker automation is missing from the private environment. Memento Maker normally repairs this automatically; Check & Repair can retry without recreating the SDK project.";
                report.NeedsRepair = true;
                return report;
            }

            if (!report.AutomationCurrent)
            {
                report.Status = "AutomationOutdated";
                report.Message = "Memento Maker automation needs to be refreshed. Memento Maker normally refreshes this automatically; Check & Repair can retry without recreating the SDK project.";
                report.NeedsRepair = true;
                return report;
            }

            report.Status = "Healthy";
            report.Message = "Private environment is healthy and ready for builds.";
            report.IsUsable = true;
            return report;
        }

        public void EnsureEnvironment(string sdkZipPath, bool forceRebuild, Action<string> progress)
        {
            if (string.IsNullOrEmpty(sdkZipPath) || !File.Exists(sdkZipPath))
                throw new FileNotFoundException("The official ModdingProject.zip could not be found.", sdkZipPath);

            Directory.CreateDirectory(_rootDirectory);

            if (forceRebuild && Directory.Exists(_projectDirectory))
            {
                Report(progress, "Removing the previous private modding environment...");
                DeleteDirectoryRobust(_projectDirectory);
            }

            if (!IsUnityProject(_projectDirectory))
            {
                Report(progress, "Extracting the official Two Point Museum ModdingProject.zip...");
                ExtractOfficialProject(sdkZipPath, progress);
            }

            Report(progress, "Applying Memento Maker automation...");
            ApplyAutomation();
            WriteMarker(sdkZipPath);
            Report(progress, "Private Unity environment is ready.");
        }

        public void RepairAutomation(string sdkZipPath, Action<string> progress)
        {
            if (!IsUnityProject(_projectDirectory))
                throw new InvalidOperationException("The private Unity project is missing or damaged and must be rebuilt from ModdingProject.zip.");
            if (string.IsNullOrEmpty(sdkZipPath) || !File.Exists(sdkZipPath))
                throw new FileNotFoundException("The official ModdingProject.zip could not be found.", sdkZipPath);

            Report(progress, "Refreshing Memento Maker automation files...");
            ApplyAutomation();
            WriteMarker(sdkZipPath);
            Report(progress, "Automation repair complete. The existing SDK environment was preserved.");
        }

        private EnvironmentMarker ReadMarker()
        {
            if (!File.Exists(_markerPath))
                return null;
            try { return JsonFile.Read<EnvironmentMarker>(_markerPath); }
            catch { return null; }
        }

        private static bool HasSdkChanged(EnvironmentMarker marker, string sdkZipPath)
        {
            if (marker == null || string.IsNullOrEmpty(sdkZipPath) || !File.Exists(sdkZipPath))
                return false;

            try
            {
                FileInfo info = new FileInfo(sdkZipPath);
                return marker.SdkZipLength != info.Length ||
                    !string.Equals(marker.SdkZipLastWriteUtc, info.LastWriteTimeUtc.ToString("o"), StringComparison.Ordinal);
            }
            catch { return false; }
        }

        private void ExtractOfficialProject(string sdkZipPath, Action<string> progress)
        {
            string tempRoot = Path.Combine(_rootDirectory, "Extract_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);

            try
            {
                ZipFile.ExtractToDirectory(sdkZipPath, tempRoot);
                string extractedProject = FindUnityProject(tempRoot);
                if (string.IsNullOrEmpty(extractedProject))
                    throw new InvalidDataException("ModdingProject.zip was extracted, but a valid MuseumModding Unity project was not found inside it.");

                if (Directory.Exists(_projectDirectory))
                    DeleteDirectoryRobust(_projectDirectory);

                Directory.Move(extractedProject, _projectDirectory);
                Report(progress, "Official project extracted to Memento Maker's private environment.");
            }
            finally
            {
                if (Directory.Exists(tempRoot))
                {
                    try { DeleteDirectoryRobust(tempRoot); } catch { }
                }
            }
        }

        private string FindUnityProject(string root)
        {
            if (IsUnityProject(root))
                return root;

            foreach (string directory in Directory.GetDirectories(root))
            {
                if (IsUnityProject(directory))
                    return directory;
            }

            return null;
        }

        private static bool IsUnityProject(string path)
        {
            return Directory.Exists(path) &&
                Directory.Exists(Path.Combine(path, "Assets")) &&
                Directory.Exists(Path.Combine(path, "Packages")) &&
                Directory.Exists(Path.Combine(path, "ProjectSettings"));
        }

        private static bool HasRequiredAutomation(string projectPath)
        {
            return File.Exists(Path.Combine(projectPath, "Assets", "Editor", "TPMSimpleModMaker", "TPMSimpleModMakerBatch.cs")) &&
                File.Exists(Path.Combine(projectPath, "Assets", "Editor", "TPMSimpleModMaker", "TPMSimpleModMakerWorker.cs")) &&
                File.Exists(Path.Combine(projectPath, "Assets", "Code", "Game", "Editor", "Modding", "SteamWorkshop", "TPMSimpleModMakerWorkshop.cs"));
        }

        private bool InstalledAutomationContainsSourceFiles()
        {
            string automationRoot = Path.Combine(_appBaseDirectory, "Automation");
            if (!Directory.Exists(automationRoot) || !Directory.Exists(_projectDirectory))
                return false;

            try
            {
                foreach (string sourceFile in Directory.GetFiles(automationRoot, "*", SearchOption.AllDirectories))
                {
                    // Unity owns importer metadata after the first import. It can also
                    // legitimately reserialize imported assets/materials while a build is
                    // running. Environment health therefore verifies that every packaged
                    // automation file is present, while the marker fingerprint determines
                    // whether this Memento Maker build has newer automation to deploy.
                    if (string.Equals(Path.GetExtension(sourceFile), ".meta", StringComparison.OrdinalIgnoreCase))
                        continue;

                    string relative = sourceFile.Substring(automationRoot.Length)
                        .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    string installedFile = Path.Combine(_projectDirectory, relative);
                    if (!File.Exists(installedFile))
                        return false;
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        private void ApplyAutomation()
        {
            string automationRoot = Path.Combine(_appBaseDirectory, "Automation");
            if (!Directory.Exists(automationRoot))
                throw new DirectoryNotFoundException("The Automation folder is missing beside MementoMaker.exe: " + automationRoot);

            RemoveLegacyWorkshopBridge();
            CopyDirectory(automationRoot, _projectDirectory);
        }

        private string ComputeAutomationFingerprint()
        {
            string automationRoot = Path.Combine(_appBaseDirectory, "Automation");
            if (!Directory.Exists(automationRoot))
                return "missing";

            List<string> files = new List<string>(Directory.GetFiles(automationRoot, "*", SearchOption.AllDirectories));
            files.Sort(StringComparer.OrdinalIgnoreCase);

            using (SHA256 sha = SHA256.Create())
            using (MemoryStream aggregate = new MemoryStream())
            {
                foreach (string file in files)
                {
                    string relative = file.Substring(automationRoot.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Replace('\\', '/');
                    byte[] name = Encoding.UTF8.GetBytes(relative.ToLowerInvariant() + "\n");
                    aggregate.Write(name, 0, name.Length);
                    byte[] data = File.ReadAllBytes(file);
                    aggregate.Write(data, 0, data.Length);
                    aggregate.WriteByte(0);
                }
                aggregate.Position = 0;
                return BitConverter.ToString(sha.ComputeHash(aggregate)).Replace("-", "").ToLowerInvariant();
            }
        }

        private void RemoveLegacyWorkshopBridge()
        {
            string legacyScript = Path.Combine(_projectDirectory, "Assets", "Editor", "TPMSimpleModMaker", "TPMSimpleModMakerWorkshop.cs");
            DeleteFileIfPresent(legacyScript);
            DeleteFileIfPresent(legacyScript + ".meta");
        }

        private static void DeleteFileIfPresent(string path)
        {
            if (!File.Exists(path))
                return;
            try { File.SetAttributes(path, FileAttributes.Normal); } catch { }
            File.Delete(path);
        }

        private void WriteMarker(string sdkZipPath)
        {
            FileInfo info = new FileInfo(sdkZipPath);
            EnvironmentMarker marker = new EnvironmentMarker();
            marker.MarkerFormatVersion = MarkerFormatVersion;
            marker.AutomationVersion = AutomationVersion;
            marker.AutomationFingerprint = ComputeAutomationFingerprint();
            marker.SdkZipPath = sdkZipPath;
            marker.SdkZipLength = info.Length;
            marker.SdkZipLastWriteUtc = info.LastWriteTimeUtc.ToString("o");
            JsonFile.Write(_markerPath, marker);
        }

        private static void CopyDirectory(string source, string destination)
        {
            Directory.CreateDirectory(destination);
            foreach (string file in Directory.GetFiles(source))
            {
                string target = Path.Combine(destination, Path.GetFileName(file));
                File.Copy(file, target, true);
            }
            foreach (string directory in Directory.GetDirectories(source))
            {
                string target = Path.Combine(destination, Path.GetFileName(directory));
                CopyDirectory(directory, target);
            }
        }

        private static void DeleteDirectoryRobust(string directory)
        {
            if (!Directory.Exists(directory))
                return;
            foreach (string file in Directory.GetFiles(directory, "*", SearchOption.AllDirectories))
            {
                try { File.SetAttributes(file, FileAttributes.Normal); } catch { }
            }
            Directory.Delete(directory, true);
        }

        private static void Report(Action<string> progress, string message)
        {
            if (progress != null)
                progress(message);
        }
    }
}
