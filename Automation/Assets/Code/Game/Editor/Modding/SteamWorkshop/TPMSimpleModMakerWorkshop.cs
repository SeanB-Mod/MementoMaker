using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Text;
using Steamworks;
using UnityEditor;
using UnityEngine;

namespace TPMSimpleModMaker
{
    /// <summary>
    /// Steam Workshop bridge for Memento Maker.
    /// Uses the same Steamworks.NET integration and app IDs as Two Point Museum's
    /// official SteamWorkshopUploadTool: creator/query app 3457760 and consumer app 2185060.
    /// </summary>
    public static class TPMSimpleModMakerWorkshop
    {
        private const uint QueryPageSize = 50;
        private const uint CreatorAppId = 3457760;
        private const uint ConsumerAppId = 2185060;
        private const int QueryTimeoutSeconds = 45;
        private const int CreateTimeoutSeconds = 60;
        private const int SubmitTimeoutSeconds = 900;
        private const int DependencyTimeoutSeconds = 60;

        [Serializable]
        public sealed class WorkshopItem
        {
            public string publishedFileId = "";
            public string title = "";
            public string description = "";
            public string tags = "";
            public string visibility = "Public";
            public string metadata = "";
            public uint additionalPreviewCount;
        }

        [Serializable]
        public sealed class WorkshopQueryResult
        {
            public bool success;
            public string message = "";
            public string steamUserId = "";
            public string steamPersonaName = "";
            public WorkshopItem[] items = new WorkshopItem[0];
            public string utcCompleted = "";
        }


        [Serializable]
        public sealed class WorkshopLegacyItem
        {
            public string publishedFileId = "";
            public string title = "";
        }

        [Serializable]
        public sealed class WorkshopPublishJob
        {
            public string publishedFileId = "";
            public string title = "";
            public string description = "";
            public string contentPath = "";
            public string previewPath = "";
            public string[] additionalPreviewPaths = new string[0];
            public bool replaceAdditionalPreviews;
            public WorkshopLegacyItem[] legacyItemsToDeprecate = new WorkshopLegacyItem[0];
            public string visibility = "Public";
            public string[] tags = new string[0];
            public string changeNote = "";
            public string mementoModId = "";
            public string requiredPublishedFileId = "";
            public string previousRequiredPublishedFileId = "";
        }

        [Serializable]
        public sealed class WorkshopPublishResult
        {
            public bool success;
            public string message = "";
            public string publishedFileId = "";
            public bool createdNew;
            public bool needsLegalAgreement;
            public bool dependencyUpdated;
            public string dependencyPublishedFileId = "";
            public string steamUserId = "";
            public string steamPersonaName = "";
            public int additionalPreviewCount;
            public int deprecatedItemCount;
            public string[] warnings = new string[0];
            public string utcCompleted = "";
        }

        private static CallResult<SteamUGCQueryCompleted_t> _queryCallResult;
        private static bool _queryCompleted;
        private static bool _queryIoFailure;
        private static SteamUGCQueryCompleted_t _queryCallbackResult;

        private static CallResult<CreateItemResult_t> _createCallResult;
        private static bool _createCompleted;
        private static bool _createIoFailure;
        private static CreateItemResult_t _createCallbackResult;

        private static CallResult<SubmitItemUpdateResult_t> _submitCallResult;
        private static bool _submitCompleted;
        private static bool _submitIoFailure;
        private static SubmitItemUpdateResult_t _submitCallbackResult;

        private static CallResult<AddUGCDependencyResult_t> _addDependencyCallResult;
        private static bool _addDependencyCompleted;
        private static bool _addDependencyIoFailure;
        private static AddUGCDependencyResult_t _addDependencyCallbackResult;

        private static CallResult<RemoveUGCDependencyResult_t> _removeDependencyCallResult;
        private static bool _removeDependencyCompleted;
        private static bool _removeDependencyIoFailure;
        private static RemoveUGCDependencyResult_t _removeDependencyCallbackResult;

        public static void QueryPublishedItemsFromCommandLine()
        {
            int exitCode = 0;
            try
            {
                string resultPath = GetCommandLineValue("-workshopResultPath");
                if (string.IsNullOrWhiteSpace(resultPath))
                    throw new ArgumentException("Missing required -workshopResultPath argument.");
                WorkshopQueryResult result = QueryPublishedItems(resultPath);
                exitCode = result.success ? 0 : 1;
            }
            catch (Exception ex)
            {
                exitCode = 1;
                Debug.LogException(ex);
                Debug.LogError("[Memento Workshop] Workshop query failed before a result could be produced: " + ex.Message);
            }

            if (Application.isBatchMode)
                EditorApplication.Exit(exitCode);
            else if (exitCode != 0)
                throw new Exception("Memento Maker Workshop query failed. See the Unity Console for details.");
        }

