using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;

namespace TPMSimpleModMaker
{
    internal static class JsonFile
    {
        private static readonly object RecoveryNoticeSync = new object();
        private static readonly Dictionary<string, string> RecoveryNotices = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public static T Read<T>(string path) where T : class
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("A JSON file path is required.", "path");

            string backupPath = path + ".bak";
            if (!File.Exists(path))
            {
                if (!File.Exists(backupPath))
                    return null;

                try
                {
                    T backupOnly = ReadCore<T>(backupPath);
                    TryRestoreBackup(path, backupPath, false);
                    RecordRecoveryNotice(path,
                        "Recovered missing JSON file from backup: " + path);
                    LogReliability("JSON RECOVERY: primary file was missing; backup was loaded for " + path);
                    return backupOnly;
                }
                catch (Exception backupException)
                {
                    LogReliability("JSON READ ERROR: primary file missing and backup unreadable for " + path + ". " + backupException);
                    throw new InvalidDataException(
                        "The JSON file is missing and its backup could not be read: " + path,
                        backupException);
                }
            }

            try
            {
                return ReadCore<T>(path);
            }
            catch (Exception primaryException)
            {
                LogReliability("JSON READ ERROR: primary file could not be read: " + path + ". " + primaryException);

                if (!File.Exists(backupPath))
                    throw;

                try
                {
                    T recovered = ReadCore<T>(backupPath);
                    TryRestoreBackup(path, backupPath, true);
                    RecordRecoveryNotice(path,
                        "Recovered damaged JSON file from backup: " + path);
                    LogReliability("JSON RECOVERY: backup was loaded successfully for " + path);
                    return recovered;
                }
                catch (Exception backupException)
                {
                    LogReliability("JSON READ ERROR: backup file could not be read: " + backupPath + ". " + backupException);
                    throw new InvalidDataException(
                        "The JSON file and its backup could not be read: " + path,
                        new AggregateException(primaryException, backupException));
                }
            }
        }

        public static void Write<T>(string path, T value)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("A JSON file path is required.", "path");

            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            string tempPath = path + ".tmp." + Guid.NewGuid().ToString("N");
            string backupPath = path + ".bak";

            try
            {
                using (FileStream stream = new FileStream(
                    tempPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    4096,
                    FileOptions.WriteThrough))
                {
                    DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(T));
                    serializer.WriteObject(stream, value);
                    stream.Flush(true);
                }

