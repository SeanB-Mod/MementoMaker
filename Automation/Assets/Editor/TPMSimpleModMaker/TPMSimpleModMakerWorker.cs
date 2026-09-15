using System;
using DiagnosticsProcess = System.Diagnostics.Process;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace TPMSimpleModMaker
{
    /// <summary>
    /// Persistent private Unity worker for Memento Maker Step 9F.
    /// Unity is started once, watches a file-backed command queue and remains alive
    /// until Memento Maker exits or persistent mode is disabled.
    /// </summary>
    [InitializeOnLoad]
    public static class TPMSimpleModMakerWorker
    {
        [Serializable]
        private sealed class WorkerCommand
        {
            public string commandId = "";
            public string command = "";
            public string jobPath = "";
            public string batchJobPath = "";
            public string resultPath = "";
            public string workshopJobPath = "";
            public string utcCreated = "";
        }

        [Serializable]
        private sealed class WorkerCommandResult
        {
            public bool success;
            public string commandId = "";
            public string message = "";
            public string utcCompleted = "";
        }

        [Serializable]
        private sealed class BatchManifest
        {
            public string[] jobPaths = new string[0];
        }

        [Serializable]
        private sealed class WorkerStatus
        {
            public int processId;
            public int parentProcessId;
            public string utc = "";
            public string state = "";
        }

        private static string _workerRoot;
        private static string _queueDirectory;
        private static string _resultsDirectory;
        private static string _readyPath;
        private static string _heartbeatPath;
        private static int _parentProcessId;
        private static bool _busy;
        private static bool _shutdownRequested;
        private static bool _initialised;
        private static double _nextHeartbeat;
        private static double _nextParentCheck;
        private static double _nextQueueCheck;

        static TPMSimpleModMakerWorker()
        {
            // If Unity performs a domain reload while the private worker is alive,
            // the original -workerRoot command-line argument is still present.
            // Reattach the queue/heartbeat callbacks automatically instead of
            // silently losing the persistent session.
            string workerRoot = GetCommandLineValue("-workerRoot");
            if (!string.IsNullOrWhiteSpace(workerRoot))
                EditorApplication.delayCall += InitialiseFromCommandLine;
        }

        public static void StartFromCommandLine()
        {
            InitialiseFromCommandLine();
        }

        private static void InitialiseFromCommandLine()
        {
            if (_initialised)
                return;

            _workerRoot = GetCommandLineValue("-workerRoot");
            if (string.IsNullOrWhiteSpace(_workerRoot))
                throw new ArgumentException("Missing required -workerRoot argument.");

            int.TryParse(GetCommandLineValue("-parentPid"), out _parentProcessId);
            _workerRoot = Path.GetFullPath(_workerRoot);
            _queueDirectory = Path.Combine(_workerRoot, "Queue");
            _resultsDirectory = Path.Combine(_workerRoot, "Results");
            _readyPath = Path.Combine(_workerRoot, "worker.ready.json");
            _heartbeatPath = Path.Combine(_workerRoot, "heartbeat.json");

            Directory.CreateDirectory(_workerRoot);
            Directory.CreateDirectory(_queueDirectory);
            Directory.CreateDirectory(_resultsDirectory);

            _busy = false;
            _shutdownRequested = false;
            _initialised = true;
            _nextHeartbeat = 0;
            _nextParentCheck = 0;
            _nextQueueCheck = 0;

            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;
            AssemblyReloadEvents.beforeAssemblyReload -= BeforeAssemblyReload;
            AssemblyReloadEvents.beforeAssemblyReload += BeforeAssemblyReload;

            WriteStatus(_readyPath, "ready");
            WriteStatus(_heartbeatPath, "ready");
            Debug.Log("[Memento Worker] READY. Persistent Unity worker is waiting for Memento Maker commands.");
        }

        private static void OnEditorUpdate()
        {
            if (_shutdownRequested)
                return;

            double now = EditorApplication.timeSinceStartup;
            if (now >= _nextHeartbeat)
            {
                _nextHeartbeat = now + 5.0;
                WriteStatus(_heartbeatPath, _busy ? "busy" : "ready");
            }

            if (now >= _nextParentCheck)
            {
                _nextParentCheck = now + 5.0;
                if (_parentProcessId > 0 && !ParentProcessIsAlive(_parentProcessId))
                {
                    Debug.Log("[Memento Worker] Parent Memento Maker process is no longer running. Shutting down.");
                    RequestShutdown();
                    return;
                }
            }

            if (_busy)
                return;

            // Keep the persistent editor lightweight while idle. Polling the file
            // system every editor frame is unnecessary; four checks per second is
            // responsive for the UI without creating constant disk churn.
            if (now < _nextQueueCheck)
                return;
            _nextQueueCheck = now + 0.25;

            string commandPath = GetNextCommandPath();
            if (string.IsNullOrEmpty(commandPath))
                return;

            _busy = true;
            try
            {
                ProcessCommand(commandPath);
            }
            finally
            {
                _busy = false;
                WriteStatus(_heartbeatPath, "ready");
            }
        }

        private static string GetNextCommandPath()
        {
            try
            {
                if (!Directory.Exists(_queueDirectory))
                    return null;

                return Directory.GetFiles(_queueDirectory, "*.json")
                    .OrderBy(path => File.GetCreationTimeUtc(path))
                    .FirstOrDefault();
            }
            catch
            {
                return null;
            }
        }

        private static void ProcessCommand(string commandPath)
        {
            WorkerCommand command = null;
            string commandId = Path.GetFileNameWithoutExtension(commandPath);
            try
            {
                command = JsonUtility.FromJson<WorkerCommand>(File.ReadAllText(commandPath));
                if (command == null)
                    throw new InvalidDataException("The worker command JSON could not be parsed.");
                if (!string.IsNullOrWhiteSpace(command.commandId))
                    commandId = command.commandId;

                Debug.Log("[Memento Worker] BEGIN " + command.command + " [" + commandId + "]");

                if (string.Equals(command.command, "Build", StringComparison.OrdinalIgnoreCase))
                {
                    TPMSimpleModMakerBatch.BuildToResultFile(command.jobPath);
                }
                else if (string.Equals(command.command, "BuildBatch", StringComparison.OrdinalIgnoreCase))
                {
                    ProcessBatch(command.batchJobPath);
                }
                else if (string.Equals(command.command, "BuildFamily", StringComparison.OrdinalIgnoreCase))
                {
                    TPMSimpleModMakerBatch.BuildFamilyToResultFile(command.batchJobPath);
                }
                else if (string.Equals(command.command, "WorkshopQuery", StringComparison.OrdinalIgnoreCase))
                {
                    InvokeWorkshopQuery(command.resultPath);
                }
                else if (string.Equals(command.command, "WorkshopPublish", StringComparison.OrdinalIgnoreCase))
                {
                    InvokeWorkshopPublish(command.workshopJobPath, command.resultPath);
                }
                else if (string.Equals(command.command, "Shutdown", StringComparison.OrdinalIgnoreCase))
                {
                    WriteCommandResult(commandId, true, "Private Unity worker shutdown requested.");
                    TryDelete(commandPath);
                    Debug.Log("[Memento Worker] Shutdown requested by Memento Maker.");
                    RequestShutdown();
                    return;
                }
                else
                {
                    throw new InvalidOperationException("Unknown Memento Maker worker command: " + command.command);
                }

                WriteCommandResult(commandId, true, FriendlyCompletion(command.command));
                Debug.Log("[Memento Worker] END " + command.command + " [" + commandId + "]");
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                WriteCommandResult(commandId, false, ex.ToString());
                Debug.LogError("[Memento Worker] FAILED " + (command == null ? "command" : command.command) + " [" + commandId + "]: " + ex.Message);
            }
            finally
            {
                TryDelete(commandPath);

                bool touchedAssets = command != null &&
                    (string.Equals(command.command, "Build", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(command.command, "BuildBatch", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(command.command, "BuildFamily", StringComparison.OrdinalIgnoreCase));
                if (touchedAssets)
                {
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                }
            }
        }

        private static void ProcessBatch(string manifestPath)
        {
            if (string.IsNullOrWhiteSpace(manifestPath))
                throw new ArgumentException("The BuildBatch command did not include batchJobPath.");
            manifestPath = Path.GetFullPath(manifestPath);
            if (!File.Exists(manifestPath))
                throw new FileNotFoundException("Batch build manifest was not found.", manifestPath);

            BatchManifest manifest = JsonUtility.FromJson<BatchManifest>(File.ReadAllText(manifestPath));
            if (manifest == null || manifest.jobPaths == null || manifest.jobPaths.Length == 0)
                throw new InvalidDataException("The batch build manifest contains no jobs: " + manifestPath);

            int total = manifest.jobPaths.Length;
            Debug.Log("[TPM Simple Mod Maker] Persistent-worker batch queue started with " + total + " job(s).");
            for (int i = 0; i < total; i++)
            {
                string jobPath = manifest.jobPaths[i];
                string marker = "[TPM Simple Mod Maker] [QUEUE " + (i + 1) + "/" + total + "] ";
                Debug.Log(marker + "BEGIN: " + jobPath);

                // Unity's Scriptable Build Pipeline can finish the Addressables build successfully
                // while a background BuildCache save briefly reports a sharing violation. Capture
                // that specific, known non-fatal warning so we can give the cache extra time to
                // release its handles before starting the next queued mod.
                int buildCacheSharingViolationSeen = 0;
                Application.LogCallback cacheWatch = delegate(string condition, string stackTrace, LogType type)
                {
                    string combined = (condition ?? string.Empty) + "\n" + (stackTrace ?? string.Empty);
                    if (combined.IndexOf("Sharing violation on path", StringComparison.OrdinalIgnoreCase) >= 0 &&
                        combined.IndexOf("\\Library\\BuildCache\\", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        System.Threading.Interlocked.Exchange(ref buildCacheSharingViolationSeen, 1);
                    }
                };

                Application.logMessageReceivedThreaded += cacheWatch;
                try
                {
                    TPMSimpleModMakerBatch.BuildToResultFile(jobPath);
                }
                finally
                {
                    Application.logMessageReceivedThreaded -= cacheWatch;
                }

                Debug.Log(marker + "COMPLETE: " + jobPath);
                SettleBuildPipeline(marker, System.Threading.Volatile.Read(ref buildCacheSharingViolationSeen) != 0);
            }
            Debug.Log("[TPM Simple Mod Maker] Persistent-worker batch queue complete.");
        }

        private static void SettleBuildPipeline(string marker, bool buildCacheSharingViolationSeen)
        {
            // A synchronous refresh plus a short cooldown makes repeated Addressables builds much
            // safer inside one long-lived Unity process. The extra delay is only used when the
            // Scriptable Build Pipeline actually reports the known BuildCache sharing violation.
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            WaitForEditorIdle();
            GC.Collect();
            GC.WaitForPendingFinalizers();

            int cooldownMs = buildCacheSharingViolationSeen ? 2500 : 1000;
            if (buildCacheSharingViolationSeen)
            {
                Debug.LogWarning(marker +
                    "BuildCache sharing violation was reported, but the build completed. " +
                    "Applying an extended " + cooldownMs + " ms settle before the next job.");
            }

            System.Threading.Thread.Sleep(cooldownMs);

            if (buildCacheSharingViolationSeen)
            {
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                WaitForEditorIdle();
            }
        }

        private static void WaitForEditorIdle()
        {
            DateTime deadline = DateTime.UtcNow.AddSeconds(15);
            while ((EditorApplication.isCompiling || EditorApplication.isUpdating) && DateTime.UtcNow < deadline)
                System.Threading.Thread.Sleep(50);
        }

        private static void InvokeWorkshopQuery(string resultPath)
        {
            if (string.IsNullOrWhiteSpace(resultPath))
                throw new ArgumentException("The WorkshopQuery command did not include resultPath.");

            Type workshopType = null;
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length && workshopType == null; i++)
                workshopType = assemblies[i].GetType("TPMSimpleModMaker.TPMSimpleModMakerWorkshop", false);

            if (workshopType == null)
                throw new TypeLoadException("Memento Maker could not find TPMSimpleModMakerWorkshop in the loaded Unity assemblies.");

            MethodInfo method = workshopType.GetMethod(
                "QueryPublishedItemsForWorker",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(string) },
                null);
            if (method == null)
                throw new MissingMethodException("TPMSimpleModMakerWorkshop.QueryPublishedItemsForWorker(string) was not found.");

            method.Invoke(null, new object[] { Path.GetFullPath(resultPath) });
        }

        private static void InvokeWorkshopPublish(string jobPath, string resultPath)
        {
            if (string.IsNullOrWhiteSpace(jobPath))
                throw new ArgumentException("The WorkshopPublish command did not include workshopJobPath.");
            if (string.IsNullOrWhiteSpace(resultPath))
                throw new ArgumentException("The WorkshopPublish command did not include resultPath.");

            Type workshopType = null;
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length && workshopType == null; i++)
                workshopType = assemblies[i].GetType("TPMSimpleModMaker.TPMSimpleModMakerWorkshop", false);

            if (workshopType == null)
                throw new TypeLoadException("Memento Maker could not find TPMSimpleModMakerWorkshop in the loaded Unity assemblies.");

            MethodInfo method = workshopType.GetMethod(
                "PublishOrUpdateForWorker",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(string), typeof(string) },
                null);
            if (method == null)
                throw new MissingMethodException("TPMSimpleModMakerWorkshop.PublishOrUpdateForWorker(string,string) was not found.");

            method.Invoke(null, new object[] { Path.GetFullPath(jobPath), Path.GetFullPath(resultPath) });
        }

        private static void WriteCommandResult(string commandId, bool success, string message)
        {
            WorkerCommandResult result = new WorkerCommandResult();
            result.success = success;
            result.commandId = commandId ?? "";
            result.message = message ?? "";
            result.utcCompleted = DateTime.UtcNow.ToString("o");

            string path = Path.Combine(_resultsDirectory, (commandId ?? "unknown") + ".result.json");
            string tempPath = path + ".tmp";
            File.WriteAllText(tempPath, JsonUtility.ToJson(result, true));
            TryDelete(path);
            File.Move(tempPath, path);
        }

        private static string FriendlyCompletion(string command)
        {
            if (string.Equals(command, "Build", StringComparison.OrdinalIgnoreCase))
                return "Unity build command completed.";
            if (string.Equals(command, "BuildBatch", StringComparison.OrdinalIgnoreCase))
                return "Unity rebuild queue completed.";
            if (string.Equals(command, "BuildFamily", StringComparison.OrdinalIgnoreCase))
                return "Unity combined variant-family build completed.";
            if (string.Equals(command, "WorkshopQuery", StringComparison.OrdinalIgnoreCase))
                return "Steam Workshop query command completed.";
            if (string.Equals(command, "WorkshopPublish", StringComparison.OrdinalIgnoreCase))
                return "Steam Workshop publish/update command completed.";
            return "Unity worker command completed.";
        }

        private static void RequestShutdown()
        {
            if (_shutdownRequested)
                return;
            _shutdownRequested = true;
            EditorApplication.update -= OnEditorUpdate;
            TryDelete(_readyPath);
            TryDelete(_heartbeatPath);
            EditorApplication.delayCall += delegate { EditorApplication.Exit(0); };
        }

        private static void BeforeAssemblyReload()
        {
            if (!string.IsNullOrEmpty(_readyPath))
                TryDelete(_readyPath);
        }

        private static bool ParentProcessIsAlive(int processId)
        {
            try
            {
                DiagnosticsProcess process = DiagnosticsProcess.GetProcessById(processId);
                return process != null && !process.HasExited;
            }
            catch
            {
                return false;
            }
        }

        private static void WriteStatus(string path, string state)
        {
            try
            {
                WorkerStatus status = new WorkerStatus();
                status.processId = DiagnosticsProcess.GetCurrentProcess().Id;
                status.parentProcessId = _parentProcessId;
                status.utc = DateTime.UtcNow.ToString("o");
                status.state = state ?? "";
                File.WriteAllText(path, JsonUtility.ToJson(status, true));
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Memento Worker] Could not update worker status file: " + ex.Message);
            }
        }

        private static string GetCommandLineValue(string key)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], key, StringComparison.OrdinalIgnoreCase))
                    return args[i + 1];
            }
            return null;
        }

        private static void TryDelete(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return;
            try { File.Delete(path); } catch { }
        }
    }
}