        public static void PublishOrUpdateFromCommandLine()
        {
            int exitCode = 0;
            try
            {
                string jobPath = GetCommandLineValue("-workshopJobPath");
                string resultPath = GetCommandLineValue("-workshopResultPath");
                if (string.IsNullOrWhiteSpace(jobPath))
                    throw new ArgumentException("Missing required -workshopJobPath argument.");
                if (string.IsNullOrWhiteSpace(resultPath))
                    throw new ArgumentException("Missing required -workshopResultPath argument.");
                WorkshopPublishResult result = PublishOrUpdate(jobPath, resultPath);
                exitCode = result.success ? 0 : 1;
            }
            catch (Exception ex)
            {
                exitCode = 1;
                Debug.LogException(ex);
                Debug.LogError("[Memento Workshop] Workshop publish/update failed before a result could be produced: " + ex.Message);
            }

            if (Application.isBatchMode)
                EditorApplication.Exit(exitCode);
            else if (exitCode != 0)
                throw new Exception("Memento Maker Workshop publish/update failed. See the Unity Console for details.");
        }

        public static void QueryPublishedItemsForWorker(string resultPath)
        {
            QueryPublishedItems(resultPath);
        }

        public static void PublishOrUpdateForWorker(string jobPath, string resultPath)
        {
            PublishOrUpdate(jobPath, resultPath);
        }

        public static WorkshopQueryResult QueryPublishedItems(string resultPath)
        {
            bool steamInitialised = false;
            WorkshopQueryResult result = new WorkshopQueryResult();
            result.utcCompleted = DateTime.UtcNow.ToString("o");

            if (string.IsNullOrWhiteSpace(resultPath))
                throw new ArgumentException("Workshop result path is empty.");
            resultPath = Path.GetFullPath(resultPath);
            EnsureDirectoryForFile(resultPath);

            try
            {
                string steamError;
                if (!InitialiseSteam(out steamError))
                {
                    result.success = false;
                    result.message = steamError;
                    WriteResult(resultPath, result);
                    return result;
                }
                steamInitialised = true;
                CSteamID steamId = SteamUser.GetSteamID();
                result.steamUserId = steamId.ToString();
                result.steamPersonaName = SteamFriends.GetPersonaName();

                Debug.Log("[Memento Workshop] Steam connected as " + result.steamPersonaName + " (" + result.steamUserId + ").");
                Debug.Log("[Memento Workshop] Querying published Workshop items...");
                List<WorkshopItem> items = QueryAllPublishedItems(steamId.GetAccountID());
                result.items = items.ToArray();
                result.success = true;
                result.message = "Steam Workshop query completed successfully. Found " + items.Count + " published item" + (items.Count == 1 ? "." : "s.");
                result.utcCompleted = DateTime.UtcNow.ToString("o");
                Debug.Log("[Memento Workshop] " + result.message);
                WriteResult(resultPath, result);
                return result;
            }
            catch (Exception ex)
            {
                result.success = false;
                result.message = ex.ToString();
                result.utcCompleted = DateTime.UtcNow.ToString("o");
                Debug.LogException(ex);
                try { WriteResult(resultPath, result); } catch { }
                return result;
            }
            finally
            {
                if (steamInitialised)
                    try { SteamAPI.Shutdown(); } catch { }
            }
        }