                if (File.Exists(path))
                {
                    // File.Replace is atomic on the supported Windows/NTFS target and also
                    // preserves the previous valid generation as path.bak. Never fall back to
                    // FileMode.Create/direct overwrite here: if replacement fails, keeping the
                    // old valid primary is safer than risking a truncated file.
                    TryDelete(backupPath);
                    File.Replace(tempPath, path, backupPath, true);
                }
                else
                {
                    File.Move(tempPath, path);
                    // Give newly-created JSON files an immediate recovery generation too.
                    // If this best-effort copy fails the primary is still complete and valid.
                    try { File.Copy(path, backupPath, true); }
                    catch (Exception backupException)
                    {
                        LogReliability("JSON BACKUP WARNING: initial backup could not be created for " + path + ". " + backupException);
                    }
                }
            }
            catch (Exception ex)
            {
                LogReliability("JSON WRITE ERROR: atomic write failed for " + path + ". " + ex);
                throw;
            }
            finally
            {
                TryDelete(tempPath);
            }
        }

        public static void DeleteWithBackup(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("A file path is required.", "path");

            string backupPath = path + ".bak";
            try
            {
                // Delete the recovery generation first. If this fails, leave the primary
                // untouched so a logical delete can never be silently undone by Read<T>()
                // restoring the .bak file on the next access.
                if (File.Exists(backupPath))
                    File.Delete(backupPath);

                if (File.Exists(path))
                    File.Delete(path);

                if (File.Exists(path) || File.Exists(backupPath))
                    throw new IOException("The file or its recovery backup could not be removed completely: " + path);
            }
            catch (Exception ex)
            {
                LogReliability("JSON DELETE ERROR: logical delete failed for " + path + ". " + ex);
                throw;
            }
        }

        public static void SynchroniseBackup(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("A file path is required.", "path");
            if (!File.Exists(path))
                throw new FileNotFoundException("The primary JSON file could not be found.", path);

            string backupPath = path + ".bak";
            string tempPath = backupPath + ".sync." + Guid.NewGuid().ToString("N");
            try
            {
                using (FileStream input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (FileStream output = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
                {
                    input.CopyTo(output);
                    output.Flush(true);
                }

                if (File.Exists(backupPath))
                    File.Replace(tempPath, backupPath, null, true);
                else
                    File.Move(tempPath, backupPath);
            }
            catch (Exception ex)
            {
                LogReliability("JSON BACKUP SYNC ERROR: could not synchronise recovery backup for " + path + ". " + ex);
                throw;
            }
            finally
            {
                TryDelete(tempPath);
            }
        }

        public static string ConsumeRecoveryNotice(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return null;

            string key = NormalisePathKey(path);
            lock (RecoveryNoticeSync)
            {
                string notice;
                if (!RecoveryNotices.TryGetValue(key, out notice))
                    return null;
                RecoveryNotices.Remove(key);
                return notice;
            }
        }

        public static void LogReliability(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return;

            try
            {
                Directory.CreateDirectory(AppInfo.LogsFolder);
                string path = Path.Combine(AppInfo.LogsFolder, "reliability.log");
                string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff zzz") + "  " + message + Environment.NewLine;
                File.AppendAllText(path, line, Encoding.UTF8);
            }
            catch
            {
                // Reliability logging must never become another failure path.
            }
        }

        public static List<string> QuarantineFileAndBackup(string path, string label)
        {
            List<string> quarantined = new List<string>();
            if (string.IsNullOrWhiteSpace(path))
                return quarantined;

            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
            MoveToQuarantine(path, label, stamp, quarantined);
            MoveToQuarantine(path + ".bak", label + "_backup", stamp, quarantined);
            return quarantined;
        }

        private static T ReadCore<T>(string path) where T : class
        {
            byte[] data = File.ReadAllBytes(path);
            int offset = 0;
            if (data.Length >= 3 && data[0] == 0xEF && data[1] == 0xBB && data[2] == 0xBF)
                offset = 3;

            using (MemoryStream stream = new MemoryStream(data, offset, data.Length - offset, false))
            {
                DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(T));
                return serializer.ReadObject(stream) as T;
            }
        }

        private static void TryRestoreBackup(string primaryPath, string backupPath, bool preserveCorruptPrimary)
        {
            string tempPath = primaryPath + ".restore." + Guid.NewGuid().ToString("N");
            string corruptPath = primaryPath + ".corrupt_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
            try
            {
                File.Copy(backupPath, tempPath, true);
                if (File.Exists(primaryPath))
                {
                    File.Replace(tempPath, primaryPath, preserveCorruptPrimary ? corruptPath : null, true);
                }
                else
                {
                    File.Move(tempPath, primaryPath);
                }
            }
            catch (Exception ex)
            {
                // Recovery has already succeeded in memory. A failed on-disk repair should be
                // logged, but it should not stop the application from using the valid backup.
                LogReliability("JSON RECOVERY WARNING: backup loaded, but primary could not be restored for " + primaryPath + ". " + ex);
            }
            finally
            {
                TryDelete(tempPath);
            }
        }

        private static void RecordRecoveryNotice(string path, string message)
        {
            string key = NormalisePathKey(path);
            lock (RecoveryNoticeSync)
                RecoveryNotices[key] = message;
        }

        private static string NormalisePathKey(string path)
        {
            try { return Path.GetFullPath(path ?? ""); }
            catch { return path ?? ""; }
        }

        private static void MoveToQuarantine(string sourcePath, string label, string stamp, List<string> quarantined)
        {
            if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
                return;

            string directory = Path.GetDirectoryName(sourcePath) ?? "";
            string extension = Path.GetExtension(sourcePath);
            string safeLabel = string.IsNullOrWhiteSpace(label) ? "json" : label;
            string target = Path.Combine(directory, safeLabel + ".corrupt_" + stamp + extension);
            int suffix = 1;
            while (File.Exists(target))
            {
                target = Path.Combine(directory, safeLabel + ".corrupt_" + stamp + "_" + suffix + extension);
                suffix++;
            }

            try
            {
                File.Move(sourcePath, target);
                quarantined.Add(target);
                LogReliability("JSON QUARANTINE: moved unreadable file to " + target);
            }
            catch (Exception ex)
            {
                LogReliability("JSON QUARANTINE WARNING: could not move " + sourcePath + ". " + ex);
            }
        }

        private static void TryDelete(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return;
            try { File.Delete(path); }
            catch { }
        }
    }
}
