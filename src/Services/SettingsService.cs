using System;
using System.Collections.Generic;
using System.IO;

namespace TPMSimpleModMaker
{
    internal sealed class SettingsService
    {
        public const int CurrentSettingsFormatVersion = 2;
        private readonly string _settingsPath;
        private readonly bool _hadExistingSettings;
        private string _loadWarning;

        public bool HadExistingSettings { get { return _hadExistingSettings; } }
        public string SettingsPath { get { return _settingsPath; } }
        public string LoadWarning { get { return _loadWarning; } }

        public SettingsService()
        {
            string root = AppInfo.LocalDataRoot;
            Directory.CreateDirectory(root);
            _settingsPath = Path.Combine(root, "settings.json");
            _hadExistingSettings = File.Exists(_settingsPath) || File.Exists(_settingsPath + ".bak");
        }

        public static string GetGameModsFolder()
        {
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string localLow = Path.GetFullPath(Path.Combine(local, "..", "LocalLow"));
            return Path.Combine(localLow, "Two Point Studios", "Two Point Museum", "Mods");
        }

        public AppSettings Load()
        {
            AppSettings settings = null;
            _loadWarning = null;
            try
            {
                settings = JsonFile.Read<AppSettings>(_settingsPath);
                string recoveryNotice = JsonFile.ConsumeRecoveryNotice(_settingsPath);
                if (!string.IsNullOrWhiteSpace(recoveryNotice))
                {
                    _loadWarning = "Memento Maker recovered settings.json from its previous backup. " +
                        "Please check Settings to confirm your paths and preferences are still correct.";
                    JsonFile.LogReliability("SETTINGS RECOVERY: " + recoveryNotice);
                }
            }
            catch (Exception ex)
            {
                List<string> quarantined = JsonFile.QuarantineFileAndBackup(_settingsPath, "settings");
                settings = new AppSettings();
                _loadWarning = "Memento Maker could not read settings.json or its backup, so safe default settings have been loaded. " +
                    "The damaged files were preserved for troubleshooting. Please open Settings and check your paths before building.";
                JsonFile.LogReliability("SETTINGS RECOVERY: settings.json and backup were unreadable. Defaults loaded. " + ex +
                    (quarantined.Count == 0 ? "" : " Quarantined: " + string.Join("; ", quarantined.ToArray())));
            }

            if (settings == null)
                settings = new AppSettings();

            if (settings.SettingsFormatVersion <= 0)
                settings.SettingsFormatVersion = CurrentSettingsFormatVersion;

            // The persistent Unity worker is recommended and enabled by default.
            // Nullable storage lets existing settings.json files (which have no value yet)
            // inherit the new default without treating a missing field as false.
            if (!settings.KeepUnityWorkerRunning.HasValue)
                settings.KeepUnityWorkerRunning = true;

            if (string.IsNullOrEmpty(settings.LastOutputFolder))
                settings.LastOutputFolder = GetGameModsFolder();

            if (settings.ModdersName == null)
                settings.ModdersName = "";

            if (string.IsNullOrWhiteSpace(settings.WorkshopFamilyPreviewStyle))
                settings.WorkshopFamilyPreviewStyle = "Simple Grid";

            return settings;
        }

        public void Save(AppSettings settings)
        {
            if (settings == null)
                throw new ArgumentNullException("settings");
            settings.SettingsFormatVersion = CurrentSettingsFormatVersion;
            JsonFile.Write(_settingsPath, settings);
        }
    }
}