        public static WorkshopPublishResult PublishOrUpdate(string jobPath, string resultPath)
        {
            bool steamInitialised = false;
            WorkshopPublishResult result = new WorkshopPublishResult();
            result.utcCompleted = DateTime.UtcNow.ToString("o");

            jobPath = Path.GetFullPath(jobPath);
            resultPath = Path.GetFullPath(resultPath);
            EnsureDirectoryForFile(resultPath);
            if (!File.Exists(jobPath))
                throw new FileNotFoundException("Workshop publish job was not found.", jobPath);

            WorkshopPublishJob job = JsonUtility.FromJson<WorkshopPublishJob>(File.ReadAllText(jobPath));
            ValidatePublishJob(job);

            try
            {
                string steamError;
                if (!InitialiseSteam(out steamError))
                {
                    result.success = false;
                    result.message = steamError;
                    WritePublishResult(resultPath, result);
                    return result;
                }
                steamInitialised = true;
                result.steamUserId = SteamUser.GetSteamID().ToString();
                result.steamPersonaName = SteamFriends.GetPersonaName();

                PublishedFileId_t publishedId;
                WorkshopItem validatedItem = null;
                ulong existingId;
                if (!string.IsNullOrWhiteSpace(job.publishedFileId))
                {
                    if (!ulong.TryParse(job.publishedFileId, out existingId) || existingId == 0)
                        throw new ArgumentException("The existing Steam Workshop PublishedFileId is invalid: " + job.publishedFileId);
                    Debug.Log("[Memento Workshop] Safety check: validating exact PublishedFileId " + existingId + " against the current Steam user's published Two Point Museum items...");
                    validatedItem = FindCurrentUsersPublishedItem(existingId);
                    if (validatedItem == null)
                        throw new InvalidOperationException(
                            "WORKSHOP LINK SAFETY CHECK FAILED: PublishedFileId " + existingId +
                            " is not present in the current Steam user's published Two Point Museum Workshop items. " +
                            "The item may have been deleted or the saved link may be stale. No Workshop update was attempted.");

                    publishedId = new PublishedFileId_t(existingId);
                    result.publishedFileId = existingId.ToString();
                    result.createdNew = false;
                    Debug.Log("[Memento Workshop] Safety check passed for exact Workshop item " + existingId + " (" + (validatedItem.title ?? "") + ").");
                    Debug.Log("[Memento Workshop] Updating existing Workshop item " + existingId + "...");
                }
                else
                {
                    Debug.Log("[Memento Workshop] Creating a new Two Point Museum Workshop item...");
                    CreateItemResult_t createResult = CreateWorkshopItem();
                    if (createResult.m_eResult != EResult.k_EResultOK || _createIoFailure)
                        throw new InvalidOperationException("Steam could not create the Workshop item. Status: " + createResult.m_eResult + ".");
                    publishedId = createResult.m_nPublishedFileId;
                    result.publishedFileId = publishedId.m_PublishedFileId.ToString();
                    result.createdNew = true;
                    result.needsLegalAgreement = createResult.m_bUserNeedsToAcceptWorkshopLegalAgreement;
                    Debug.Log("[Memento Workshop] Created Workshop item " + publishedId.m_PublishedFileId + ". Uploading content and metadata...");
                }

                UGCUpdateHandle_t updateHandle = SteamUGC.StartItemUpdate(new AppId_t(ConsumerAppId), publishedId);
                if (updateHandle == UGCUpdateHandle_t.Invalid)
                    throw new InvalidOperationException("Steam returned an invalid Workshop update handle.");

                RequireSet(SteamUGC.SetItemTags(updateHandle, new List<string>(job.tags ?? new string[0])), "tags");
                RequireSet(SteamUGC.SetItemTitle(updateHandle, job.title), "title");
                RequireSet(SteamUGC.SetItemDescription(updateHandle, job.description ?? ""), "description");
                RequireSet(SteamUGC.SetItemVisibility(updateHandle, ParseVisibility(job.visibility)), "visibility");
                RequireSet(SteamUGC.SetItemContent(updateHandle, job.contentPath), "content folder");
                RequireSet(SteamUGC.SetItemPreview(updateHandle, job.previewPath), "preview image");

                // Memento Maker owns the additional-preview gallery for combined-family Workshop items.
                // Remove the current indexed list from highest index to zero, then add current previews.
                if (job.replaceAdditionalPreviews && !result.createdNew)
                {
                    uint existingPreviewCount = validatedItem == null ? 0U : validatedItem.additionalPreviewCount;
                    int removed = 0;
                    for (int previewIndex = (int)existingPreviewCount - 1; previewIndex >= 0; previewIndex--)
                    {
                        if (!SteamUGC.RemoveItemPreview(updateHandle, (uint)previewIndex))
                            throw new InvalidOperationException("Steam rejected removal of existing additional preview index " + previewIndex + ".");
                        removed++;
                    }
                    Debug.Log("[Memento Workshop] Queued removal of " + removed + " existing additional preview(s) before refreshing the family gallery.");
                }
                if (job.additionalPreviewPaths != null)
                {
                    for (int i = 0; i < job.additionalPreviewPaths.Length; i++)
                    {
                        string extraPreview = job.additionalPreviewPaths[i];
                        if (string.IsNullOrWhiteSpace(extraPreview))
                            continue;
                        RequireSet(SteamUGC.AddItemPreviewFile(updateHandle, extraPreview,
                            EItemPreviewType.k_EItemPreviewType_Image), "additional preview image " + (i + 1));
                        result.additionalPreviewCount++;
                    }
                }

                if (!string.IsNullOrEmpty(job.mementoModId))
                    SteamUGC.SetItemMetadata(updateHandle, "MementoMakerModId=" + job.mementoModId);

                SubmitItemUpdateResult_t submitResult = SubmitWorkshopUpdate(updateHandle, job.changeNote ?? "");
                if (submitResult.m_eResult != EResult.k_EResultOK || _submitIoFailure)
                    throw new InvalidOperationException("Steam Workshop upload failed with status " + submitResult.m_eResult + ".");

                result.publishedFileId = submitResult.m_nPublishedFileId.m_PublishedFileId.ToString();
                if (string.IsNullOrEmpty(result.publishedFileId) || result.publishedFileId == "0")
                    result.publishedFileId = publishedId.m_PublishedFileId.ToString();
                result.needsLegalAgreement = result.needsLegalAgreement || submitResult.m_bUserNeedsToAcceptWorkshopLegalAgreement;

                UpdateRequiredItemDependency(
                    new PublishedFileId_t(ulong.Parse(result.publishedFileId)),
                    job.previousRequiredPublishedFileId,
                    job.requiredPublishedFileId,
                    result);

                List<string> maintenanceWarnings = new List<string>();
                DeprecateLegacyWorkshopItems(job.legacyItemsToDeprecate, result.publishedFileId, result, maintenanceWarnings);
                result.warnings = maintenanceWarnings.ToArray();

                result.success = true;
                result.utcCompleted = DateTime.UtcNow.ToString("o");
                result.message = (result.createdNew ? "Workshop item published successfully." : "Workshop item updated successfully.") +
                    " PublishedFileId: " + result.publishedFileId + (result.needsLegalAgreement ? " Steam requires acceptance of the Workshop Legal Agreement before the item can be publicly visible." : "");
                Debug.Log("[Memento Workshop] " + result.message);
                WritePublishResult(resultPath, result);
                return result;
            }
            catch (Exception ex)
            {
                result.success = false;
                result.message = ex.ToString();
                result.utcCompleted = DateTime.UtcNow.ToString("o");
                Debug.LogException(ex);
                Debug.LogError("[Memento Workshop] Publish/update failed: " + ex.Message);
                try { WritePublishResult(resultPath, result); } catch { }
                return result;
            }
            finally
            {
                if (steamInitialised)
                    try { SteamAPI.Shutdown(); } catch { }
            }
        }

