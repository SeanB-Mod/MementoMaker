using System;
using System.Diagnostics;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace TPMSimpleModMaker
{
    internal sealed class WorkshopService
    {
        private static readonly TimeSpan OneShotUnityTimeout = TimeSpan.FromMinutes(45);

        public async Task<WorkshopQueryResult> QueryPublishedItemsAsync(
            string unityExe,
            string projectPath,
            Action<string> progress,
            Action<string> logLine,
            UnityWorkerService worker)
        {
            string jobsRoot = Path.Combine(AppInfo.LocalDataRoot, "Jobs");
            Directory.CreateDirectory(jobsRoot);

            string jobFolder = Path.Combine(
                jobsRoot,
                "Workshop_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + "_" + Guid.NewGuid().ToString("N").Substring(0, 6));
            Directory.CreateDirectory(jobFolder);

            string resultPath = Path.Combine(jobFolder, "workshop_query.result.json");
            string logPath = Path.Combine(jobFolder, "unity_workshop.log");

            Report(logLine, "Workshop query: " + resultPath);
            Report(logLine, "Project: " + projectPath);

            if (worker != null)
            {
                Report(progress, "Waiting for the private Unity worker...");
                UnityWorkerCommandRequest request = new UnityWorkerCommandRequest();
                request.Command = "WorkshopQuery";
                request.ResultPath = resultPath;
                await worker.ExecuteCommandAsync(
                    unityExe,
                    projectPath,
                    request,
                    progress,
                    delegate(string line)
                    {
                        HandleWorkshopProgress(line, progress);
                        Report(logLine, line);
                    });
            }
            else
            {
                Report(progress, "Starting Unity to check Steam Workshop...");
                ProcessStartInfo startInfo = new ProcessStartInfo();
                startInfo.FileName = unityExe;
                startInfo.Arguments =
                    "-batchmode -projectPath " + Quote(projectPath) +
                    " -executeMethod TPMSimpleModMaker.TPMSimpleModMakerWorkshop.QueryPublishedItemsFromCommandLine" +
                    " -workshopResultPath " + Quote(resultPath) +
                    " -logFile " + Quote(logPath);
                startInfo.UseShellExecute = false;
                startInfo.CreateNoWindow = true;
                startInfo.WorkingDirectory = projectPath;

                Process process = new Process();
                process.StartInfo = startInfo;
                if (!process.Start())
                    throw new InvalidOperationException("Unity could not be started for the Steam Workshop check.");

                DateTime startedUtc = DateTime.UtcNow;
                long logPosition = 0;
                while (!process.HasExited)
                {
                    logPosition = ReadNewLog(logPath, logPosition, progress, logLine);
                    if (DateTime.UtcNow - startedUtc >= OneShotUnityTimeout)
                    {
                        Report(logLine, "UNITY TIMEOUT: one-shot Workshop query exceeded 45 minutes. Terminating Unity.");
                        TerminateTimedOutProcess(process);
                        ReadNewLog(logPath, logPosition, progress, logLine);
                        throw CreateUnityTimeoutException("Steam Workshop query", logPath);
                    }
                    await Task.Delay(350);
                }

                process.WaitForExit();
                ReadNewLog(logPath, logPosition, progress, logLine);

                if (!File.Exists(resultPath))
                {
                    throw new InvalidOperationException(
                        "Unity exited with code " + process.ExitCode +
                        " but did not create a Steam Workshop result JSON. Full Unity log: " + logPath);
                }
            }

            if (!File.Exists(resultPath))
            {
                throw new InvalidOperationException(
                    "The private Unity worker completed the Workshop command but no result JSON was produced. Worker log: " +
                    (worker == null ? logPath : worker.LogPath));
            }

            WorkshopQueryResult result = JsonFile.Read<WorkshopQueryResult>(resultPath);
            if (result == null)
                throw new InvalidDataException("The Steam Workshop result JSON could not be parsed: " + resultPath);

            if (!result.Success)
                throw new InvalidOperationException(result.Message + Environment.NewLine +
                    (worker == null ? "Unity log: " + logPath : "Worker log: " + worker.LogPath));

            Report(progress, result.Message);
            return result;
        }

        public async Task<WorkshopPublishResult> PublishOrUpdateAsync(
            string unityExe,
            string projectPath,
            WorkshopPublishJob job,
            Action<string> progress,
            Action<string> logLine,
            UnityWorkerService worker)
        {
            if (job == null)
                throw new ArgumentNullException("job");
            if (string.IsNullOrWhiteSpace(job.ContentPath) || !Directory.Exists(job.ContentPath))
                throw new DirectoryNotFoundException("The built mod folder could not be found: " + job.ContentPath);
            if (string.IsNullOrWhiteSpace(job.PreviewPath) || !File.Exists(job.PreviewPath))
                throw new FileNotFoundException("Choose a Workshop preview image before publishing.", job.PreviewPath);

            string jobsRoot = Path.Combine(AppInfo.LocalDataRoot, "Jobs");
            Directory.CreateDirectory(jobsRoot);
            string jobFolder = Path.Combine(jobsRoot, "WorkshopPublish_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + "_" + Guid.NewGuid().ToString("N").Substring(0, 6));
            Directory.CreateDirectory(jobFolder);

            // Steam's official ISteamUGC API requires the primary preview image to be under 1 MB.
            // Always stage our own bounded JPEG so an otherwise valid user image cannot make the upload fail.
            string preparedPreview = PrepareWorkshopPreview(job.PreviewPath, jobFolder, "workshop_preview.jpg");
            List<string> preparedAdditionalPreviews = new List<string>();
            if (job.AdditionalPreviewPaths != null)
            {
                for (int i = 0; i < job.AdditionalPreviewPaths.Count; i++)
                {
                    string sourcePreview = job.AdditionalPreviewPaths[i];
                    if (string.IsNullOrWhiteSpace(sourcePreview) || !File.Exists(sourcePreview))
                        continue;
                    preparedAdditionalPreviews.Add(PrepareWorkshopPreview(sourcePreview, jobFolder,
                        "workshop_preview_" + (i + 1).ToString("00") + ".jpg"));
                }
            }
            WorkshopPublishJob stagedJob = new WorkshopPublishJob();
            stagedJob.PublishedFileId = job.PublishedFileId ?? "";
            stagedJob.Title = job.Title ?? "";
            stagedJob.Description = job.Description ?? "";
            stagedJob.ContentPath = Path.GetFullPath(job.ContentPath);
            stagedJob.PreviewPath = preparedPreview;
            stagedJob.AdditionalPreviewPaths = preparedAdditionalPreviews;
            stagedJob.ReplaceAdditionalPreviews = job.ReplaceAdditionalPreviews;
            stagedJob.LegacyItemsToDeprecate = job.LegacyItemsToDeprecate == null
                ? new List<WorkshopLegacyItem>() : new List<WorkshopLegacyItem>(job.LegacyItemsToDeprecate);
            stagedJob.Visibility = string.IsNullOrEmpty(job.Visibility) ? "Private" : job.Visibility;
            stagedJob.Tags = job.Tags == null || job.Tags.Count == 0 ? new List<string>(new[] { "Items" }) : new List<string>(job.Tags);
            stagedJob.ChangeNote = job.ChangeNote ?? "";
            stagedJob.MementoModId = job.MementoModId ?? "";
            // Preserve Workshop Required Item state when staging the job for Unity.
            // Without these fields the content upload succeeds, but Unity receives an
            // empty dependency request and Steam never gets AddDependency/RemoveDependency.
            stagedJob.RequiredPublishedFileId = job.RequiredPublishedFileId ?? "";
            stagedJob.PreviousRequiredPublishedFileId = job.PreviousRequiredPublishedFileId ?? "";

            string jobPath = Path.Combine(jobFolder, "workshop_publish.job.json");
            string resultPath = Path.Combine(jobFolder, "workshop_publish.result.json");
            string logPath = Path.Combine(jobFolder, "unity_workshop_publish.log");
            JsonFile.Write(jobPath, stagedJob);

            Report(logLine, "Workshop publish job: " + jobPath);
            Report(logLine, "Workshop content: " + stagedJob.ContentPath);
            Report(logLine, "Workshop preview: " + stagedJob.PreviewPath);
            Report(logLine, "Workshop additional previews: " + (stagedJob.AdditionalPreviewPaths == null ? 0 : stagedJob.AdditionalPreviewPaths.Count));
            Report(logLine, "Workshop legacy items queued for deprecation: " + (stagedJob.LegacyItemsToDeprecate == null ? 0 : stagedJob.LegacyItemsToDeprecate.Count));
            Report(logLine, "Workshop Required Item: " + (string.IsNullOrEmpty(stagedJob.RequiredPublishedFileId) ? "<none>" : stagedJob.RequiredPublishedFileId));
            Report(logLine, "Workshop previous Required Item: " + (string.IsNullOrEmpty(stagedJob.PreviousRequiredPublishedFileId) ? "<none>" : stagedJob.PreviousRequiredPublishedFileId));
            Report(progress, string.IsNullOrEmpty(stagedJob.PublishedFileId) ? "Creating a new Steam Workshop item..." : "Updating Steam Workshop item " + stagedJob.PublishedFileId + "...");

            if (worker != null)
            {
                UnityWorkerCommandRequest request = new UnityWorkerCommandRequest();
                request.Command = "WorkshopPublish";
                request.WorkshopJobPath = jobPath;
                request.ResultPath = resultPath;
                await worker.ExecuteCommandAsync(unityExe, projectPath, request, progress, delegate(string line)
                {
                    HandleWorkshopPublishProgress(line, progress);
                    Report(logLine, line);
                });
            }
            else
            {
                ProcessStartInfo startInfo = new ProcessStartInfo();
                startInfo.FileName = unityExe;
                startInfo.Arguments =
                    "-batchmode -projectPath " + Quote(projectPath) +
                    " -executeMethod TPMSimpleModMaker.TPMSimpleModMakerWorkshop.PublishOrUpdateFromCommandLine" +
                    " -workshopJobPath " + Quote(jobPath) +
                    " -workshopResultPath " + Quote(resultPath) +
                    " -logFile " + Quote(logPath);
                startInfo.UseShellExecute = false;
                startInfo.CreateNoWindow = true;
                startInfo.WorkingDirectory = projectPath;

                Process process = new Process();
                process.StartInfo = startInfo;
                if (!process.Start())
                    throw new InvalidOperationException("Unity could not be started for the Steam Workshop publish/update.");
                DateTime startedUtc = DateTime.UtcNow;
                long logPosition = 0;
                while (!process.HasExited)
                {
                    logPosition = ReadNewLog(logPath, logPosition, delegate(string line) { HandleWorkshopPublishProgress(line, progress); }, logLine);
                    if (DateTime.UtcNow - startedUtc >= OneShotUnityTimeout)
                    {
                        Report(logLine, "UNITY TIMEOUT: one-shot Workshop publish/update exceeded 45 minutes. Terminating Unity.");
                        TerminateTimedOutProcess(process);
                        ReadNewLog(logPath, logPosition, delegate(string line) { HandleWorkshopPublishProgress(line, progress); }, logLine);
                        throw CreateUnityTimeoutException("Steam Workshop publish/update", logPath);
                    }
                    await Task.Delay(350);
                }
                process.WaitForExit();
                ReadNewLog(logPath, logPosition, delegate(string line) { HandleWorkshopPublishProgress(line, progress); }, logLine);
            }

            if (!File.Exists(resultPath))
                throw new InvalidOperationException("Steam Workshop publish/update completed without a result JSON. " + (worker == null ? "Unity log: " + logPath : "Worker log: " + worker.LogPath));

            WorkshopPublishResult result = JsonFile.Read<WorkshopPublishResult>(resultPath);
            if (result == null)
                throw new InvalidDataException("The Steam Workshop publish result JSON could not be parsed: " + resultPath);
            if (!result.Success)
            {
                string idNote = string.IsNullOrEmpty(result.PublishedFileId) ? "" : Environment.NewLine +
                    "Steam Workshop item ID involved: " + result.PublishedFileId + ". If a NEW item was created before the upload failed, select/link this item on the next attempt instead of creating another one.";
                throw new InvalidOperationException(result.Message + idNote + Environment.NewLine + (worker == null ? "Unity log: " + logPath : "Worker log: " + worker.LogPath));
            }
            Report(progress, result.Message);
            return result;
        }

        private static string PrepareWorkshopPreview(string sourcePath, string jobFolder, string fileName)
        {
            bool preservePng = string.Equals(Path.GetExtension(sourcePath), ".png", StringComparison.OrdinalIgnoreCase);
            if (preservePng)
                return PrepareWorkshopPngPreview(sourcePath, jobFolder, fileName);

            string target = Path.Combine(jobFolder, string.IsNullOrEmpty(fileName) ? "workshop_preview.jpg" : fileName);
            using (Image source = Image.FromFile(sourcePath))
            {
                const int maxDimension = 768;
                double scale = Math.Min(1.0, Math.Min((double)maxDimension / Math.Max(1, source.Width), (double)maxDimension / Math.Max(1, source.Height)));
                int width = Math.Max(1, (int)Math.Round(source.Width * scale));
                int height = Math.Max(1, (int)Math.Round(source.Height * scale));
                using (Bitmap bitmap = new Bitmap(source, new Size(width, height)))
                {
                    ImageCodecInfo codec = GetJpegCodec();
                    long[] qualities = new long[] { 88L, 78L, 68L, 58L, 48L };
                    for (int i = 0; i < qualities.Length; i++)
                    {
                        if (File.Exists(target)) File.Delete(target);
                        using (EncoderParameters parameters = new EncoderParameters(1))
                        {
                            parameters.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, qualities[i]);
                            bitmap.Save(target, codec, parameters);
                        }
                        if (new FileInfo(target).Length < 950L * 1024L)
                            return target;
                    }
                }
            }
            if (!File.Exists(target) || new FileInfo(target).Length >= 1024L * 1024L)
                throw new InvalidDataException("Memento Maker could not prepare a Steam Workshop preview image under 1 MB.");
            return target;
        }

        private static string PrepareWorkshopPngPreview(string sourcePath, string jobFolder, string fileName)
        {
            string baseName = string.IsNullOrEmpty(fileName) ? "workshop_preview" : Path.GetFileNameWithoutExtension(fileName);
            string target = Path.Combine(jobFolder, baseName + ".png");
            int[] maximumDimensions = new int[] { 768, 672, 576, 512, 448, 384, 320, 256 };

            using (Image source = Image.FromFile(sourcePath))
            {
                for (int i = 0; i < maximumDimensions.Length; i++)
                {
                    int maxDimension = maximumDimensions[i];
                    double scale = Math.Min(1.0, Math.Min((double)maxDimension / Math.Max(1, source.Width), (double)maxDimension / Math.Max(1, source.Height)));
                    int width = Math.Max(1, (int)Math.Round(source.Width * scale));
                    int height = Math.Max(1, (int)Math.Round(source.Height * scale));
                    using (Bitmap bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb))
                    using (Graphics graphics = Graphics.FromImage(bitmap))
                    {
                        graphics.Clear(Color.Transparent);
                        graphics.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceOver;
                        graphics.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                        graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                        graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                        graphics.DrawImage(source, new Rectangle(0, 0, width, height));
                        if (File.Exists(target)) File.Delete(target);
                        bitmap.Save(target, ImageFormat.Png);
                    }
                    if (new FileInfo(target).Length < 950L * 1024L)
                        return target;
                }
            }

            if (!File.Exists(target) || new FileInfo(target).Length >= 1024L * 1024L)
                throw new InvalidDataException("Memento Maker could not prepare a transparent Steam Workshop preview image under 1 MB.");
            return target;
        }

        private static ImageCodecInfo GetJpegCodec()
        {
            ImageCodecInfo[] codecs = ImageCodecInfo.GetImageEncoders();
            for (int i = 0; i < codecs.Length; i++)
                if (codecs[i].FormatID == ImageFormat.Jpeg.Guid)
                    return codecs[i];
            throw new InvalidOperationException("The JPEG image encoder is unavailable.");
        }

        private static void HandleWorkshopPublishProgress(string line, Action<string> progress)
        {
            if (string.IsNullOrEmpty(line) || line.IndexOf("[Memento Workshop]", StringComparison.OrdinalIgnoreCase) < 0)
                return;
            if (line.IndexOf("Safety check: validating", StringComparison.OrdinalIgnoreCase) >= 0)
                Report(progress, "Revalidating the exact Workshop item before update...");
            else if (line.IndexOf("Safety check passed", StringComparison.OrdinalIgnoreCase) >= 0)
                Report(progress, "Workshop link validated. Preparing update...");
            else if (line.IndexOf("Creating a new", StringComparison.OrdinalIgnoreCase) >= 0)
                Report(progress, "Creating the Steam Workshop item...");
            else if (line.IndexOf("Created Workshop item", StringComparison.OrdinalIgnoreCase) >= 0)
                Report(progress, "Workshop item created. Uploading content...");
            else if (line.IndexOf("Updating existing", StringComparison.OrdinalIgnoreCase) >= 0)
                Report(progress, "Preparing existing Workshop item for update...");
            else if (line.IndexOf("Upload progress", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                int marker = line.IndexOf("Upload progress:", StringComparison.OrdinalIgnoreCase);
                Report(progress, marker >= 0 ? line.Substring(marker) : "Uploading Workshop content...");
            }
        }

        private static void HandleWorkshopProgress(string line, Action<string> progress)
        {
            if (string.IsNullOrEmpty(line))
                return;
            if (line.IndexOf("[Memento Workshop]", StringComparison.OrdinalIgnoreCase) < 0)
                return;

            if (line.IndexOf("Querying", StringComparison.OrdinalIgnoreCase) >= 0)
                Report(progress, "Querying your published Steam Workshop items...");
            else if (line.IndexOf("Steam connected", StringComparison.OrdinalIgnoreCase) >= 0)
                Report(progress, "Steam connected. Reading Workshop items...");
        }

        private static long ReadNewLog(
            string path,
            long position,
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
                            if (line.IndexOf("[Memento Workshop]", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                Report(logLine, line);
                                if (line.IndexOf("Querying", StringComparison.OrdinalIgnoreCase) >= 0)
                                    Report(progress, "Querying your published Steam Workshop items...");
                                else if (line.IndexOf("Steam connected", StringComparison.OrdinalIgnoreCase) >= 0)
                                    Report(progress, "Steam connected. Reading Workshop items...");
                            }
                            else
                            {
                                string lower = line.ToLowerInvariant();
                                if (lower.Contains("exception") || lower.Contains("error"))
                                    Report(logLine, line);
                            }
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
