using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TPMSimpleModMaker
{
    internal sealed class UnityWorkerService : IDisposable
    {
        private readonly object _sync = new object();
        private readonly SemaphoreSlim _commandLock = new SemaphoreSlim(1, 1);
        private readonly string _workerRoot;
        private readonly string _queueDirectory;
        private readonly string _resultsDirectory;
        private readonly string _readyPath;
        private readonly string _heartbeatPath;
        private readonly string _pidPath;
        private readonly string _logPath;

        private Process _process;
        private Task _startTask;
        private string _unityExe;
        private string _projectPath;
        private long _logPosition;
        private bool _licenceWarningReported;
        private bool _disposed;

        public UnityWorkerService()
        {
            _workerRoot = Path.Combine(AppInfo.LocalDataRoot, "Worker");
            _queueDirectory = Path.Combine(_workerRoot, "Queue");
            _resultsDirectory = Path.Combine(_workerRoot, "Results");
            _readyPath = Path.Combine(_workerRoot, "worker.ready.json");
            _heartbeatPath = Path.Combine(_workerRoot, "heartbeat.json");
            _pidPath = Path.Combine(_workerRoot, "worker.pid");
            _logPath = Path.Combine(_workerRoot, "unity_worker.log");
        }

        public bool IsRunning
        {
            get
            {
                lock (_sync)
                    return IsProcessAlive(_process);
            }
        }

        public bool IsReady
        {
            get { return IsRunning && File.Exists(_readyPath); }
        }

        public string LogPath { get { return _logPath; } }

        public Task StartAsync(string unityExe, string projectPath, Action<string> progress, Action<string> logLine)
        {
            if (_disposed)
                throw new ObjectDisposedException("UnityWorkerService");

            lock (_sync)
            {
                if (IsProcessAlive(_process) &&
                    string.Equals(_unityExe, unityExe, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(_projectPath, projectPath, StringComparison.OrdinalIgnoreCase) &&
                    File.Exists(_readyPath))
                {
                    return Task.FromResult(0);
                }

                if (_startTask != null && !_startTask.IsCompleted &&
                    string.Equals(_unityExe, unityExe, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(_projectPath, projectPath, StringComparison.OrdinalIgnoreCase))
                {
                    return _startTask;
                }

                _unityExe = unityExe;
                _projectPath = projectPath;
                _startTask = StartCoreAsync(unityExe, projectPath, progress, logLine);
                return _startTask;
            }
        }

        private async Task StartCoreAsync(string unityExe, string projectPath, Action<string> progress, Action<string> logLine)
        {
            if (string.IsNullOrEmpty(unityExe) || !File.Exists(unityExe))
                throw new FileNotFoundException("Unity 2020.3.47f1 could not be found.", unityExe);
            if (string.IsNullOrEmpty(projectPath) || !Directory.Exists(projectPath))
                throw new DirectoryNotFoundException("The private Unity project could not be found: " + projectPath);

            Process previous;
            lock (_sync)
                previous = _process;
            if (IsProcessAlive(previous))
                await StopProcessAsync(previous, 3000, logLine);

            await CleanupOrphanWorkerAsync(logLine);
            PrepareWorkerFolders();
            DeleteIfExists(_readyPath);
            DeleteIfExists(_heartbeatPath);
            DeleteIfExists(_pidPath);
            DeleteIfExists(_logPath);
            _logPosition = 0;
            _licenceWarningReported = false;

            Report(progress, "Starting private Unity worker in the background...");
            Report(logLine, "Starting persistent private Unity worker...");
            Report(logLine, "Project: " + projectPath);

            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = unityExe;
            startInfo.Arguments =
                "-batchmode -projectPath " + Quote(projectPath) +
                " -executeMethod TPMSimpleModMaker.TPMSimpleModMakerWorker.StartFromCommandLine" +
                " -workerRoot " + Quote(_workerRoot) +
                " -parentPid " + Process.GetCurrentProcess().Id +
                " -logFile " + Quote(_logPath);
            startInfo.UseShellExecute = false;
            startInfo.CreateNoWindow = true;
            startInfo.WorkingDirectory = projectPath;

            Process process = new Process();
            process.StartInfo = startInfo;
            if (!process.Start())
                throw new InvalidOperationException("Unity could not be started for the persistent worker.");

            lock (_sync)
                _process = process;

            try
            {
                File.WriteAllText(_pidPath, process.Id.ToString());
            }
            catch { }

            try
            {
                DateTime deadline = DateTime.UtcNow.AddMinutes(12);
                while (DateTime.UtcNow < deadline)
                {
                    _logPosition = ReadNewLog(_logPosition, logLine);

                    if (process.HasExited)
                        throw new InvalidOperationException(
                            "The private Unity worker stopped during startup (code " + process.ExitCode +
                            "). Open Settings and use Check & Repair.");

                    if (File.Exists(_readyPath))
                    {
                        _logPosition = ReadNewLog(_logPosition, logLine);
                        Report(progress, "Private Unity worker is ready.");
                        Report(logLine, "Private Unity worker is ready and will remain open for this Memento Maker session.");
                        return;
                    }

                    await Task.Delay(400);
                }

                throw new TimeoutException(
                    "Unity is taking longer than expected to finish configuring the private modding environment.");
            }
            catch
            {
                if (IsProcessAlive(process))
                {
                    try { process.Kill(); } catch { }
                }
                lock (_sync)
                {
                    if (object.ReferenceEquals(_process, process))
                        _process = null;
                }
                CleanupReadyMarkers();
                throw;
            }
        }

        public async Task ExecuteCommandAsync(
            string unityExe,
            string projectPath,
            UnityWorkerCommandRequest request,
            Action<string> progress,
            Action<string> logLine)
        {
            if (request == null)
                throw new ArgumentNullException("request");

            await StartAsync(unityExe, projectPath, progress, logLine);
            await _commandLock.WaitAsync();
            try
            {
                if (!IsReady)
                    throw new InvalidOperationException("The private Unity worker is not ready.");

                string commandId = string.IsNullOrEmpty(request.CommandId)
                    ? DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + "_" + Guid.NewGuid().ToString("N").Substring(0, 8)
                    : request.CommandId;
                request.CommandId = commandId;
                request.UtcCreated = DateTime.UtcNow.ToString("o");

                string commandPath = Path.Combine(_queueDirectory, commandId + ".json");
                string tempPath = commandPath + ".tmp";
                string resultPath = Path.Combine(_resultsDirectory, commandId + ".result.json");
                DeleteIfExists(commandPath);
                DeleteIfExists(tempPath);
                DeleteIfExists(resultPath);

                JsonFile.Write(tempPath, request);
                File.Move(tempPath, commandPath);

                Report(progress, "Sending " + FriendlyCommand(request.Command) + " to the running Unity worker...");
                Report(logLine, "[Worker] Queued " + request.Command + " command: " + commandId);

                DateTime deadline = DateTime.UtcNow.AddMinutes(45);
                while (DateTime.UtcNow < deadline)
                {
                    _logPosition = ReadNewLog(_logPosition, logLine);

                    Process process;
                    lock (_sync)
                        process = _process;
                    if (!IsProcessAlive(process))
                        throw new InvalidOperationException("The private Unity worker stopped while processing " + request.Command + ".");

                    if (File.Exists(resultPath))
                    {
                        UnityWorkerCommandResult result = JsonFile.Read<UnityWorkerCommandResult>(resultPath);
                        _logPosition = ReadNewLog(_logPosition, logLine);
                        if (result == null)
                            throw new InvalidDataException("The Unity worker command result could not be parsed: " + resultPath);
                        if (!result.Success)
                            throw new InvalidOperationException(result.Message + Environment.NewLine + "Worker log: " + _logPath);
                        Report(progress, result.Message);
                        return;
                    }

                    await Task.Delay(300);
                }

                throw new TimeoutException("The private Unity worker did not complete " + request.Command + " within 45 minutes.");
            }
            finally
            {
                _commandLock.Release();
            }
        }

        public async Task ShutdownAsync(Action<string> logLine)
        {
            Process process;
            lock (_sync)
                process = _process;

            if (!IsProcessAlive(process))
            {
                CleanupReadyMarkers();
                return;
            }

            try
            {
                Directory.CreateDirectory(_queueDirectory);
                Directory.CreateDirectory(_resultsDirectory);

                string commandId = "shutdown_" + Guid.NewGuid().ToString("N").Substring(0, 8);
                UnityWorkerCommandRequest request = new UnityWorkerCommandRequest();
                request.CommandId = commandId;
                request.Command = "Shutdown";
                request.UtcCreated = DateTime.UtcNow.ToString("o");

                string commandPath = Path.Combine(_queueDirectory, commandId + ".json");
                string tempPath = commandPath + ".tmp";
                JsonFile.Write(tempPath, request);
                File.Move(tempPath, commandPath);
                Report(logLine, "Stopping private Unity worker...");

                DateTime deadline = DateTime.UtcNow.AddSeconds(5);
                while (DateTime.UtcNow < deadline && IsProcessAlive(process))
                {
                    _logPosition = ReadNewLog(_logPosition, logLine);
                    await Task.Delay(200);
                }
            }
            catch (Exception ex)
            {
                Report(logLine, "[Worker] Graceful shutdown warning: " + ex.Message);
            }

            if (IsProcessAlive(process))
                await StopProcessAsync(process, 1000, logLine);

            lock (_sync)
            {
                if (object.ReferenceEquals(_process, process))
                    _process = null;
                _startTask = null;
            }
            CleanupReadyMarkers();
        }

        public async Task RestartAsync(string unityExe, string projectPath, Action<string> progress, Action<string> logLine)
        {
            await ShutdownAsync(logLine);
            await StartAsync(unityExe, projectPath, progress, logLine);
        }

        private async Task CleanupOrphanWorkerAsync(Action<string> logLine)
        {
            if (!File.Exists(_pidPath))
                return;

            int pid;
            if (!int.TryParse(SafeReadAllText(_pidPath), out pid) || pid <= 0)
            {
                CleanupReadyMarkers();
                return;
            }

            Process orphan = null;
            try { orphan = Process.GetProcessById(pid); }
            catch
            {
                CleanupReadyMarkers();
                return;
            }

            if (!IsProcessAlive(orphan))
            {
                CleanupReadyMarkers();
                return;
            }

            bool looksLikeUnity = false;
            try { looksLikeUnity = orphan.ProcessName.IndexOf("Unity", StringComparison.OrdinalIgnoreCase) >= 0; }
            catch { }
            if (!looksLikeUnity)
            {
                Report(logLine, "[Worker] Ignoring stale worker PID because it no longer points to Unity.");
                CleanupReadyMarkers();
                return;
            }

            Report(logLine, "[Worker] A previous private Unity worker is still closing. Waiting briefly before startup...");
            DateTime deadline = DateTime.UtcNow.AddSeconds(8);
            while (DateTime.UtcNow < deadline && IsProcessAlive(orphan))
                await Task.Delay(250);

            if (IsProcessAlive(orphan))
            {
                try
                {
                    Report(logLine, "[Worker] Previous worker did not close after its parent exited; terminating the orphaned Unity process.");
                    orphan.Kill();
                    orphan.WaitForExit(3000);
                }
                catch (Exception ex)
                {
                    Report(logLine, "[Worker] Could not close the orphaned Unity process: " + ex.Message);
                }
            }

            CleanupReadyMarkers();
        }

        private static string SafeReadAllText(string path)
        {
            try { return File.ReadAllText(path).Trim(); }
            catch { return ""; }
        }

        private void PrepareWorkerFolders()
        {
            Directory.CreateDirectory(_workerRoot);
            Directory.CreateDirectory(_queueDirectory);
            Directory.CreateDirectory(_resultsDirectory);

            foreach (string file in Directory.GetFiles(_queueDirectory, "*.json"))
                DeleteIfExists(file);
            foreach (string file in Directory.GetFiles(_queueDirectory, "*.tmp"))
                DeleteIfExists(file);

            try
            {
                DateTime cutoff = DateTime.UtcNow.AddDays(-3);
                foreach (string file in Directory.GetFiles(_resultsDirectory, "*.json"))
                {
                    if (File.GetLastWriteTimeUtc(file) < cutoff)
                        DeleteIfExists(file);
                }
            }
            catch { }
        }

        private long ReadNewLog(long position, Action<string> logLine)
        {
            if (!File.Exists(_logPath))
                return position;

            try
            {
                using (FileStream stream = new FileStream(_logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                {
                    if (position > stream.Length)
                        position = 0;
                    stream.Position = position;

                    using (StreamReader reader = new StreamReader(stream, Encoding.UTF8, true, 4096, true))
                    {
                        string line;
                        while ((line = reader.ReadLine()) != null)
                        {
                            if (IsKnownLicenceNoise(line))
                            {
                                if (!_licenceWarningReported)
                                {
                                    _licenceWarningReported = true;
                                    Report(logLine, "[Unity] Licence refresh warning received; waiting for Unity startup to continue using the local entitlement.");
                                }
                                continue;
                            }

                            if (ShouldShowLogLine(line))
                                Report(logLine, line);
                        }
                    }

                    return stream.Position;
                }
            }
            catch
            {
                return position;
            }
        }

        private static bool IsKnownLicenceNoise(string line)
        {
            if (string.IsNullOrEmpty(line))
                return false;
            string lower = line.ToLowerInvariant();
            return lower.Contains("access token is unavailable; failed to update") ||
                lower.Contains("no ulf license found") ||
                lower.Contains("token not found in cache");
        }

        private static bool ShouldShowLogLine(string line)
        {
            if (string.IsNullOrEmpty(line))
                return false;

            string lower = line.ToLowerInvariant();
            return line.IndexOf("[Memento Worker]", StringComparison.OrdinalIgnoreCase) >= 0 ||
                line.IndexOf("[Memento Workshop]", StringComparison.OrdinalIgnoreCase) >= 0 ||
                line.IndexOf("[TPM Simple Mod Maker]", StringComparison.OrdinalIgnoreCase) >= 0 ||
                lower.Contains("exception") || lower.Contains("error") ||
                lower.Contains("addressables") && lower.Contains("build") ||
                lower.Contains("build completed") || lower.Contains("packing completed");
        }

        private static string FriendlyCommand(string command)
        {
            if (string.Equals(command, "Build", StringComparison.OrdinalIgnoreCase))
                return "the build";
            if (string.Equals(command, "BuildBatch", StringComparison.OrdinalIgnoreCase))
                return "the rebuild queue";
            if (string.Equals(command, "BuildFamily", StringComparison.OrdinalIgnoreCase))
                return "the combined variant-family build";
            if (string.Equals(command, "WorkshopQuery", StringComparison.OrdinalIgnoreCase))
                return "the Workshop check";
            if (string.Equals(command, "WorkshopPublish", StringComparison.OrdinalIgnoreCase))
                return "the Workshop publish/update";
            return command ?? "the command";
        }

        private static async Task StopProcessAsync(Process process, int waitMilliseconds, Action<string> logLine)
        {
            if (!IsProcessAlive(process))
                return;

            DateTime deadline = DateTime.UtcNow.AddMilliseconds(waitMilliseconds);
            while (DateTime.UtcNow < deadline && IsProcessAlive(process))
                await Task.Delay(100);

            if (IsProcessAlive(process))
            {
                try
                {
                    Report(logLine, "[Worker] Unity did not close in time; terminating the private worker process.");
                    process.Kill();
                    process.WaitForExit(3000);
                }
                catch { }
            }
        }

        private static bool IsProcessAlive(Process process)
        {
            if (process == null)
                return false;
            try { return !process.HasExited; }
            catch { return false; }
        }

        private void CleanupReadyMarkers()
        {
            DeleteIfExists(_readyPath);
            DeleteIfExists(_heartbeatPath);
            DeleteIfExists(_pidPath);
        }

        private static void DeleteIfExists(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return;
            try { File.SetAttributes(path, FileAttributes.Normal); } catch { }
            try { File.Delete(path); } catch { }
        }

        private static string Quote(string value)
        {
            return WindowsCommandLine.QuoteArgument(value);
        }

        private static void Report(Action<string> action, string message)
        {
            if (action != null)
                action(message);
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;

            Process process;
            lock (_sync)
                process = _process;
            if (IsProcessAlive(process))
            {
                try { process.Kill(); } catch { }
            }
            CleanupReadyMarkers();
            _commandLock.Dispose();
        }
    }
}