        private static bool InitialiseSteam(out string errorMessage)
        {
            Debug.Log("[Memento Workshop] Initialising Steam using the official SDK integration...");
            string steamError;
            ESteamAPIInitResult initStatus = SteamAPI.InitEx(out steamError);
            if (initStatus == ESteamAPIInitResult.k_ESteamAPIInitResult_OK)
            {
                errorMessage = "";
                return true;
            }
            errorMessage = "Steam could not be initialised. Ensure the Steam client is running and you are logged in. Steam result: " + initStatus +
                (string.IsNullOrEmpty(steamError) ? "" : " - " + steamError);
            Debug.LogError("[Memento Workshop] " + errorMessage);
            return false;
        }

        private static CreateItemResult_t CreateWorkshopItem()
        {
            _createCompleted = false;
            _createIoFailure = false;
            _createCallbackResult = new CreateItemResult_t();
            SteamAPICall_t call = SteamUGC.CreateItem(new AppId_t(ConsumerAppId), EWorkshopFileType.k_EWorkshopFileTypeCommunity);
            _createCallResult = CallResult<CreateItemResult_t>.Create(OnCreateCompleted);
            _createCallResult.Set(call);
            DateTime deadline = DateTime.UtcNow.AddSeconds(CreateTimeoutSeconds);
            while (!_createCompleted && DateTime.UtcNow < deadline)
            {
                SteamAPI.RunCallbacks();
                Thread.Sleep(50);
            }
            if (!_createCompleted)
                throw new TimeoutException("Steam did not finish creating the Workshop item within " + CreateTimeoutSeconds + " seconds.");
            return _createCallbackResult;
        }

        private static SubmitItemUpdateResult_t SubmitWorkshopUpdate(UGCUpdateHandle_t handle, string changeNote)
        {
            _submitCompleted = false;
            _submitIoFailure = false;
            _submitCallbackResult = new SubmitItemUpdateResult_t();
            SteamAPICall_t call = SteamUGC.SubmitItemUpdate(handle, changeNote ?? "");
            _submitCallResult = CallResult<SubmitItemUpdateResult_t>.Create(OnSubmitCompleted);
            _submitCallResult.Set(call);

            DateTime deadline = DateTime.UtcNow.AddSeconds(SubmitTimeoutSeconds);
            int lastPercent = -1;
            while (!_submitCompleted && DateTime.UtcNow < deadline)
            {
                SteamAPI.RunCallbacks();
                ulong processed;
                ulong total;
                EItemUpdateStatus status = SteamUGC.GetItemUpdateProgress(handle, out processed, out total);
                int percent = total > 0 ? (int)Math.Min(100UL, (processed * 100UL) / total) : 0;
                if (percent != lastPercent && (percent == 0 || percent == 100 || percent >= lastPercent + 5))
                {
                    lastPercent = percent;
                    Debug.Log("[Memento Workshop] Upload progress: " + percent + "% (" + status + ").");
                }
                Thread.Sleep(100);
            }
            if (!_submitCompleted)
                throw new TimeoutException("Steam Workshop upload did not finish within " + SubmitTimeoutSeconds + " seconds.");
            return _submitCallbackResult;
        }

