using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace TPMSimpleModMaker
{
    internal sealed class BuildService
    {
        private static readonly TimeSpan OneShotUnityTimeout = TimeSpan.FromMinutes(45);

        private sealed class BatchLogState
        {
            public string Prefix = "[Unity] ";
        }

        public async Task<BuildResultFile> BuildAsync(
            string unityExe,
            string projectPath,
            BuildJob job,
            Action<string> progress,
            Action<string> logLine,
            UnityWorkerService worker)
        {
            string jobsRoot = Path.Combine(AppInfo.LocalDataRoot, "Jobs");
            Directory.CreateDirectory(jobsRoot);

            string jobFolder = Path.Combine(jobsRoot, DateTime.Now.ToString("yyyyMMdd_HHmmss_fff"));
            Directory.CreateDirectory(jobFolder);

            string jobPath = Path.Combine(jobFolder, "build_job.json");
            string resultPath = jobPath + ".result.json";
            string logPath = Path.Combine(jobFolder, "unity.log");
            JsonFile.Write(jobPath, job);

            Report(logLine, "Job: " + jobPath);
            Report(logLine, "Project: " + projectPath);

            if (worker != null)
            {
                Report(progress, "Waiting for the private Unity worker...");
                UnityWorkerCommandRequest request = new UnityWorkerCommandRequest();
                request.Command = "Build";
                request.JobPath = jobPath;
                await worker.ExecuteCommandAsync(unityExe, projectPath, request, progress, logLine);
            }
            else
            {
                Report(progress, "Starting Unity 2020.3.47f1 in batch mode...");
                ProcessStartInfo startInfo = CreateUnityStartInfo(
                    unityExe,
                    projectPath,
                    "-executeMethod TPMSimpleModMaker.TPMSimpleModMakerBatch.BuildFromCommandLine" +
                    " -jobPath " + Quote(jobPath) +
                    " -logFile " + Quote(logPath));

                Process process = StartUnity(startInfo);
                DateTime startedUtc = DateTime.UtcNow;
                long logPosition = 0;
                while (!process.HasExited)
                {
                    logPosition = ReadNewLog(logPath, logPosition, logLine);
                    if (DateTime.UtcNow - startedUtc >= OneShotUnityTimeout)
                    {
                        Report(logLine, "UNITY TIMEOUT: one-shot build exceeded 45 minutes. Terminating Unity.");
                        TerminateTimedOutProcess(process);
                        ReadNewLog(logPath, logPosition, logLine);
                        throw CreateUnityTimeoutException("build", logPath);
                    }
                    await Task.Delay(500);
                }

                process.WaitForExit();
                ReadNewLog(logPath, logPosition, logLine);

                if (!File.Exists(resultPath))
                {
                    throw new InvalidOperationException(
                        "Unity exited with code " + process.ExitCode + " but did not create a result JSON. Full Unity log: " + logPath);
                }
            }

            if (!File.Exists(resultPath))
            {
                throw new InvalidOperationException(
                    "The private Unity worker completed the build command but did not create a result JSON. Worker log: " +
                    (worker == null ? logPath : worker.LogPath));
            }

            BuildResultFile result = JsonFile.Read<BuildResultFile>(resultPath);
            if (result == null)
                throw new InvalidDataException("The Unity result JSON could not be parsed: " + resultPath);

            if (!result.Success)
                throw new InvalidOperationException(result.Message + Environment.NewLine +
                    (worker == null ? "Unity log: " + logPath : "Worker log: " + worker.LogPath));

            Report(progress, "Mod built successfully.");
            Report(logLine, "Output: " + result.OutputPath);
            return result;
        }

        public async Task<List<BatchBuildOutcome>> BuildBatchAsync(
            string unityExe,
            string projectPath,
            IList<BuildJob> jobs,
            Action<string> progress,
            Action<string> logLine,
            UnityWorkerService worker)
        {
            if (jobs == null || jobs.Count == 0)
                return new List<BatchBuildOutcome>();

            string jobsRoot = Path.Combine(AppInfo.LocalDataRoot, "Jobs");
            Directory.CreateDirectory(jobsRoot);

            string batchFolder = Path.Combine(
                jobsRoot,
                "Batch_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + "_" + Guid.NewGuid().ToString("N").Substring(0, 6));
            Directory.CreateDirectory(batchFolder);

            BatchBuildManifest manifest = new BatchBuildManifest();
            manifest.JobPaths = new List<string>();

            for (int i = 0; i < jobs.Count; i++)
            {
                string jobFolder = Path.Combine(batchFolder, (i + 1).ToString("000"));
                Directory.CreateDirectory(jobFolder);
                string jobPath = Path.Combine(jobFolder, "build_job.json");
                JsonFile.Write(jobPath, jobs[i]);
                manifest.JobPaths.Add(jobPath);
                Report(logLine, "[" + (i + 1) + "/" + jobs.Count + "] Job: " + jobPath);
            }

            string manifestPath = Path.Combine(batchFolder, "batch_jobs.json");
            string logPath = Path.Combine(batchFolder, "unity_batch.log");
            JsonFile.Write(manifestPath, manifest);

            Report(logLine, "Batch manifest: " + manifestPath);
            Report(logLine, "Project: " + projectPath);

            Process process = null;
            if (worker != null)
            {
                Report(progress, "Sending " + jobs.Count + " queued mods to the running Unity worker...");
                Report(logLine, "Unity worker will remain open after the rebuild queue completes.");
                UnityWorkerCommandRequest request = new UnityWorkerCommandRequest();
                request.Command = "BuildBatch";
                request.BatchJobPath = manifestPath;
                BatchLogState workerState = new BatchLogState();
                await worker.ExecuteCommandAsync(
                    unityExe,
                    projectPath,
                    request,
                    progress,
                    delegate(string line)
                    {
                        UpdateBatchLogState(line, workerState, progress);
                        Report(logLine, workerState.Prefix + line);
                    });
            }
            else
            {
                Report(progress, "Starting one Unity batch session for " + jobs.Count + " queued mods...");
                Report(logLine, "Unity will remain open for the whole rebuild queue.");

                ProcessStartInfo startInfo = CreateUnityStartInfo(
                    unityExe,
                    projectPath,
                    "-executeMethod TPMSimpleModMaker.TPMSimpleModMakerBatch.BuildBatchFromCommandLine" +
                    " -batchJobPath " + Quote(manifestPath) +
                    " -logFile " + Quote(logPath));

                process = StartUnity(startInfo);
                DateTime startedUtc = DateTime.UtcNow;
                BatchLogState state = new BatchLogState();
                long logPosition = 0;
                while (!process.HasExited)
                {
                    logPosition = ReadNewBatchLog(logPath, logPosition, state, progress, logLine);
                    if (DateTime.UtcNow - startedUtc >= OneShotUnityTimeout)
                    {
                        Report(logLine, "UNITY TIMEOUT: one-shot rebuild queue exceeded 45 minutes. Terminating Unity.");
                        TerminateTimedOutProcess(process);
                        ReadNewBatchLog(logPath, logPosition, state, progress, logLine);
                        throw CreateUnityTimeoutException("rebuild queue", logPath);
                    }
                    await Task.Delay(500);
                }

                process.WaitForExit();
                ReadNewBatchLog(logPath, logPosition, state, progress, logLine);
            }

            List<BatchBuildOutcome> outcomes = new List<BatchBuildOutcome>();
            int resultFilesFound = 0;

            for (int i = 0; i < manifest.JobPaths.Count; i++)
            {
                string jobPath = manifest.JobPaths[i];
                string resultPath = jobPath + ".result.json";
                BatchBuildOutcome outcome = new BatchBuildOutcome();
                outcome.JobPath = jobPath;

                if (!File.Exists(resultPath))
                {
                    outcome.ErrorMessage = worker == null
                        ? "Unity did not create a result JSON for this queued job. Batch Unity log: " + logPath
                        : "The persistent Unity worker did not create a result JSON for this queued job. Worker log: " + worker.LogPath;
                }
                else
                {
                    resultFilesFound++;
                    try
                    {
                        outcome.Result = JsonFile.Read<BuildResultFile>(resultPath);
                        if (outcome.Result == null)
                            outcome.ErrorMessage = "The Unity result JSON could not be parsed: " + resultPath;
                        else if (!outcome.Result.Success)
                            outcome.ErrorMessage = outcome.Result.Message;
                    }
                    catch (Exception ex)
                    {
                        outcome.ErrorMessage = "Could not read Unity result JSON: " + ex.Message;
                    }
                }

                outcomes.Add(outcome);
            }

            if (resultFilesFound == 0)
            {
                if (worker != null)
                {
                    throw new InvalidOperationException(
                        "The persistent Unity worker completed the rebuild queue without producing any result files. Worker log: " + worker.LogPath);
                }
                if (process != null && process.ExitCode != 0)
                {
                    throw new InvalidOperationException(
                        "The single-session Unity rebuild queue exited with code " + process.ExitCode +
                        " without producing any result files. Full Unity log: " + logPath);
                }
            }

            Report(progress, worker == null
                ? "Unity batch session complete. Processing rebuild results..."
                : "Persistent Unity worker queue complete. Processing rebuild results...");
            return outcomes;
        }

        public async Task<FamilyBuildResultFile> BuildFamilyAsync(
            string unityExe,
            string projectPath,
            IList<BuildJob> jobs,
            string familyKey,
            string familyName,
            string outputPath,
            Action<string> progress,
            Action<string> logLine,
            UnityWorkerService worker,
            string packageMode = BuildPackageModes.Family)
        {
            if (jobs == null || jobs.Count == 0)
                throw new InvalidOperationException("A combined package build requires at least one Memento Maker project.");

            string jobsRoot = Path.Combine(AppInfo.LocalDataRoot, "Jobs");
            Directory.CreateDirectory(jobsRoot);
            string familyFolder = Path.Combine(
                jobsRoot,
                "Family_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + "_" + Guid.NewGuid().ToString("N").Substring(0, 6));
            Directory.CreateDirectory(familyFolder);

            FamilyBuildManifest manifest = new FamilyBuildManifest();
            manifest.JobPaths = new List<string>();
            manifest.FamilyKey = familyKey ?? "";
            manifest.FamilyName = familyName ?? "Variant Family";
            manifest.OutputPath = outputPath ?? "";
            manifest.PackageMode = BuildPackageModes.Normalise(packageMode);

            for (int i = 0; i < jobs.Count; i++)
            {
                string jobFolder = Path.Combine(familyFolder, (i + 1).ToString("000"));
                Directory.CreateDirectory(jobFolder);
                string jobPath = Path.Combine(jobFolder, "build_job.json");
                JsonFile.Write(jobPath, jobs[i]);
                manifest.JobPaths.Add(jobPath);
                Report(logLine, "[Family " + (i + 1) + "/" + jobs.Count + "] Job: " + jobPath);
            }

            string manifestPath = Path.Combine(familyFolder, "family_build.json");
            string resultPath = manifestPath + ".result.json";
            string logPath = Path.Combine(familyFolder, "unity_family.log");
            JsonFile.Write(manifestPath, manifest);

            Report(logLine, "Family manifest: " + manifestPath);
            Report(logLine, "Project: " + projectPath);

            if (worker != null)
            {
                Report(progress, BuildPackageModes.Normalise(packageMode) == BuildPackageModes.DecorPack
                    ? "Sending the Decor Pack build to the running Unity worker..."
                    : "Sending the combined family build to the running Unity worker...");
                UnityWorkerCommandRequest request = new UnityWorkerCommandRequest();
                request.Command = "BuildFamily";
                request.BatchJobPath = manifestPath;
                await worker.ExecuteCommandAsync(unityExe, projectPath, request, progress, logLine);
            }
            else
            {
                Report(progress, BuildPackageModes.Normalise(packageMode) == BuildPackageModes.DecorPack
                    ? "Starting Unity 2020.3.47f1 for the Decor Pack build..."
                    : "Starting Unity 2020.3.47f1 for the combined family build...");
                ProcessStartInfo startInfo = CreateUnityStartInfo(
                    unityExe,
                    projectPath,
                    "-executeMethod TPMSimpleModMaker.TPMSimpleModMakerBatch.BuildFamilyFromCommandLine" +
                    " -familyJobPath " + Quote(manifestPath) +
                    " -logFile " + Quote(logPath));

                Process process = StartUnity(startInfo);
                DateTime startedUtc = DateTime.UtcNow;
                long logPosition = 0;
                while (!process.HasExited)
                {
                    logPosition = ReadNewLog(logPath, logPosition, logLine);
                    if (DateTime.UtcNow - startedUtc >= OneShotUnityTimeout)
                    {
                        Report(logLine, "UNITY TIMEOUT: one-shot combined-package build exceeded 45 minutes. Terminating Unity.");
                        TerminateTimedOutProcess(process);
                        ReadNewLog(logPath, logPosition, logLine);
                        throw CreateUnityTimeoutException("combined-package build", logPath);
                    }
                    await Task.Delay(500);
                }
                process.WaitForExit();
                ReadNewLog(logPath, logPosition, logLine);
            }

            if (!File.Exists(resultPath))
            {
                throw new InvalidOperationException(
                    "Unity completed the combined-package build without creating a result JSON. " +
                    (worker == null ? "Unity log: " + logPath : "Worker log: " + worker.LogPath));
            }

            FamilyBuildResultFile result = JsonFile.Read<FamilyBuildResultFile>(resultPath);
            if (result == null)
                throw new InvalidDataException("The combined package result JSON could not be parsed: " + resultPath);
            if (!result.Success)
                throw new InvalidOperationException(result.Message + Environment.NewLine +
                    (worker == null ? "Unity log: " + logPath : "Worker log: " + worker.LogPath));
            if (result.MemberResults == null || result.MemberResults.Count != jobs.Count)
                throw new InvalidDataException("The combined package build did not return one result for every member.");

            Report(progress, BuildPackageModes.Normalise(packageMode) == BuildPackageModes.DecorPack
                ? "Decor Pack built successfully as one combined mod."
                : "Variant family built successfully as one combined mod.");
            Report(logLine, "Family output: " + result.OutputPath);
            return result;
        }

        private static ProcessStartInfo CreateUnityStartInfo(string unityExe, string projectPath, string extraArguments)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = unityExe;
            startInfo.Arguments =
                "-batchmode -projectPath " + Quote(projectPath) + " " + extraArguments;
            startInfo.UseShellExecute = false;
            startInfo.CreateNoWindow = true;
            startInfo.WorkingDirectory = projectPath;
            return startInfo;
        }

        private static Process StartUnity(ProcessStartInfo startInfo)
        {
            Process process = new Process();
            process.StartInfo = startInfo;
            if (!process.Start())
                throw new InvalidOperationException("Unity could not be started.");
            return process;
        }

        private static long ReadNewBatchLog(
            string path,
            long position,
            BatchLogState state,
            Action<string> progress,
            Action<string> logLine)
        {
            if (!File.Exists(path))
                return position;

            try
            {
                using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                {
                    if (position > stream.Length)
                        position = 0;
                    stream.Position = position;

                    using (StreamReader reader = new StreamReader(stream, Encoding.UTF8, true, 4096, true))
                    {
                        string line;
                        while ((line = reader.ReadLine()) != null)
                        {
                            UpdateBatchLogState(line, state, progress);
                            if (ShouldShowLogLine(line))
                                Report(logLine, state.Prefix + line);
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

        private static void UpdateBatchLogState(string line, BatchLogState state, Action<string> progress)
        {
            if (string.IsNullOrEmpty(line))
                return;

            int marker = line.IndexOf("[QUEUE ", StringComparison.OrdinalIgnoreCase);
            if (marker < 0)
                return;

            int end = line.IndexOf(']', marker);
            if (end <= marker + 7)
                return;

            string fraction = line.Substring(marker + 7, end - (marker + 7));
            state.Prefix = "[" + fraction + "] ";

            if (line.IndexOf("BEGIN", StringComparison.OrdinalIgnoreCase) >= 0)
                Report(progress, state.Prefix + "Building queued mod in the existing Unity session...");
        }

        private static long ReadNewLog(string path, long position, Action<string> logLine)
        {
            if (!File.Exists(path))
                return position;

            try
            {
                using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                {
                    if (position > stream.Length)
                        position = 0;
                    stream.Position = position;

                    using (StreamReader reader = new StreamReader(stream, Encoding.UTF8, true, 4096, true))
                    {
                        string line;
                        while ((line = reader.ReadLine()) != null)
                        {
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

        private static bool ShouldShowLogLine(string line)
        {
            if (string.IsNullOrEmpty(line))
                return false;

            string lower = line.ToLowerInvariant();
            return line.IndexOf("[TPM Simple Mod Maker]", StringComparison.OrdinalIgnoreCase) >= 0 ||
                lower.Contains("exception") || lower.Contains("error") ||
                lower.Contains("addressables") && lower.Contains("build") ||
                lower.Contains("build completed") || lower.Contains("packing completed");
        }

        private static void TerminateTimedOutProcess(Process process)
        {
            if (process == null)
                return;
            try
            {
                if (!process.HasExited)
                    process.Kill();
            }
            catch { }
            try { process.WaitForExit(5000); }
            catch { }
        }

        private static TimeoutException CreateUnityTimeoutException(string operation, string logPath)
        {
            return new TimeoutException(
                "Unity did not complete the " + operation + " within 45 minutes, so Memento Maker stopped the one-shot Unity process. " +
                "Full Unity log: " + logPath);
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
    }
}