        private static void UpdateRequiredItemDependency(
            PublishedFileId_t workshopItemId,
            string previousRequiredId,
            string requiredId,
            WorkshopPublishResult result)
        {
            ulong previous = ParseOptionalPublishedFileId(previousRequiredId, "previous Required Item");
            ulong required = ParseOptionalPublishedFileId(requiredId, "Required Item");
            ulong item = workshopItemId.m_PublishedFileId;

            if (required == item && required != 0)
                throw new InvalidOperationException("A Workshop item cannot require itself.");

            if (previous != 0 && previous != required)
            {
                Debug.Log("[Memento Workshop] Removing previous Required Item dependency " + previous + " from Workshop item " + item + "...");
                RemoveUGCDependencyResult_t removeResult = RemoveDependencyBlocking(workshopItemId, new PublishedFileId_t(previous));
                if (_removeDependencyIoFailure || !IsRemoveDependencySuccess(removeResult.m_eResult))
                    throw new InvalidOperationException(
                        "Steam could not remove the previous Required Item " + previous + ". Status: " + removeResult.m_eResult + ".");
                result.dependencyUpdated = removeResult.m_eResult == EResult.k_EResultOK;
            }

            // Re-assert the current Required Item on every publish/update, even when it
            // matches the locally stored previous ID. This is intentionally idempotent
            // and repairs projects published by the 0.9.5 preview where the desktop
            // staging layer accidentally dropped the dependency fields before Unity.
            if (required != 0)
            {
                Debug.Log("[Memento Workshop] Ensuring Required Item dependency " + required + " exists on Workshop item " + item + "...");
                AddUGCDependencyResult_t addResult = AddDependencyBlocking(workshopItemId, new PublishedFileId_t(required));
                if (_addDependencyIoFailure || !IsAddDependencySuccess(addResult.m_eResult))
                    throw new InvalidOperationException(
                        "Steam could not add Required Item " + required + ". Status: " + addResult.m_eResult + ".");
                if (addResult.m_eResult == EResult.k_EResultOK)
                    result.dependencyUpdated = true;
                else
                    Debug.Log("[Memento Workshop] Required Item dependency was already present; Steam confirmed the existing relationship.");
            }

            result.dependencyPublishedFileId = required == 0 ? "" : required.ToString();
            if (result.dependencyUpdated)
                Debug.Log("[Memento Workshop] Required Item dependency updated successfully.");
        }


        private static bool IsAddDependencySuccess(EResult result)
        {
            return result == EResult.k_EResultOK ||
                result == EResult.k_EResultDuplicateRequest;
        }

        private static bool IsRemoveDependencySuccess(EResult result)
        {
            // FileNotFound / NoMatch are safe idempotent outcomes when repairing local
            // state from an older preview that recorded a dependency which Steam never had.
            return result == EResult.k_EResultOK ||
                result == EResult.k_EResultFileNotFound ||
                result == EResult.k_EResultNoMatch;
        }

        private static ulong ParseOptionalPublishedFileId(string value, string label)
        {
            if (string.IsNullOrWhiteSpace(value))
                return 0;
            ulong parsed;
            if (!ulong.TryParse(value, out parsed) || parsed == 0)
                throw new ArgumentException("The " + label + " PublishedFileId is invalid: " + value);
            return parsed;
        }

        private static AddUGCDependencyResult_t AddDependencyBlocking(PublishedFileId_t workshopItemId, PublishedFileId_t dependencyId)
        {
            _addDependencyCompleted = false;
            _addDependencyIoFailure = false;
            _addDependencyCallbackResult = new AddUGCDependencyResult_t();

            SteamAPICall_t call = SteamUGC.AddDependency(workshopItemId, dependencyId);
            _addDependencyCallResult = CallResult<AddUGCDependencyResult_t>.Create(OnAddDependencyCompleted);
            _addDependencyCallResult.Set(call);

            DateTime deadline = DateTime.UtcNow.AddSeconds(DependencyTimeoutSeconds);
            while (!_addDependencyCompleted && DateTime.UtcNow < deadline)
            {
                SteamAPI.RunCallbacks();
                Thread.Sleep(50);
            }
            if (!_addDependencyCompleted)
                throw new TimeoutException("Steam did not finish adding the Required Item within " + DependencyTimeoutSeconds + " seconds.");
            return _addDependencyCallbackResult;
        }

        private static RemoveUGCDependencyResult_t RemoveDependencyBlocking(PublishedFileId_t workshopItemId, PublishedFileId_t dependencyId)
        {
            _removeDependencyCompleted = false;
            _removeDependencyIoFailure = false;
            _removeDependencyCallbackResult = new RemoveUGCDependencyResult_t();

            SteamAPICall_t call = SteamUGC.RemoveDependency(workshopItemId, dependencyId);
            _removeDependencyCallResult = CallResult<RemoveUGCDependencyResult_t>.Create(OnRemoveDependencyCompleted);
            _removeDependencyCallResult.Set(call);

            DateTime deadline = DateTime.UtcNow.AddSeconds(DependencyTimeoutSeconds);
            while (!_removeDependencyCompleted && DateTime.UtcNow < deadline)
            {
                SteamAPI.RunCallbacks();
                Thread.Sleep(50);
            }
            if (!_removeDependencyCompleted)
                throw new TimeoutException("Steam did not finish removing the previous Required Item within " + DependencyTimeoutSeconds + " seconds.");
            return _removeDependencyCallbackResult;
        }

        private static void OnAddDependencyCompleted(AddUGCDependencyResult_t result, bool ioFailure)
        {
            _addDependencyCallbackResult = result;
            _addDependencyIoFailure = ioFailure;
            _addDependencyCompleted = true;
        }

        private static void OnRemoveDependencyCompleted(RemoveUGCDependencyResult_t result, bool ioFailure)
        {
            _removeDependencyCallbackResult = result;
            _removeDependencyIoFailure = ioFailure;
            _removeDependencyCompleted = true;
        }

        private static void OnCreateCompleted(CreateItemResult_t result, bool ioFailure)
        {
            _createCallbackResult = result;
            _createIoFailure = ioFailure;
            _createCompleted = true;
        }

        private static void OnSubmitCompleted(SubmitItemUpdateResult_t result, bool ioFailure)
        {
            _submitCallbackResult = result;
            _submitIoFailure = ioFailure;
            _submitCompleted = true;
        }

        private static void DeprecateLegacyWorkshopItems(WorkshopLegacyItem[] legacyItems, string familyPublishedFileId,
            WorkshopPublishResult result, List<string> warnings)
        {
            if (legacyItems == null || legacyItems.Length == 0)
                return;

            List<WorkshopItem> authoredItems = QueryAllPublishedItems(SteamUser.GetSteamID().GetAccountID());
            HashSet<string> processed = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < legacyItems.Length; i++)
            {
                WorkshopLegacyItem legacy = legacyItems[i];
                string idText = legacy == null ? "" : (legacy.publishedFileId ?? "").Trim();
                if (string.IsNullOrEmpty(idText) || string.Equals(idText, familyPublishedFileId ?? "", StringComparison.Ordinal) || !processed.Add(idText))
                    continue;

                ulong id;
                if (!ulong.TryParse(idText, out id) || id == 0)
                {
                    warnings.Add("Legacy Workshop item ID '" + idText + "' is invalid and was not deprecated.");
                    continue;
                }

                WorkshopItem current = null;
                for (int j = 0; j < authoredItems.Count; j++)
                {
                    if (authoredItems[j] != null && string.Equals(authoredItems[j].publishedFileId, idText, StringComparison.Ordinal))
                    {
                        current = authoredItems[j];
                        break;
                    }
                }
                if (current == null)
                {
                    warnings.Add("Legacy Workshop item " + idText + " is no longer present in the current user's authored items and was not changed.");
                    continue;
                }

                try
                {
                    UGCUpdateHandle_t legacyHandle = SteamUGC.StartItemUpdate(new AppId_t(ConsumerAppId), new PublishedFileId_t(id));
                    if (legacyHandle == UGCUpdateHandle_t.Invalid)
                        throw new InvalidOperationException("Steam returned an invalid update handle.");

                    string title = current.title ?? "";
                    if (!title.StartsWith("Deprecated - ", StringComparison.OrdinalIgnoreCase))
                        title = "Deprecated - " + title;
                    if (title.Length > 128)
                        title = title.Substring(0, 128);

                    RequireSet(SteamUGC.SetItemTitle(legacyHandle, title), "deprecated item title");
                    RequireSet(SteamUGC.SetItemVisibility(legacyHandle,
                        ERemoteStoragePublishedFileVisibility.k_ERemoteStoragePublishedFileVisibilityPrivate), "deprecated item visibility");
                    SubmitItemUpdateResult_t legacySubmit = SubmitWorkshopUpdate(legacyHandle,
                        "Deprecated: this item is now included in a Memento Maker variant family.");
                    if (legacySubmit.m_eResult != EResult.k_EResultOK || _submitIoFailure)
                        throw new InvalidOperationException("Steam returned " + legacySubmit.m_eResult + ".");

                    result.deprecatedItemCount++;
                    Debug.Log("[Memento Workshop] Deprecated legacy Workshop item " + idText + " as '" + title + "' and changed visibility to Hidden/Private.");
                }
                catch (Exception ex)
                {
                    warnings.Add("Legacy Workshop item " + idText + " could not be deprecated: " + ex.Message);
                    Debug.LogWarning("[Memento Workshop] " + warnings[warnings.Count - 1]);
                }
            }
        }

        private static void ValidatePublishJob(WorkshopPublishJob job)
        {
            if (job == null)
                throw new InvalidDataException("Workshop publish job JSON could not be parsed.");
            if (string.IsNullOrWhiteSpace(job.title))
                throw new ArgumentException("Workshop title is required.");
            if (string.IsNullOrWhiteSpace(job.contentPath) || !Directory.Exists(job.contentPath))
                throw new DirectoryNotFoundException("Workshop content folder was not found: " + job.contentPath);
            if (string.IsNullOrWhiteSpace(job.previewPath) || !File.Exists(job.previewPath))
                throw new FileNotFoundException("Workshop preview image was not found.", job.previewPath);
            if (new FileInfo(job.previewPath).Length >= 1024L * 1024L)
                throw new InvalidDataException("Steam Workshop preview images must be under 1 MB. Memento Maker should have prepared a smaller preview before this point.");
            if (job.additionalPreviewPaths == null)
                job.additionalPreviewPaths = new string[0];
            for (int i = 0; i < job.additionalPreviewPaths.Length; i++)
            {
                string extra = job.additionalPreviewPaths[i];
                if (string.IsNullOrWhiteSpace(extra) || !File.Exists(extra))
                    throw new FileNotFoundException("Workshop additional preview image was not found.", extra);
                if (new FileInfo(extra).Length >= 1024L * 1024L)
                    throw new InvalidDataException("Steam Workshop additional preview images must be under 1 MB.");
            }
            if (job.legacyItemsToDeprecate == null)
                job.legacyItemsToDeprecate = new WorkshopLegacyItem[0];
            if (job.tags == null || job.tags.Length == 0)
                job.tags = new[] { "Items" };
        }

        private static string VisibilityToString(ERemoteStoragePublishedFileVisibility value)
        {
            if (value == ERemoteStoragePublishedFileVisibility.k_ERemoteStoragePublishedFileVisibilityPrivate)
                return "Private";
            if (value == ERemoteStoragePublishedFileVisibility.k_ERemoteStoragePublishedFileVisibilityFriendsOnly)
                return "Friends Only";
            if (value == ERemoteStoragePublishedFileVisibility.k_ERemoteStoragePublishedFileVisibilityUnlisted)
                return "Unlisted";
            return "Public";
        }

        private static ERemoteStoragePublishedFileVisibility ParseVisibility(string value)
        {
            if (string.Equals(value, "Private", StringComparison.OrdinalIgnoreCase))
                return ERemoteStoragePublishedFileVisibility.k_ERemoteStoragePublishedFileVisibilityPrivate;
            if (string.Equals(value, "Friends Only", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "FriendsOnly", StringComparison.OrdinalIgnoreCase))
                return ERemoteStoragePublishedFileVisibility.k_ERemoteStoragePublishedFileVisibilityFriendsOnly;
            if (string.Equals(value, "Unlisted", StringComparison.OrdinalIgnoreCase))
                return ERemoteStoragePublishedFileVisibility.k_ERemoteStoragePublishedFileVisibilityUnlisted;
            return ERemoteStoragePublishedFileVisibility.k_ERemoteStoragePublishedFileVisibilityPrivate;
        }

        private static void RequireSet(bool ok, string field)
        {
            if (!ok)
                throw new InvalidOperationException("Steam rejected the Workshop " + field + ". The update handle may be invalid or the value may not be accepted.");
        }

        private static WorkshopItem FindCurrentUsersPublishedItem(ulong publishedFileId)
        {
            CSteamID steamId = SteamUser.GetSteamID();
            List<WorkshopItem> items = QueryAllPublishedItems(steamId.GetAccountID());
            string expected = publishedFileId.ToString();
            for (int i = 0; i < items.Count; i++)
            {
                WorkshopItem item = items[i];
                if (item != null && string.Equals(item.publishedFileId, expected, StringComparison.Ordinal))
                    return item;
            }
            return null;
        }

        private static List<WorkshopItem> QueryAllPublishedItems(AccountID_t accountId)
        {
            List<WorkshopItem> allItems = new List<WorkshopItem>();
            uint page = 1;
            uint pages = 1;
            do
            {
                SteamUGCQueryCompleted_t pageResult = QueryPage(accountId, page);
                if (pageResult.m_eResult != EResult.k_EResultOK || _queryIoFailure)
                    throw new InvalidOperationException("Fetching the Workshop item list failed with status " + pageResult.m_eResult + ".");
                for (uint i = 0; i < pageResult.m_unNumResultsReturned; i++)
                {
                    SteamUGCDetails_t details;
                    if (!SteamUGC.GetQueryUGCResult(pageResult.m_handle, i, out details))
                        continue;
                    WorkshopItem item = new WorkshopItem();
                    item.publishedFileId = details.m_nPublishedFileId.m_PublishedFileId.ToString();
                    item.title = details.m_rgchTitle ?? "";
                    item.description = details.m_rgchDescription ?? "";
                    item.tags = details.m_rgchTags ?? "";
                    item.visibility = VisibilityToString(details.m_eVisibility);
                    string metadata;
                    if (SteamUGC.GetQueryUGCMetadata(pageResult.m_handle, i, out metadata, 4096))
                        item.metadata = metadata ?? "";
                    item.additionalPreviewCount = SteamUGC.GetQueryUGCNumAdditionalPreviews(pageResult.m_handle, i);
                    allItems.Add(item);
                }
                uint total = pageResult.m_unTotalMatchingResults;
                pages = total == 0 ? 1 : 1 + ((total - 1) / QueryPageSize);
                SteamUGC.ReleaseQueryUGCRequest(pageResult.m_handle);
                Debug.Log("[Memento Workshop] Workshop page " + page + "/" + pages + " returned " + pageResult.m_unNumResultsReturned + " item(s).");
                page++;
            }
            while (page <= pages);
            return allItems;
        }

        private static SteamUGCQueryCompleted_t QueryPage(AccountID_t accountId, uint page)
        {
            _queryCompleted = false;
            _queryIoFailure = false;
            _queryCallbackResult = new SteamUGCQueryCompleted_t();
            UGCQueryHandle_t queryHandle = SteamUGC.CreateQueryUserUGCRequest(
                accountId,
                EUserUGCList.k_EUserUGCList_Published,
                EUGCMatchingUGCType.k_EUGCMatchingUGCType_All,
                EUserUGCListSortOrder.k_EUserUGCListSortOrder_CreationOrderAsc,
                new AppId_t(CreatorAppId),
                new AppId_t(ConsumerAppId),
                page);
            // Family Workshop uploads write a stable MementoMakerModId metadata value. Request metadata
            // in authored-item queries so shared family links can be recovered deterministically.
            SteamUGC.SetReturnMetadata(queryHandle, true);
            SteamUGC.SetReturnAdditionalPreviews(queryHandle, true);
            SteamAPICall_t apiCall = SteamUGC.SendQueryUGCRequest(queryHandle);
            _queryCallResult = CallResult<SteamUGCQueryCompleted_t>.Create(OnQueryCompleted);
            _queryCallResult.Set(apiCall);
            DateTime deadline = DateTime.UtcNow.AddSeconds(QueryTimeoutSeconds);
            while (!_queryCompleted && DateTime.UtcNow < deadline)
            {
                SteamAPI.RunCallbacks();
                Thread.Sleep(50);
            }
            if (!_queryCompleted)
            {
                SteamUGC.ReleaseQueryUGCRequest(queryHandle);
                throw new TimeoutException("Steam Workshop did not respond within " + QueryTimeoutSeconds + " seconds.");
            }
            return _queryCallbackResult;
        }

        private static void OnQueryCompleted(SteamUGCQueryCompleted_t result, bool ioFailure)
        {
            _queryCallbackResult = result;
            _queryIoFailure = ioFailure;
            _queryCompleted = true;
        }

        private static void EnsureDirectoryForFile(string path)
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
        }

        private static void WriteResult(string path, WorkshopQueryResult result)
        {
            File.WriteAllText(path, JsonUtility.ToJson(result, true));
        }

        private static void WritePublishResult(string path, WorkshopPublishResult result)
        {
            File.WriteAllText(path, JsonUtility.ToJson(result, true));
        }

        private static string GetCommandLineValue(string key)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], key, StringComparison.OrdinalIgnoreCase))
                    return args[i + 1];
            return null;
        }
    }
}
