using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace TPMSimpleModMaker
{
    internal sealed class ProjectLibraryService
    {
        public const int CurrentProjectFormatVersion = 7;
        private readonly string _projectsRoot;
        private readonly string _familyWorkshopRoot;
        private readonly object _loadWarningSync = new object();
        private readonly List<string> _loadWarnings = new List<string>();
        private readonly HashSet<string> _loadWarningKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public ProjectLibraryService()
        {
            _projectsRoot = Path.Combine(AppInfo.LocalDataRoot, "Projects");
            _familyWorkshopRoot = Path.Combine(AppInfo.LocalDataRoot, "VariantFamilies");
            Directory.CreateDirectory(_projectsRoot);
            Directory.CreateDirectory(_familyWorkshopRoot);
        }

        public string ProjectsRoot { get { return _projectsRoot; } }

        public List<ModProjectRecord> LoadAll()
        {
            List<ModProjectRecord> result = new List<ModProjectRecord>();
            if (!Directory.Exists(_projectsRoot))
                return result;

            foreach (string directory in Directory.GetDirectories(_projectsRoot))
            {
                try
                {
                    string projectPath = Path.Combine(directory, "project.json");
                    ModProjectRecord record = JsonFile.Read<ModProjectRecord>(projectPath);
                    string recoveryNotice = JsonFile.ConsumeRecoveryNotice(projectPath);
                    if (!string.IsNullOrWhiteSpace(recoveryNotice))
                    {
                        AddLoadWarning(projectPath,
                            "Recovered saved project metadata from backup: " + Path.GetFileName(directory));
                        JsonFile.LogReliability("PROJECT RECOVERY: " + recoveryNotice);
                    }
                    if (record != null && !string.IsNullOrEmpty(record.ModId))
                    {
                        NormaliseFormatVersion(record);
                        ApplyWorkshopFamilyRegistry(record);
                        result.Add(record);
                    }
                }
                catch (Exception ex)
                {
                    string projectPath = Path.Combine(directory, "project.json");
                    AddLoadWarning(projectPath,
                        "Could not load saved project metadata: " + Path.GetFileName(directory));
                    JsonFile.LogReliability("PROJECT LOAD ERROR: project was left untouched and omitted from My Mods for " + projectPath + ". " + ex);
                }
            }

            result.Sort(delegate(ModProjectRecord a, ModProjectRecord b)
            {
                DateTime ad = ParseDate(a == null ? null : a.LastBuiltUtc);
                DateTime bd = ParseDate(b == null ? null : b.LastBuiltUtc);
                return bd.CompareTo(ad);
            });
            return result;
        }

        public List<string> ConsumeLoadWarnings()
        {
            lock (_loadWarningSync)
            {
                List<string> copy = new List<string>(_loadWarnings);
                _loadWarnings.Clear();
                _loadWarningKeys.Clear();
                return copy;
            }
        }

        private void AddLoadWarning(string key, string message)
        {
            string warningKey = string.IsNullOrWhiteSpace(key) ? (message ?? "") : key;
            lock (_loadWarningSync)
            {
                if (_loadWarningKeys.Add(warningKey))
                    _loadWarnings.Add(message ?? warningKey);
            }
        }

        public ModProjectRecord Load(string modId)
        {
            if (string.IsNullOrEmpty(modId))
                return null;
            ModProjectRecord record = JsonFile.Read<ModProjectRecord>(Path.Combine(GetProjectDirectory(modId), "project.json"));
            NormaliseFormatVersion(record);
            ApplyWorkshopFamilyRegistry(record);
            return record;
        }

        public ModProjectRecord SaveAfterBuild(
            ModProjectRecord existing,
            BuildResultFile buildResult,
            string sourceArtworkPath,
            string sourceCustomIconPath,
            string templateKey,
            string modName,
            string description,
            int itemCost,
            int kudoshCost,
            string fitMode,
            ImagePlacementState placement,
            string secondaryArtworkPath,
            string secondaryFitMode,
            ImagePlacementState secondaryPlacement,
            string iconMode,
            string outputRoot,
            string variantMode,
            string variantParentModId,
            string buildPackageMode = "Single",
            string builtFamilyKey = "",
            string builtFamilyName = "",
            IList<string> builtFamilyMemberIds = null)
        {
            if (buildResult == null || string.IsNullOrEmpty(buildResult.ItemModId))
                throw new InvalidOperationException("The build completed without returning an ItemModID, so the project record cannot be saved.");

            string modId = buildResult.ItemModId;
            string projectDirectory = GetProjectDirectory(modId);

            if (existing != null && !string.Equals(existing.ModId, modId, StringComparison.Ordinal))
                throw new InvalidOperationException("The rebuilt mod returned a different ItemModID than its saved project record.");

            if (existing == null && File.Exists(Path.Combine(projectDirectory, "project.json")))
                throw new InvalidOperationException("A saved project already exists with ItemModID " + modId + ". The generated ID was not unique.");

            Directory.CreateDirectory(projectDirectory);

            ModProjectRecord record = existing ?? new ModProjectRecord();
            record.ProjectFormatVersion = CurrentProjectFormatVersion;
            record.ProjectVersion = string.IsNullOrEmpty(record.WorkshopPublishedFileId) ? Math.Max(1, record.ProjectVersion) : Math.Max(2, record.ProjectVersion);
            record.ModId = modId;
            record.Name = modName ?? "";
            record.Description = description ?? "";
            record.Template = templateKey ?? "";
            record.ItemCost = itemCost;
            record.KudoshCost = kudoshCost;
            ApplyVariantState(record, variantMode, variantParentModId);
            if (!string.IsNullOrEmpty(buildResult.ItemCustomisationId))
                record.ItemCustomisationId = buildResult.ItemCustomisationId;
            record.FitMode = fitMode ?? "Original";
            record.Zoom = placement == null ? 1.0f : placement.Zoom;
            record.OffsetX = placement == null ? 0.0f : placement.OffsetX;
            record.OffsetY = placement == null ? 0.0f : placement.OffsetY;
            record.RotationDegrees = placement == null ? 0.0f : NormaliseRotationDegrees(placement.RotationDegrees);
            record.GuideColor = placement == null ? "Yellow" : (placement.GuideColorName ?? "Yellow");
            record.ReferenceWidth = placement == null ? 0 : placement.ReferenceWidth;
            record.ReferenceHeight = placement == null ? 0 : placement.ReferenceHeight;
            record.SecondaryFitMode = secondaryFitMode ?? "Fill";
            record.SecondaryZoom = secondaryPlacement == null ? 1.0f : secondaryPlacement.Zoom;
            record.SecondaryOffsetX = secondaryPlacement == null ? 0.0f : secondaryPlacement.OffsetX;
            record.SecondaryOffsetY = secondaryPlacement == null ? 0.0f : secondaryPlacement.OffsetY;
            record.SecondaryRotationDegrees = secondaryPlacement == null ? 0.0f : NormaliseRotationDegrees(secondaryPlacement.RotationDegrees);
            record.SecondaryGuideColor = secondaryPlacement == null ? "Yellow" : (secondaryPlacement.GuideColorName ?? "Yellow");
            record.SecondaryReferenceWidth = secondaryPlacement == null ? 0 : secondaryPlacement.ReferenceWidth;
            record.SecondaryReferenceHeight = secondaryPlacement == null ? 0 : secondaryPlacement.ReferenceHeight;
            record.IconMode = iconMode ?? "Original Artwork";
            record.OutputRoot = outputRoot ?? "";
            record.LastBuiltOutputPath = buildResult.OutputPath ?? "";
            if (string.IsNullOrEmpty(record.CreatedUtc))
                record.CreatedUtc = DateTime.UtcNow.ToString("o");
            record.LastBuiltUtc = DateTime.UtcNow.ToString("o");
            record.LastEditedUtc = record.LastBuiltUtc;
            record.PendingChanges = false;
            record.LastBuildPackageMode = BuildPackageModes.Normalise(buildPackageMode);
            bool combinedPackage = BuildPackageModes.IsCombined(record.LastBuildPackageMode);
            record.LastBuiltFamilyKey = combinedPackage ? (builtFamilyKey ?? "") : "";
            record.LastBuiltFamilyName = combinedPackage ? (builtFamilyName ?? "") : "";
            record.LastBuiltFamilyMemberIds = new List<string>();
            if (combinedPackage && builtFamilyMemberIds != null)
            {
                HashSet<string> seenFamilyIds = new HashSet<string>(StringComparer.Ordinal);
                foreach (string memberId in builtFamilyMemberIds)
                {
                    if (!string.IsNullOrEmpty(memberId) && seenFamilyIds.Add(memberId))
                        record.LastBuiltFamilyMemberIds.Add(memberId);
                }
                record.LastBuiltFamilyMemberIds.Sort(StringComparer.Ordinal);
            }

            record.ArtworkFileName = CopySourceFile(sourceArtworkPath, projectDirectory, "artwork");
            if (!string.IsNullOrEmpty(secondaryArtworkPath) && File.Exists(secondaryArtworkPath))
                record.SecondaryArtworkFileName = CopySourceFile(secondaryArtworkPath, projectDirectory, "artwork_right");
            else if (string.IsNullOrEmpty(secondaryArtworkPath))
            {
                DeleteExistingNamedFiles(projectDirectory, "artwork_right");
                record.SecondaryArtworkFileName = "";
            }

            if (string.Equals(record.IconMode, "Custom Icon File", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrEmpty(sourceCustomIconPath) && File.Exists(sourceCustomIconPath))
            {
                record.CustomIconFileName = CopySourceFile(sourceCustomIconPath, projectDirectory, "custom_icon");
            }
            else
            {
                DeleteExistingNamedFiles(projectDirectory, "custom_icon");
                record.CustomIconFileName = "";
            }

            record.ProjectFormatVersion = CurrentProjectFormatVersion;
            JsonFile.Write(Path.Combine(projectDirectory, "project.json"), record);
            return record;
        }

        public ModProjectRecord SaveChanges(ModProjectRecord record, string modName, string description, string replacementArtworkPath)
        {
            return SaveChanges(record, modName, description, replacementArtworkPath,
                record == null ? 0 : record.ItemCost,
                record == null ? 0 : record.KudoshCost,
                record == null ? "" : record.Template,
                record == null ? VariantModes.Standalone : record.VariantMode,
                record == null ? "" : record.VariantParentModId);
        }

        public ModProjectRecord SaveChanges(ModProjectRecord record, string modName, string description, string replacementArtworkPath,
            int itemCost, int kudoshCost, string templateKey, string variantMode, string variantParentModId,
            string replacementSecondaryArtworkPath = null)
        {
            if (record == null || string.IsNullOrEmpty(record.ModId))
                throw new InvalidOperationException("The selected mod does not have a valid ItemModID.");

            string projectDirectory = GetProjectDirectory(record.ModId);
            Directory.CreateDirectory(projectDirectory);

            record.Name = modName ?? "";
            record.Description = description ?? "";
            record.ItemCost = Math.Max(0, itemCost);
            record.KudoshCost = Math.Max(0, kudoshCost);
            if (!string.IsNullOrEmpty(templateKey))
                record.Template = templateKey;
            ApplyVariantState(record, variantMode, variantParentModId);

            if (!string.IsNullOrEmpty(replacementArtworkPath))
            {
                if (!File.Exists(replacementArtworkPath))
                    throw new FileNotFoundException("The selected artwork image could not be found.", replacementArtworkPath);
                record.ArtworkFileName = CopySourceFile(replacementArtworkPath, projectDirectory, "artwork");
            }
            if (!string.IsNullOrEmpty(replacementSecondaryArtworkPath))
            {
                if (!File.Exists(replacementSecondaryArtworkPath))
                    throw new FileNotFoundException("The selected Right Banner artwork image could not be found.", replacementSecondaryArtworkPath);
                record.SecondaryArtworkFileName = CopySourceFile(replacementSecondaryArtworkPath, projectDirectory, "artwork_right");
            }

            record.LastEditedUtc = DateTime.UtcNow.ToString("o");
            record.PendingChanges = true;
            record.ProjectFormatVersion = CurrentProjectFormatVersion;
            JsonFile.Write(Path.Combine(projectDirectory, "project.json"), record);
            return record;
        }

        public string GetArtworkPath(ModProjectRecord record)
        {
            if (record == null || string.IsNullOrEmpty(record.ArtworkFileName))
                return "";
            return Path.Combine(GetProjectDirectory(record.ModId), record.ArtworkFileName);
        }

        public string GetSecondaryArtworkPath(ModProjectRecord record)
        {
            if (record == null || string.IsNullOrEmpty(record.SecondaryArtworkFileName))
                return "";
            return Path.Combine(GetProjectDirectory(record.ModId), record.SecondaryArtworkFileName);
        }

        public string GetCustomIconPath(ModProjectRecord record)
        {
            if (record == null || string.IsNullOrEmpty(record.CustomIconFileName))
                return "";
            return Path.Combine(GetProjectDirectory(record.ModId), record.CustomIconFileName);
        }

        public string GetWorkshopPreviewPath(ModProjectRecord record)
        {
            // Default every Workshop operation to the exact built ItemIcon used by the game
            // and shown in the My Mods Preview column. A saved Workshop preview is only a
            // fallback for older records whose built icon cannot be found.
            string builtIcon = GetBuiltItemIconPath(record);
            if (!string.IsNullOrEmpty(builtIcon))
                return builtIcon;

            if (record == null || string.IsNullOrEmpty(record.WorkshopPreviewFileName))
                return "";
            string savedPreview = Path.Combine(GetProjectDirectory(record.ModId), record.WorkshopPreviewFileName);
            return File.Exists(savedPreview) ? savedPreview : "";
        }

        public string GetBuiltItemIconPath(ModProjectRecord record)
        {
            if (record == null || string.IsNullOrEmpty(record.LastBuiltOutputPath) || !Directory.Exists(record.LastBuiltOutputPath))
                return "";

            string direct = Path.Combine(record.LastBuiltOutputPath, "Icons", "ItemIcon.png");
            if (File.Exists(direct))
                return direct;

            try
            {
                string[] matches = Directory.GetFiles(record.LastBuiltOutputPath, "ItemIcon.png", SearchOption.AllDirectories);
                if (matches.Length > 0)
                    return matches[0];
            }
            catch { }

            return "";
        }

        public ModProjectRecord SaveWorkshopState(ModProjectRecord record, WorkshopPublishJob job, WorkshopPublishResult result, string sourcePreviewPath)
        {
            if (record == null || string.IsNullOrEmpty(record.ModId))
                throw new InvalidOperationException("The selected mod does not have a valid ItemModID.");
            if (job == null || result == null || !result.Success || string.IsNullOrEmpty(result.PublishedFileId))
                throw new InvalidOperationException("A successful Steam Workshop result is required before the project can be updated.");

            EnsureWorkshopItemAvailableForProject(result.PublishedFileId, record.ModId);

            string projectDirectory = GetProjectDirectory(record.ModId);
            Directory.CreateDirectory(projectDirectory);
            record.ProjectVersion = Math.Max(record.ProjectVersion, 3);
            if (!string.IsNullOrEmpty(record.WorkshopPublishedFileId) &&
                !string.Equals(record.WorkshopPublishedFileId, result.PublishedFileId, StringComparison.Ordinal))
                record.WorkshopPreviousPublishedFileId = record.WorkshopPublishedFileId;
            record.WorkshopPublishedFileId = result.PublishedFileId;
            record.WorkshopPackageMode = BuildPackageModes.Single;
            record.WorkshopFamilyKey = "";
            record.WorkshopFamilyName = "";
            record.WorkshopFamilyMemberIds = new List<string>();
            record.WorkshopLinkState = "Linked";
            record.WorkshopLinkNeedsUpdate = false;
            record.WorkshopLinkLastCheckedUtc = DateTime.UtcNow.ToString("o");
            record.WorkshopTitle = job.Title ?? "";
            record.WorkshopDescription = job.Description ?? "";
            record.WorkshopVisibility = string.IsNullOrEmpty(job.Visibility) ? "Private" : job.Visibility;
            record.WorkshopLastChangeNote = job.ChangeNote ?? "";
            record.WorkshopNeedsLegalAgreement = result.NeedsLegalAgreement;
            // Store the dependency Steam actually confirmed. This prevents a failed or
            // dropped dependency request from being remembered locally as successful.
            record.WorkshopDependencyPublishedFileId = result.DependencyPublishedFileId ?? "";
            record.WorkshopLastUpdatedUtc = DateTime.UtcNow.ToString("o");
            if (result.CreatedNew || string.IsNullOrEmpty(record.WorkshopLastPublishedUtc))
                record.WorkshopLastPublishedUtc = record.WorkshopLastUpdatedUtc;

            if (!string.IsNullOrEmpty(sourcePreviewPath) && File.Exists(sourcePreviewPath))
                record.WorkshopPreviewFileName = CopySourceFile(sourcePreviewPath, projectDirectory, "workshop_preview");

            record.ProjectFormatVersion = CurrentProjectFormatVersion;
            JsonFile.Write(Path.Combine(projectDirectory, "project.json"), record);
            return record;
        }

        private string GetWorkshopFamilyStatePath(string familyKey)
        {
            string raw = familyKey ?? "";
            string encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(raw))
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
            if (string.IsNullOrEmpty(encoded)) encoded = "family";
            return Path.Combine(_familyWorkshopRoot, encoded + ".json");
        }

        public WorkshopFamilyState LoadWorkshopFamilyState(string familyKey)
        {
            if (string.IsNullOrEmpty(familyKey)) return null;
            try
            {
                WorkshopFamilyState state = JsonFile.Read<WorkshopFamilyState>(GetWorkshopFamilyStatePath(familyKey));
                if (state == null || !string.Equals(state.FamilyKey ?? "", familyKey, StringComparison.Ordinal))
                    return null;
                if (state.MemberIds == null) state.MemberIds = new List<string>();
                if (state.LinkState == null) state.LinkState = "Unlinked";
                state.PackageMode = BuildPackageModes.Normalise(state.PackageMode);
                if (!BuildPackageModes.IsCombined(state.PackageMode))
                    state.PackageMode = (state.FamilyKey ?? "").StartsWith("decorpack:", StringComparison.OrdinalIgnoreCase)
                        ? BuildPackageModes.DecorPack
                        : BuildPackageModes.Family;
                return state;
            }
            catch
            {
                return null;
            }
        }

        private void SaveWorkshopFamilyRegistry(WorkshopFamilyState state)
        {
            if (state == null || string.IsNullOrEmpty(state.FamilyKey)) return;
            state.FormatVersion = 1;
            if (state.MemberIds == null) state.MemberIds = new List<string>();
            state.PackageMode = BuildPackageModes.Normalise(state.PackageMode);
            if (!BuildPackageModes.IsCombined(state.PackageMode))
                state.PackageMode = (state.FamilyKey ?? "").StartsWith("decorpack:", StringComparison.OrdinalIgnoreCase)
                    ? BuildPackageModes.DecorPack
                    : BuildPackageModes.Family;
            JsonFile.Write(GetWorkshopFamilyStatePath(state.FamilyKey), state);
        }

        private void DeleteWorkshopFamilyRegistry(string familyKey)
        {
            if (string.IsNullOrEmpty(familyKey)) return;
            string path = GetWorkshopFamilyStatePath(familyKey);
            JsonFile.DeleteWithBackup(path);
        }

        private static string ResolveCombinedPackageMode(IEnumerable<ModProjectRecord> members)
        {
            if (members != null)
            {
                foreach (ModProjectRecord member in members)
                {
                    if (member == null) continue;
                    string mode = BuildPackageModes.Normalise(member.LastBuildPackageMode);
                    if (BuildPackageModes.IsCombined(mode)) return mode;
                    mode = BuildPackageModes.Normalise(member.WorkshopPackageMode);
                    if (BuildPackageModes.IsCombined(mode)) return mode;
                }
            }
            return BuildPackageModes.Family;
        }

        private static string CombinedPackageDefaultName(string packageMode)
        {
            return BuildPackageModes.Normalise(packageMode) == BuildPackageModes.DecorPack ? "Wallpaper Pack" : "Variant Family";
        }

        // The family registry is the authoritative Workshop identity. Project records remain mirrors
        // for backwards compatibility, but a stale/missing mirror must never hide a published family.
        public List<ModProjectRecord> SynchroniseWorkshopFamilyStateToMembers(IEnumerable<ModProjectRecord> members, string familyKey)
        {
            List<ModProjectRecord> saved = new List<ModProjectRecord>();
            WorkshopFamilyState state = LoadWorkshopFamilyState(familyKey);
            if (state == null || string.IsNullOrEmpty(state.PublishedFileId) || members == null)
                return saved;

            HashSet<string> currentIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (ModProjectRecord member in members)
                if (member != null && !string.IsNullOrEmpty(member.ModId)) currentIds.Add(member.ModId);

            foreach (string modId in currentIds)
            {
                ModProjectRecord record = JsonFile.Read<ModProjectRecord>(Path.Combine(GetProjectDirectory(modId), "project.json"));
                if (record == null) continue;
                NormaliseFormatVersion(record);

                // Only repair records that are actually part of this combined local package.
                string packageMode = BuildPackageModes.Normalise(record.LastBuildPackageMode);
                if (!BuildPackageModes.IsCombined(packageMode) || packageMode != BuildPackageModes.Normalise(state.PackageMode) ||
                    !string.Equals(record.LastBuiltFamilyKey ?? "", familyKey ?? "", StringComparison.Ordinal))
                    continue;

                record.WorkshopPublishedFileId = state.PublishedFileId ?? "";
                record.WorkshopPreviousPublishedFileId = state.PreviousPublishedFileId ?? "";
                record.WorkshopPackageMode = packageMode;
                record.WorkshopFamilyKey = state.FamilyKey ?? familyKey ?? "";
                record.WorkshopFamilyName = state.FamilyName ?? CombinedPackageDefaultName(packageMode);
                record.WorkshopFamilyMemberIds = state.MemberIds == null ? new List<string>() : new List<string>(state.MemberIds);
                record.WorkshopLinkState = state.LinkState ?? "Linked";
                record.WorkshopLinkNeedsUpdate = state.LinkNeedsUpdate;
                record.WorkshopTitle = state.Title ?? "";
                record.WorkshopDescription = state.Description ?? "";
                record.WorkshopVisibility = state.Visibility ?? "";
                record.WorkshopLastChangeNote = state.LastChangeNote ?? "";
                record.WorkshopLastPublishedUtc = state.LastPublishedUtc ?? "";
                record.WorkshopLastUpdatedUtc = state.LastUpdatedUtc ?? "";
                record.WorkshopLinkLastCheckedUtc = state.LastCheckedUtc ?? "";
                record.WorkshopNeedsLegalAgreement = state.NeedsLegalAgreement;
                record.WorkshopDependencyPublishedFileId = "";
                WriteProject(record);
                saved.Add(record);
            }
            return saved;
        }

        private void ApplyWorkshopFamilyRegistry(ModProjectRecord record)
        {
            if (record == null || !BuildPackageModes.IsCombined(record.LastBuildPackageMode) ||
                string.IsNullOrEmpty(record.LastBuiltFamilyKey))
                return;

            WorkshopFamilyState state = LoadWorkshopFamilyState(record.LastBuiltFamilyKey);
            string packageMode = BuildPackageModes.Normalise(record.LastBuildPackageMode);
            if (state == null || string.IsNullOrEmpty(state.PublishedFileId) ||
                BuildPackageModes.Normalise(state.PackageMode) != packageMode)
                return;

            record.WorkshopPublishedFileId = state.PublishedFileId ?? "";
            record.WorkshopPreviousPublishedFileId = state.PreviousPublishedFileId ?? "";
            record.WorkshopPackageMode = packageMode;
            record.WorkshopFamilyKey = state.FamilyKey ?? "";
            record.WorkshopFamilyName = state.FamilyName ?? CombinedPackageDefaultName(packageMode);
            record.WorkshopFamilyMemberIds = state.MemberIds == null ? new List<string>() : new List<string>(state.MemberIds);
            record.WorkshopLinkState = state.LinkState ?? "Linked";
            record.WorkshopLinkNeedsUpdate = state.LinkNeedsUpdate;
            record.WorkshopTitle = state.Title ?? "";
            record.WorkshopDescription = state.Description ?? "";
            record.WorkshopVisibility = state.Visibility ?? "";
            record.WorkshopLastChangeNote = state.LastChangeNote ?? "";
            record.WorkshopLastPublishedUtc = state.LastPublishedUtc ?? "";
            record.WorkshopLastUpdatedUtc = state.LastUpdatedUtc ?? "";
            record.WorkshopLinkLastCheckedUtc = state.LastCheckedUtc ?? "";
            record.WorkshopNeedsLegalAgreement = state.NeedsLegalAgreement;
        }

        private static string NormalisePathForComparison(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return "";
            try
            {
                string full = Path.GetFullPath(path.Trim());
                return full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
            catch
            {
                return path.Trim().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
        }

        public bool TryRecoverWorkshopFamilyStateFromLocalPublishHistory(IEnumerable<ModProjectRecord> members, string familyKey, string familyName)
        {
            if (members == null || string.IsNullOrEmpty(familyKey))
                return false;

            string packageMode = ResolveCombinedPackageMode(members);
            WorkshopFamilyState existing = LoadWorkshopFamilyState(familyKey);
            if (existing != null && !string.IsNullOrEmpty(existing.PublishedFileId) &&
                BuildPackageModes.Normalise(existing.PackageMode) == packageMode)
                return true;

            // Preserve legacy one-by-one Workshop identities so recovery does not accidentally
            // adopt an older parent/child page that happens to share the family title.
            HashSet<string> legacyMemberWorkshopIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (ModProjectRecord member in members)
            {
                if (member == null || string.IsNullOrEmpty(member.ModId)) continue;
                ModProjectRecord raw = JsonFile.Read<ModProjectRecord>(Path.Combine(GetProjectDirectory(member.ModId), "project.json"));
                if (raw == null) continue;
                if (!string.IsNullOrEmpty(raw.WorkshopPreviousPublishedFileId))
                    legacyMemberWorkshopIds.Add(raw.WorkshopPreviousPublishedFileId);
                if (!string.IsNullOrEmpty(raw.WorkshopPublishedFileId) &&
                    !string.Equals(raw.WorkshopFamilyKey ?? "", familyKey ?? "", StringComparison.Ordinal))
                    legacyMemberWorkshopIds.Add(raw.WorkshopPublishedFileId);
            }

            // Also remember the currently installed shared family output path. Early BL-020
            // previews could successfully upload the family but fail to persist the local family
            // registry. The publish job still records the exact content folder Steam received,
            // which is a stronger recovery key than a mutable title and survives family-key
            // compatibility changes between previews.
            HashSet<string> currentFamilyOutputPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (ModProjectRecord member in members)
            {
                if (member == null || string.IsNullOrEmpty(member.ModId)) continue;
                ModProjectRecord current = Load(member.ModId) ?? member;
                if (BuildPackageModes.Normalise(current.LastBuildPackageMode) != packageMode ||
                    !string.Equals(current.LastBuiltFamilyKey ?? "", familyKey ?? "", StringComparison.Ordinal))
                    continue;
                string normalised = NormalisePathForComparison(current.LastBuiltOutputPath);
                if (!string.IsNullOrEmpty(normalised)) currentFamilyOutputPaths.Add(normalised);
            }

            string jobsRoot = Path.Combine(AppInfo.LocalDataRoot, "Jobs");
            if (!Directory.Exists(jobsRoot))
                return false;

            string expectedMementoId = packageMode == BuildPackageModes.DecorPack ? familyKey : "family:" + familyKey;
            string[] directories;
            try
            {
                directories = Directory.GetDirectories(jobsRoot, "WorkshopPublish_*");
            }
            catch
            {
                return false;
            }

            Array.Sort(directories, delegate(string a, string b)
            {
                DateTime ad = DateTime.MinValue;
                DateTime bd = DateTime.MinValue;
                try { ad = Directory.GetLastWriteTimeUtc(a); } catch { }
                try { bd = Directory.GetLastWriteTimeUtc(b); } catch { }
                return bd.CompareTo(ad);
            });

            for (int i = 0; i < directories.Length; i++)
            {
                string directory = directories[i];
                string jobPath = Path.Combine(directory, "workshop_publish.job.json");
                string resultPath = Path.Combine(directory, "workshop_publish.result.json");
                if (!File.Exists(jobPath) || !File.Exists(resultPath))
                    continue;

                WorkshopPublishJob job;
                WorkshopPublishResult result;
                try
                {
                    job = JsonFile.Read<WorkshopPublishJob>(jobPath);
                    result = JsonFile.Read<WorkshopPublishResult>(resultPath);
                }
                catch
                {
                    continue;
                }

                if (job == null || result == null || !result.Success || string.IsNullOrEmpty(result.PublishedFileId))
                    continue;

                bool exactFamilyIdentity = string.Equals(job.MementoModId ?? "", expectedMementoId, StringComparison.Ordinal);
                bool matchingFamilyOutput = false;
                string jobOutput = NormalisePathForComparison(job.ContentPath);
                if (!string.IsNullOrEmpty(jobOutput) && currentFamilyOutputPaths.Contains(jobOutput) &&
                    !legacyMemberWorkshopIds.Contains(result.PublishedFileId))
                    matchingFamilyOutput = true;

                bool compatibleFamilyHistory = packageMode == BuildPackageModes.Family && !exactFamilyIdentity && !matchingFamilyOutput &&
                    (job.MementoModId ?? "").StartsWith("family:", StringComparison.Ordinal) &&
                    string.Equals((job.Title ?? "").Trim(), (familyName ?? "").Trim(), StringComparison.OrdinalIgnoreCase) &&
                    !legacyMemberWorkshopIds.Contains(result.PublishedFileId);
                if (!exactFamilyIdentity && !matchingFamilyOutput && !compatibleFamilyHistory)
                    continue;

                try
                {
                    SaveWorkshopFamilyState(members, familyKey, familyName, job, result, job.PreviewPath);
                    return true;
                }
                catch
                {
                    // Continue looking through older successful family publish jobs. A later job
                    // can be incomplete if the desktop process was interrupted after Steam returned.
                }
            }

            return false;
        }

        public bool TryRecoverWorkshopFamilyStateFromQuery(IEnumerable<ModProjectRecord> members, string familyKey, string familyName, WorkshopQueryResult query)
        {
            if (query == null || !query.Success || query.Items == null || string.IsNullOrEmpty(familyKey) || members == null)
                return false;

            string packageMode = ResolveCombinedPackageMode(members);
            WorkshopFamilyState existing = LoadWorkshopFamilyState(familyKey);
            if (existing != null && !string.IsNullOrEmpty(existing.PublishedFileId) &&
                BuildPackageModes.Normalise(existing.PackageMode) == packageMode)
                return true;

            HashSet<string> familyIds = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> legacyMemberWorkshopIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (ModProjectRecord member in members)
            {
                if (member == null || string.IsNullOrEmpty(member.ModId)) continue;
                familyIds.Add(member.ModId);
                ModProjectRecord raw = JsonFile.Read<ModProjectRecord>(Path.Combine(GetProjectDirectory(member.ModId), "project.json"));
                if (raw == null) continue;
                if (!string.IsNullOrEmpty(raw.WorkshopPreviousPublishedFileId))
                    legacyMemberWorkshopIds.Add(raw.WorkshopPreviousPublishedFileId);
                if (!string.IsNullOrEmpty(raw.WorkshopPublishedFileId) &&
                    !string.Equals(raw.WorkshopFamilyKey ?? "", familyKey ?? "", StringComparison.Ordinal))
                    legacyMemberWorkshopIds.Add(raw.WorkshopPublishedFileId);
            }

            List<WorkshopItemSummary> candidates = FilterWorkshopItemsAvailableForFamily(query.Items, familyIds);
            WorkshopItemSummary match = null;
            int matchCount = 0;

            string mementoId = packageMode == BuildPackageModes.DecorPack ? familyKey : "family:" + (familyKey ?? "");
            string expectedMetadata = "MementoMakerModId=" + mementoId;
            for (int i = 0; i < candidates.Count; i++)
            {
                WorkshopItemSummary item = candidates[i];
                if (item == null || string.IsNullOrEmpty(item.PublishedFileId) || legacyMemberWorkshopIds.Contains(item.PublishedFileId))
                    continue;
                if (!string.Equals((item.Metadata ?? "").Trim(), expectedMetadata, StringComparison.Ordinal))
                    continue;
                match = item;
                matchCount++;
            }

            // Compatibility fallback: if metadata is missing, require one exact title match after
            // excluding known legacy per-item Workshop pages.
            if (matchCount == 0)
            {
                string wantedTitle = (familyName ?? "").Trim();
                for (int i = 0; i < candidates.Count; i++)
                {
                    WorkshopItemSummary item = candidates[i];
                    if (item == null || string.IsNullOrEmpty(item.PublishedFileId) || legacyMemberWorkshopIds.Contains(item.PublishedFileId))
                        continue;
                    if (!string.Equals((item.Title ?? "").Trim(), wantedTitle, StringComparison.OrdinalIgnoreCase))
                        continue;
                    match = item;
                    matchCount++;
                }
            }
            if (matchCount != 1 || match == null)
                return false;

            List<string> snapshot = new List<string>(familyIds);
            snapshot.Sort(StringComparer.Ordinal);
            string now = DateTime.UtcNow.ToString("o");
            WorkshopFamilyState state = new WorkshopFamilyState();
            state.FamilyKey = familyKey;
            state.FamilyName = string.IsNullOrWhiteSpace(familyName) ? CombinedPackageDefaultName(packageMode) : familyName.Trim();
            state.PackageMode = packageMode;
            state.PublishedFileId = match.PublishedFileId;
            state.MemberIds = snapshot;
            state.LinkState = "Linked";
            state.LinkNeedsUpdate = false;
            state.Title = match.Title ?? state.FamilyName;
            state.Visibility = match.Visibility ?? "Private";
            state.LastPublishedUtc = now;
            state.LastUpdatedUtc = now;
            state.LastCheckedUtc = now;
            SaveWorkshopFamilyRegistry(state);

            foreach (string modId in familyIds)
            {
                ModProjectRecord record = JsonFile.Read<ModProjectRecord>(Path.Combine(GetProjectDirectory(modId), "project.json"));
                if (record == null) continue;
                NormaliseFormatVersion(record);
                record.WorkshopPublishedFileId = state.PublishedFileId;
                record.WorkshopPackageMode = packageMode;
                record.WorkshopFamilyKey = state.FamilyKey;
                record.WorkshopFamilyName = state.FamilyName;
                record.WorkshopFamilyMemberIds = new List<string>(snapshot);
                record.WorkshopLinkState = "Linked";
                record.WorkshopLinkNeedsUpdate = false;
                record.WorkshopTitle = state.Title;
                record.WorkshopVisibility = state.Visibility;
                record.WorkshopLastPublishedUtc = now;
                record.WorkshopLastUpdatedUtc = now;
                record.WorkshopLinkLastCheckedUtc = now;
                WriteProject(record);
            }
            return true;
        }

        public List<ModProjectRecord> SaveWorkshopFamilyState(IEnumerable<ModProjectRecord> members, string familyKey, string familyName,
            WorkshopPublishJob job, WorkshopPublishResult result, string sourcePreviewPath)
        {
            List<ModProjectRecord> saved = new List<ModProjectRecord>();
            if (members == null)
                return saved;
            if (job == null || result == null || !result.Success || string.IsNullOrEmpty(result.PublishedFileId))
                throw new InvalidOperationException("A successful Steam Workshop result is required before the family can be updated.");

            HashSet<string> memberIds = new HashSet<string>(StringComparer.Ordinal);
            List<ModProjectRecord> memberList = new List<ModProjectRecord>();
            foreach (ModProjectRecord member in members)
            {
                if (member == null || string.IsNullOrEmpty(member.ModId) || !memberIds.Add(member.ModId))
                    continue;
                memberList.Add(Load(member.ModId) ?? member);
            }
            if (memberList.Count == 0)
                throw new InvalidOperationException("The combined package does not contain any saved Memento Maker projects.");

            string packageMode = ResolveCombinedPackageMode(memberList);
            EnsureWorkshopItemAvailableForFamily(result.PublishedFileId, memberIds);
            List<string> snapshot = new List<string>(memberIds);
            snapshot.Sort(StringComparer.Ordinal);
            string now = DateTime.UtcNow.ToString("o");

            WorkshopFamilyState familyState = new WorkshopFamilyState();
            familyState.FamilyKey = familyKey ?? "";
            familyState.FamilyName = string.IsNullOrWhiteSpace(familyName) ? CombinedPackageDefaultName(packageMode) : familyName;
            familyState.PackageMode = packageMode;
            familyState.PublishedFileId = result.PublishedFileId;
            familyState.MemberIds = new List<string>(snapshot);
            familyState.LinkState = "Linked";
            familyState.LinkNeedsUpdate = false;
            familyState.Title = job.Title ?? "";
            familyState.Description = job.Description ?? "";
            familyState.Visibility = string.IsNullOrEmpty(job.Visibility) ? "Private" : job.Visibility;
            familyState.LastChangeNote = job.ChangeNote ?? "";
            familyState.LastUpdatedUtc = now;
            familyState.LastPublishedUtc = now;
            familyState.LastCheckedUtc = now;
            familyState.NeedsLegalAgreement = result.NeedsLegalAgreement;
            SaveWorkshopFamilyRegistry(familyState);
            WorkshopFamilyState persistedFamilyState = LoadWorkshopFamilyState(familyState.FamilyKey);
            if (persistedFamilyState == null ||
                !string.Equals(persistedFamilyState.PublishedFileId ?? "", familyState.PublishedFileId ?? "", StringComparison.Ordinal))
                throw new IOException("The family Workshop upload succeeded, but Memento Maker could not persist the shared Workshop identity locally. No second upload is required; retry the family link/refresh after resolving local-data write access.");

            for (int i = 0; i < memberList.Count; i++)
            {
                ModProjectRecord record = memberList[i];
                string oldId = record.WorkshopPublishedFileId ?? "";
                if (!string.IsNullOrEmpty(oldId) && !string.Equals(oldId, result.PublishedFileId, StringComparison.Ordinal))
                    record.WorkshopPreviousPublishedFileId = oldId;

                record.WorkshopPublishedFileId = result.PublishedFileId;
                record.WorkshopPackageMode = packageMode;
                record.WorkshopFamilyKey = familyKey ?? "";
                record.WorkshopFamilyName = string.IsNullOrWhiteSpace(familyName) ? CombinedPackageDefaultName(packageMode) : familyName;
                record.WorkshopFamilyMemberIds = new List<string>(snapshot);
                record.WorkshopLinkState = "Linked";
                record.WorkshopLinkNeedsUpdate = false;
                record.WorkshopLinkLastCheckedUtc = now;
                record.WorkshopTitle = job.Title ?? "";
                record.WorkshopDescription = job.Description ?? "";
                record.WorkshopVisibility = string.IsNullOrEmpty(job.Visibility) ? "Private" : job.Visibility;
                record.WorkshopLastChangeNote = job.ChangeNote ?? "";
                record.WorkshopNeedsLegalAgreement = result.NeedsLegalAgreement;
                // Internal parent/child relationships are contained in the same combined package,
                // so a family Workshop item never needs another family member as a Steam dependency.
                record.WorkshopDependencyPublishedFileId = "";
                record.WorkshopLastUpdatedUtc = now;
                if (result.CreatedNew || string.IsNullOrEmpty(record.WorkshopLastPublishedUtc))
                    record.WorkshopLastPublishedUtc = now;
                if (!string.IsNullOrEmpty(sourcePreviewPath) && File.Exists(sourcePreviewPath))
                {
                    string projectDirectory = GetProjectDirectory(record.ModId);
                    Directory.CreateDirectory(projectDirectory);
                    record.WorkshopPreviewFileName = CopySourceFile(sourcePreviewPath, projectDirectory, "workshop_preview");
                }
                record.ProjectVersion = Math.Max(record.ProjectVersion, 5);
                record.ProjectFormatVersion = CurrentProjectFormatVersion;
                WriteProject(record);
                saved.Add(record);
            }
            return saved;
        }

        public void EnsureWorkshopItemAvailableForFamily(string publishedFileId, IEnumerable<string> familyMemberIds)
        {
            if (string.IsNullOrEmpty(publishedFileId))
                return;
            HashSet<string> allowed = new HashSet<string>(StringComparer.Ordinal);
            if (familyMemberIds != null)
                foreach (string id in familyMemberIds)
                    if (!string.IsNullOrEmpty(id)) allowed.Add(id);

            List<ModProjectRecord> records = LoadAll();
            for (int i = 0; i < records.Count; i++)
            {
                ModProjectRecord candidate = records[i];
                if (candidate == null || string.IsNullOrEmpty(candidate.ModId) || allowed.Contains(candidate.ModId))
                    continue;
                if (!string.Equals(candidate.WorkshopPublishedFileId ?? "", publishedFileId, StringComparison.Ordinal))
                    continue;
                string ownerName = string.IsNullOrEmpty(candidate.Name) ? candidate.ModId : candidate.Name;
                throw new InvalidOperationException(
                    "Workshop item " + publishedFileId + " is already linked to Memento Maker mod '" + ownerName + "' (" + candidate.ModId + "). " +
                    "It cannot also become the Workshop item for this variant family. Unlink it from that mod first if you want to move the association.");
            }
        }

        public List<WorkshopItemSummary> FilterWorkshopItemsAvailableForFamily(List<WorkshopItemSummary> items, IEnumerable<string> familyMemberIds)
        {
            List<WorkshopItemSummary> available = new List<WorkshopItemSummary>();
            if (items == null)
                return available;
            HashSet<string> allowed = new HashSet<string>(StringComparer.Ordinal);
            if (familyMemberIds != null)
                foreach (string id in familyMemberIds)
                    if (!string.IsNullOrEmpty(id)) allowed.Add(id);
            HashSet<string> blocked = new HashSet<string>(StringComparer.Ordinal);
            List<ModProjectRecord> records = LoadAll();
            for (int i = 0; i < records.Count; i++)
            {
                ModProjectRecord candidate = records[i];
                if (candidate == null || string.IsNullOrEmpty(candidate.ModId) || allowed.Contains(candidate.ModId))
                    continue;
                if (!string.IsNullOrEmpty(candidate.WorkshopPublishedFileId))
                    blocked.Add(candidate.WorkshopPublishedFileId);
            }
            for (int i = 0; i < items.Count; i++)
            {
                WorkshopItemSummary item = items[i];
                if (item != null && !string.IsNullOrEmpty(item.PublishedFileId) && !blocked.Contains(item.PublishedFileId))
                    available.Add(item);
            }
            return available;
        }

        public List<ModProjectRecord> RelinkWorkshopFamilyItem(IEnumerable<ModProjectRecord> members, string familyKey, string familyName, WorkshopItemSummary item)
        {
            if (item == null || string.IsNullOrEmpty(item.PublishedFileId))
                throw new InvalidOperationException("Select a valid Steam Workshop item to relink.");
            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            List<ModProjectRecord> list = new List<ModProjectRecord>();
            if (members != null)
            {
                foreach (ModProjectRecord member in members)
                {
                    if (member == null || string.IsNullOrEmpty(member.ModId) || !ids.Add(member.ModId)) continue;
                    list.Add(Load(member.ModId) ?? member);
                }
            }
            string packageMode = ResolveCombinedPackageMode(list);
            EnsureWorkshopItemAvailableForFamily(item.PublishedFileId, ids);
            List<string> snapshot = new List<string>(ids);
            snapshot.Sort(StringComparer.Ordinal);
            string now = DateTime.UtcNow.ToString("o");
            WorkshopFamilyState familyState = new WorkshopFamilyState();
            familyState.FamilyKey = familyKey ?? "";
            familyState.FamilyName = string.IsNullOrWhiteSpace(familyName) ? CombinedPackageDefaultName(packageMode) : familyName;
            familyState.PackageMode = packageMode;
            familyState.PublishedFileId = item.PublishedFileId;
            familyState.MemberIds = new List<string>(snapshot);
            familyState.LinkState = "Linked";
            familyState.LinkNeedsUpdate = true;
            familyState.Title = item.Title ?? "";
            familyState.Visibility = item.Visibility ?? "Private";
            familyState.LastCheckedUtc = now;
            SaveWorkshopFamilyRegistry(familyState);
            for (int i = 0; i < list.Count; i++)
            {
                ModProjectRecord record = list[i];
                string oldId = record.WorkshopPublishedFileId ?? "";
                if (!string.IsNullOrEmpty(oldId) && !string.Equals(oldId, item.PublishedFileId, StringComparison.Ordinal))
                    record.WorkshopPreviousPublishedFileId = oldId;
                record.WorkshopPublishedFileId = item.PublishedFileId;
                record.WorkshopPackageMode = packageMode;
                record.WorkshopFamilyKey = familyKey ?? "";
                record.WorkshopFamilyName = string.IsNullOrWhiteSpace(familyName) ? CombinedPackageDefaultName(packageMode) : familyName;
                record.WorkshopFamilyMemberIds = new List<string>(snapshot);
                record.WorkshopLinkState = "Linked";
                record.WorkshopLinkNeedsUpdate = true;
                record.WorkshopLinkLastCheckedUtc = now;
                if (!string.IsNullOrEmpty(item.Title)) record.WorkshopTitle = item.Title;
                if (!string.IsNullOrEmpty(item.Visibility)) record.WorkshopVisibility = item.Visibility;
                record.WorkshopDependencyPublishedFileId = "";
                record.ProjectVersion = Math.Max(record.ProjectVersion, 5);
                WriteProject(record);
            }
            return list;
        }

        public List<ModProjectRecord> UnlinkWorkshopFamilyItem(IEnumerable<ModProjectRecord> members, string familyKey)
        {
            WorkshopFamilyState registry = LoadWorkshopFamilyState(familyKey);
            string registryPublishedId = registry == null ? "" : (registry.PublishedFileId ?? "");

            HashSet<string> targetMemberIds = new HashSet<string>(StringComparer.Ordinal);
            if (registry != null && registry.MemberIds != null)
            {
                for (int i = 0; i < registry.MemberIds.Count; i++)
                {
                    string memberId = registry.MemberIds[i];
                    if (!string.IsNullOrEmpty(memberId))
                        targetMemberIds.Add(memberId);
                }
            }
            if (members != null)
            {
                foreach (ModProjectRecord member in members)
                {
                    if (member != null && !string.IsNullOrEmpty(member.ModId))
                        targetMemberIds.Add(member.ModId);
                }
            }

            List<ModProjectRecord> result = new List<ModProjectRecord>();
            List<ModProjectRecord> allRecords = LoadAll();
            string now = DateTime.UtcNow.ToString("o");
            for (int i = 0; i < allRecords.Count; i++)
            {
                ModProjectRecord record = allRecords[i];
                if (record == null || string.IsNullOrEmpty(record.ModId))
                    continue;

                bool matchesFamilyKey = !string.IsNullOrEmpty(familyKey) &&
                    string.Equals(record.WorkshopFamilyKey ?? "", familyKey ?? "", StringComparison.Ordinal);
                bool matchesMember = targetMemberIds.Contains(record.ModId);
                bool matchesSharedId = !string.IsNullOrEmpty(registryPublishedId) &&
                    string.Equals(record.WorkshopPublishedFileId ?? "", registryPublishedId, StringComparison.Ordinal) &&
                    string.Equals(record.LastBuiltFamilyKey ?? "", familyKey ?? "", StringComparison.Ordinal);

                if (!matchesFamilyKey && !matchesMember && !matchesSharedId)
                    continue;

                string linkedId = record.WorkshopPublishedFileId ?? "";
                if (string.IsNullOrEmpty(linkedId))
                    linkedId = registryPublishedId;
                if (!string.IsNullOrEmpty(linkedId))
                    record.WorkshopPreviousPublishedFileId = linkedId;

                record.WorkshopPublishedFileId = "";
                record.WorkshopLinkState = "Unlinked";
                record.WorkshopLinkNeedsUpdate = false;
                record.WorkshopLinkLastCheckedUtc = now;
                record.WorkshopPackageMode = BuildPackageModes.Single;
                record.WorkshopFamilyKey = "";
                record.WorkshopFamilyName = "";
                record.WorkshopFamilyMemberIds = new List<string>();
                record.ProjectVersion = Math.Max(record.ProjectVersion, 5);
                WriteProject(record);
                result.Add(record);
            }

            // The normal atomic writer keeps the previous generation as .bak. For an
            // explicit unlink, do not leave a recovery generation that still contains the
            // old active Workshop ID: if a project primary were later damaged, restoring
            // that backup could otherwise reintroduce a link the user deliberately removed.
            for (int i = 0; i < result.Count; i++)
            {
                ModProjectRecord unlinked = result[i];
                if (unlinked == null || string.IsNullOrEmpty(unlinked.ModId)) continue;
                JsonFile.SynchroniseBackup(Path.Combine(GetProjectDirectory(unlinked.ModId), "project.json"));
            }

            // Delete the shared registry only after all member mirrors and their recovery
            // backups were cleared successfully. DeleteWithBackup removes both registry
            // generations, preventing resilient JSON recovery from resurrecting the link.
            DeleteWorkshopFamilyRegistry(familyKey);
            return result;
        }

        public bool UpdateWorkshopFamilyLinkFromQuery(IEnumerable<ModProjectRecord> members, string familyKey, WorkshopQueryResult query)
        {
            if (query == null || !query.Success || members == null) return false;
            string packageMode = ResolveCombinedPackageMode(members);
            WorkshopFamilyState state = LoadWorkshopFamilyState(familyKey);
            List<ModProjectRecord> list = new List<ModProjectRecord>();
            foreach (ModProjectRecord member in members)
            {
                if (member == null || string.IsNullOrEmpty(member.ModId)) continue;
                ModProjectRecord record = JsonFile.Read<ModProjectRecord>(Path.Combine(GetProjectDirectory(member.ModId), "project.json"));
                if (record == null) record = member;
                NormaliseFormatVersion(record);
                list.Add(record);
            }
            if (list.Count == 0) return false;

            string publishedId = state == null ? "" : (state.PublishedFileId ?? "");
            if (string.IsNullOrEmpty(publishedId))
            {
                for (int i = 0; i < list.Count; i++)
                {
                    if (BuildPackageModes.Normalise(list[i].WorkshopPackageMode) == packageMode &&
                        string.Equals(list[i].WorkshopFamilyKey ?? "", familyKey ?? "", StringComparison.Ordinal) &&
                        !string.IsNullOrEmpty(list[i].WorkshopPublishedFileId))
                    {
                        publishedId = list[i].WorkshopPublishedFileId;
                        break;
                    }
                }
            }
            if (string.IsNullOrEmpty(publishedId)) return false;

            WorkshopItemSummary found = FindWorkshopItem(query.Items, publishedId);
            string now = DateTime.UtcNow.ToString("o");
            bool recentSuccessfulPublish = false;
            DateTime updated = state == null ? DateTime.MinValue : ParseDate(state.LastUpdatedUtc);
            if (updated != DateTime.MinValue)
                recentSuccessfulPublish = (DateTime.UtcNow - updated.ToUniversalTime()) <= TimeSpan.FromMinutes(15);

            if (state == null)
            {
                state = new WorkshopFamilyState();
                state.FamilyKey = familyKey ?? "";
                state.FamilyName = string.IsNullOrWhiteSpace(list[0].LastBuiltFamilyName) ? CombinedPackageDefaultName(packageMode) : list[0].LastBuiltFamilyName;
                state.PackageMode = packageMode;
                state.PublishedFileId = publishedId;
                state.MemberIds = new List<string>();
                for (int i = 0; i < list.Count; i++) if (!string.IsNullOrEmpty(list[i].ModId)) state.MemberIds.Add(list[i].ModId);
                state.MemberIds.Sort(StringComparer.Ordinal);
                state.LinkNeedsUpdate = list[0].WorkshopLinkNeedsUpdate;
                state.LastUpdatedUtc = list[0].WorkshopLastUpdatedUtc ?? "";
                state.LastPublishedUtc = list[0].WorkshopLastPublishedUtc ?? "";
            }

            state.LastCheckedUtc = now;
            if (found != null)
            {
                state.LinkState = "Linked";
                if (!string.IsNullOrEmpty(found.Title)) state.Title = found.Title;
                if (!string.IsNullOrEmpty(found.Visibility)) state.Visibility = found.Visibility;
            }
            else if (recentSuccessfulPublish)
            {
                state.LinkState = "Linked";
            }
            else
            {
                state.PreviousPublishedFileId = state.PublishedFileId ?? "";
                state.PublishedFileId = "";
                state.LinkState = "Missing";
                state.LinkNeedsUpdate = false;
            }
            SaveWorkshopFamilyRegistry(state);

            for (int i = 0; i < list.Count; i++)
            {
                ModProjectRecord record = list[i];
                record.WorkshopPublishedFileId = state.PublishedFileId ?? "";
                record.WorkshopPreviousPublishedFileId = state.PreviousPublishedFileId ?? "";
                record.WorkshopPackageMode = string.IsNullOrEmpty(state.PublishedFileId) ? BuildPackageModes.Single : packageMode;
                record.WorkshopFamilyKey = string.IsNullOrEmpty(state.PublishedFileId) ? "" : (state.FamilyKey ?? "");
                record.WorkshopFamilyName = string.IsNullOrEmpty(state.PublishedFileId) ? "" : (state.FamilyName ?? CombinedPackageDefaultName(packageMode));
                record.WorkshopFamilyMemberIds = string.IsNullOrEmpty(state.PublishedFileId) ? new List<string>() : new List<string>(state.MemberIds ?? new List<string>());
                record.WorkshopLinkState = state.LinkState ?? "Unlinked";
                record.WorkshopLinkNeedsUpdate = state.LinkNeedsUpdate;
                record.WorkshopLinkLastCheckedUtc = now;
                if (!string.IsNullOrEmpty(state.Title)) record.WorkshopTitle = state.Title;
                if (!string.IsNullOrEmpty(state.Visibility)) record.WorkshopVisibility = state.Visibility;
                record.WorkshopLastUpdatedUtc = state.LastUpdatedUtc ?? record.WorkshopLastUpdatedUtc;
                record.WorkshopLastPublishedUtc = state.LastPublishedUtc ?? record.WorkshopLastPublishedUtc;
                WriteProject(record);
            }
            return found != null || recentSuccessfulPublish;
        }

        public bool UpdateWorkshopLinkFromQuery(ModProjectRecord record, WorkshopQueryResult query)
        {
            if (record == null || string.IsNullOrEmpty(record.ModId) || query == null || !query.Success)
                return false;

            string currentId = record.WorkshopPublishedFileId ?? "";
            if (string.IsNullOrEmpty(currentId))
            {
                // Nothing is actively linked. Preserve an explicit Missing state until
                // the user publishes/relinks, but do not rewrite ordinary unlinked projects.
                return false;
            }

            WorkshopItemSummary foundItem = FindWorkshopItem(query.Items, currentId);
            bool found = foundItem != null;
            record.WorkshopLinkLastCheckedUtc = DateTime.UtcNow.ToString("o");
            if (found)
            {
                record.WorkshopLinkState = "Linked";
                if (!string.IsNullOrEmpty(foundItem.Title))
                    record.WorkshopTitle = foundItem.Title;
                if (!string.IsNullOrEmpty(foundItem.Visibility))
                    record.WorkshopVisibility = foundItem.Visibility;
                record.ProjectVersion = Math.Max(record.ProjectVersion, 4);
                WriteProject(record);
                return true;
            }

            // Never leave a stale id in the active update slot. Steam ids are only
            // written back here after an exact match or a successful publish/update.
            MarkWorkshopLinkMissing(record, currentId);
            return false;
        }

        public void MarkWorkshopLinkMissing(ModProjectRecord record, string missingPublishedFileId)
        {
            if (record == null || string.IsNullOrEmpty(record.ModId))
                return;
            string missingId = string.IsNullOrEmpty(missingPublishedFileId) ? (record.WorkshopPublishedFileId ?? "") : missingPublishedFileId;
            if (!string.IsNullOrEmpty(missingId))
                record.WorkshopPreviousPublishedFileId = missingId;
            record.WorkshopPublishedFileId = "";
            record.WorkshopLinkState = "Missing";
            record.WorkshopLinkNeedsUpdate = false;
            record.WorkshopLinkLastCheckedUtc = DateTime.UtcNow.ToString("o");
            record.ProjectVersion = Math.Max(record.ProjectVersion, 3);
            WriteProject(record);
        }

        // Explicitly relink a saved Memento Maker project to a Workshop item
        // selected by the user. Relinking never uploads content, so it is deliberately
        // marked as needing an update until the next successful Workshop submission.
        public ModProjectRecord RelinkWorkshopItem(ModProjectRecord record, WorkshopItemSummary item)
        {
            if (record == null || string.IsNullOrEmpty(record.ModId))
                throw new InvalidOperationException("The selected mod does not have a valid ItemModID.");
            if (item == null || string.IsNullOrEmpty(item.PublishedFileId))
                throw new InvalidOperationException("Select a valid Steam Workshop item to relink.");

            EnsureWorkshopItemAvailableForProject(item.PublishedFileId, record.ModId);

            string oldId = record.WorkshopPublishedFileId ?? "";
            if (!string.IsNullOrEmpty(oldId) && !string.Equals(oldId, item.PublishedFileId, StringComparison.Ordinal))
                record.WorkshopPreviousPublishedFileId = oldId;
            else if (string.IsNullOrEmpty(oldId) && string.Equals(record.WorkshopLinkState, "Missing", StringComparison.OrdinalIgnoreCase) &&
                     !string.IsNullOrEmpty(record.WorkshopPreviousPublishedFileId) &&
                     !string.Equals(record.WorkshopPreviousPublishedFileId, item.PublishedFileId, StringComparison.Ordinal))
            {
                // Preserve the missing/deleted id as history when linking to a different item.
            }

            record.WorkshopPublishedFileId = item.PublishedFileId;
            record.WorkshopLinkState = "Linked";
            record.WorkshopLinkNeedsUpdate = true;
            record.WorkshopLinkLastCheckedUtc = DateTime.UtcNow.ToString("o");
            if (!string.IsNullOrEmpty(item.Title))
                record.WorkshopTitle = item.Title;
            if (!string.IsNullOrEmpty(item.Visibility))
                record.WorkshopVisibility = item.Visibility;
            record.ProjectVersion = Math.Max(record.ProjectVersion, 4);
            WriteProject(record);
            return record;
        }

        // One Steam Workshop ID may belong to only one independent Memento Maker project at a time.
        // Combined Families / Décor Packs intentionally duplicate the same shared Workshop identity
        // across all members, so another member of the SAME Workshop family is not a duplicate owner.
        public ModProjectRecord FindWorkshopLinkOwner(string publishedFileId, string excludeModId)
        {
            if (string.IsNullOrEmpty(publishedFileId))
                return null;

            List<ModProjectRecord> records = LoadAll();
            ModProjectRecord target = null;
            if (!string.IsNullOrEmpty(excludeModId))
            {
                for (int i = 0; i < records.Count; i++)
                {
                    ModProjectRecord candidateTarget = records[i];
                    if (candidateTarget != null && string.Equals(candidateTarget.ModId ?? "", excludeModId, StringComparison.Ordinal))
                    {
                        target = candidateTarget;
                        break;
                    }
                }
            }

            for (int i = 0; i < records.Count; i++)
            {
                ModProjectRecord candidate = records[i];
                if (candidate == null || string.IsNullOrEmpty(candidate.ModId))
                    continue;
                if (!string.IsNullOrEmpty(excludeModId) && string.Equals(candidate.ModId, excludeModId, StringComparison.Ordinal))
                    continue;
                if (!string.Equals(candidate.WorkshopPublishedFileId ?? "", publishedFileId, StringComparison.Ordinal))
                    continue;
                if (SharesWorkshopFamilyIdentity(target, candidate))
                    continue;
                return candidate;
            }
            return null;
        }

        private static bool SharesWorkshopFamilyIdentity(ModProjectRecord a, ModProjectRecord b)
        {
            if (a == null || b == null)
                return false;
            string modeA = BuildPackageModes.Normalise(a.WorkshopPackageMode);
            string modeB = BuildPackageModes.Normalise(b.WorkshopPackageMode);
            if (!BuildPackageModes.IsCombined(modeA) || modeA != modeB)
                return false;
            if (string.IsNullOrEmpty(a.WorkshopFamilyKey) || string.IsNullOrEmpty(b.WorkshopFamilyKey))
                return false;
            return string.Equals(a.WorkshopFamilyKey, b.WorkshopFamilyKey, StringComparison.Ordinal);
        }

        public void EnsureWorkshopItemAvailableForProject(string publishedFileId, string targetModId)
        {
            ModProjectRecord owner = FindWorkshopLinkOwner(publishedFileId, targetModId);
            if (owner == null)
                return;

            string ownerName = string.IsNullOrEmpty(owner.Name) ? owner.ModId : owner.Name;
            throw new InvalidOperationException(
                "Workshop item " + publishedFileId + " is already linked to Memento Maker mod '" + ownerName + "' (" + owner.ModId + "). " +
                "A Steam Workshop item can only be linked to one Memento Maker mod. Unlink it from that mod first if you want to move the association.");
        }

        public List<WorkshopItemSummary> FilterWorkshopItemsAvailableForProject(List<WorkshopItemSummary> items, string targetModId)
        {
            List<WorkshopItemSummary> available = new List<WorkshopItemSummary>();
            if (items == null)
                return available;

            HashSet<string> usedByOtherProjects = new HashSet<string>(StringComparer.Ordinal);
            List<ModProjectRecord> records = LoadAll();
            ModProjectRecord target = null;
            if (!string.IsNullOrEmpty(targetModId))
            {
                for (int i = 0; i < records.Count; i++)
                {
                    ModProjectRecord candidateTarget = records[i];
                    if (candidateTarget != null && string.Equals(candidateTarget.ModId ?? "", targetModId, StringComparison.Ordinal))
                    {
                        target = candidateTarget;
                        break;
                    }
                }
            }
            for (int i = 0; i < records.Count; i++)
            {
                ModProjectRecord candidate = records[i];
                if (candidate == null || string.IsNullOrEmpty(candidate.ModId))
                    continue;
                if (!string.IsNullOrEmpty(targetModId) && string.Equals(candidate.ModId, targetModId, StringComparison.Ordinal))
                    continue;
                if (SharesWorkshopFamilyIdentity(target, candidate))
                    continue;
                if (!string.IsNullOrEmpty(candidate.WorkshopPublishedFileId))
                    usedByOtherProjects.Add(candidate.WorkshopPublishedFileId);
            }

            for (int i = 0; i < items.Count; i++)
            {
                WorkshopItemSummary item = items[i];
                if (item == null || string.IsNullOrEmpty(item.PublishedFileId))
                    continue;
                if (!usedByOtherProjects.Contains(item.PublishedFileId))
                    available.Add(item);
            }
            return available;
        }

        // Remove only Memento Maker's local association. This never calls Steam
        // and therefore never deletes or changes the Workshop item itself.
        public ModProjectRecord UnlinkWorkshopItem(ModProjectRecord record)
        {
            if (record == null || string.IsNullOrEmpty(record.ModId))
                throw new InvalidOperationException("The selected mod does not have a valid ItemModID.");

            string linkedId = record.WorkshopPublishedFileId ?? "";
            if (!string.IsNullOrEmpty(linkedId))
                record.WorkshopPreviousPublishedFileId = linkedId;

            record.WorkshopPublishedFileId = "";
            record.WorkshopPackageMode = BuildPackageModes.Single;
            record.WorkshopFamilyKey = "";
            record.WorkshopFamilyName = "";
            record.WorkshopFamilyMemberIds = new List<string>();
            record.WorkshopLinkState = "Unlinked";
            record.WorkshopLinkNeedsUpdate = false;
            record.WorkshopLinkLastCheckedUtc = DateTime.UtcNow.ToString("o");
            record.ProjectVersion = Math.Max(record.ProjectVersion, 4);
            WriteProject(record);
            JsonFile.SynchroniseBackup(Path.Combine(GetProjectDirectory(record.ModId), "project.json"));
            return record;
        }

        public int ApplyWorkshopQueryState(WorkshopQueryResult query, string skipModId)
        {
            if (query == null || !query.Success)
                return 0;

            int changed = 0;
            List<ModProjectRecord> records = LoadAll();
            for (int i = 0; i < records.Count; i++)
            {
                ModProjectRecord record = records[i];
                if (record == null || string.IsNullOrEmpty(record.ModId) ||
                    string.Equals(record.ModId, skipModId, StringComparison.Ordinal))
                    continue;

                // Family Workshop identities are duplicated across every family member and
                // must be validated atomically by UpdateWorkshopFamilyLinkFromQuery. Running the
                // old per-project validator here could clear the same newly-published family ID
                // one member at a time during Steam's short post-publish propagation window.
                if (BuildPackageModes.IsCombined(record.WorkshopPackageMode))
                    continue;

                string beforeId = record.WorkshopPublishedFileId ?? "";
                string beforeState = record.WorkshopLinkState ?? "";
                UpdateWorkshopLinkFromQuery(record, query);
                if (!string.Equals(beforeId, record.WorkshopPublishedFileId ?? "", StringComparison.Ordinal) ||
                    !string.Equals(beforeState, record.WorkshopLinkState ?? "", StringComparison.Ordinal))
                    changed++;
            }
            return changed;
        }

        private static WorkshopItemSummary FindWorkshopItem(List<WorkshopItemSummary> items, string publishedFileId)
        {
            if (items == null || string.IsNullOrEmpty(publishedFileId))
                return null;
            for (int i = 0; i < items.Count; i++)
            {
                WorkshopItemSummary item = items[i];
                if (item != null && string.Equals(item.PublishedFileId, publishedFileId, StringComparison.Ordinal))
                    return item;
            }
            return null;
        }

        public List<ModProjectRecord> FindChildren(string parentModId)
        {
            List<ModProjectRecord> children = new List<ModProjectRecord>();
            if (string.IsNullOrEmpty(parentModId))
                return children;

            List<ModProjectRecord> records = LoadAll();
            for (int i = 0; i < records.Count; i++)
            {
                ModProjectRecord candidate = records[i];
                if (candidate == null)
                    continue;
                if (VariantModes.Normalise(candidate.VariantMode) == VariantModes.Modded &&
                    string.Equals(candidate.VariantParentModId ?? "", parentModId, StringComparison.Ordinal))
                    children.Add(candidate);
            }
            return children;
        }

        public List<ModProjectRecord> GetEligibleVariantParents(string excludeModId)
        {
            List<ModProjectRecord> eligible = new List<ModProjectRecord>();
            List<ModProjectRecord> records = LoadAll();
            for (int i = 0; i < records.Count; i++)
            {
                ModProjectRecord candidate = records[i];
                if (candidate == null || string.IsNullOrEmpty(candidate.ModId))
                    continue;
                if (!string.IsNullOrEmpty(excludeModId) &&
                    string.Equals(candidate.ModId, excludeModId, StringComparison.Ordinal))
                    continue;
                if (VariantModes.Normalise(candidate.VariantMode) != VariantModes.Standalone)
                    continue;
                if (IsWallpaperTemplateKey(candidate.Template))
                    continue;
                if (string.IsNullOrEmpty(candidate.LastBuiltUtc))
                    continue;
                eligible.Add(candidate);
            }

            eligible.Sort(delegate(ModProjectRecord a, ModProjectRecord b)
            {
                return string.Compare(a == null ? "" : a.Name, b == null ? "" : b.Name, StringComparison.OrdinalIgnoreCase);
            });
            return eligible;
        }

        public void ValidateVariantState(ModProjectRecord record, string variantMode, string variantParentModId)
        {
            string mode = VariantModes.Normalise(variantMode);
            string ownId = record == null ? "" : (record.ModId ?? "");

            if (record != null && IsWallpaperTemplateKey(record.Template) && mode != VariantModes.Standalone)
                throw new InvalidOperationException("Wallpaper room visuals cannot be variants or variant-family members.");

            if (mode != VariantModes.Standalone && !string.IsNullOrEmpty(ownId))
            {
                List<ModProjectRecord> children = FindChildren(ownId);
                if (children.Count > 0)
                    throw new InvalidOperationException(
                        "This mod is already the parent of " + children.Count + " variant" + (children.Count == 1 ? "" : "s") +
                        ". A parent item cannot itself become a child of the base game or another mod.");
            }

            if (mode != VariantModes.Modded)
                return;

            if (string.IsNullOrEmpty(variantParentModId))
                throw new InvalidOperationException("Choose a built Memento Maker mod to use as the parent item.");
            if (!string.IsNullOrEmpty(ownId) && string.Equals(ownId, variantParentModId, StringComparison.Ordinal))
                throw new InvalidOperationException("A mod cannot be a variant of itself.");

            ModProjectRecord parent = Load(variantParentModId);
            if (parent == null)
                throw new InvalidOperationException("The selected parent mod no longer exists in Memento Maker.");
            if (VariantModes.Normalise(parent.VariantMode) != VariantModes.Standalone)
                throw new InvalidOperationException(
                    "The selected parent mod is itself a variant. Only standalone mods can be used as parent items.");
            if (IsWallpaperTemplateKey(parent.Template))
                throw new InvalidOperationException("Wallpaper room visuals cannot be used as variant parents.");
            if (string.IsNullOrEmpty(parent.LastBuiltUtc))
                throw new InvalidOperationException("The selected parent mod has not been built yet.");
        }

        public List<ModProjectRecord> ConvertChildrenToStandalone(string parentModId)
        {
            List<ModProjectRecord> children = FindChildren(parentModId);
            string now = DateTime.UtcNow.ToString("o");
            for (int i = 0; i < children.Count; i++)
            {
                ModProjectRecord child = children[i];
                DetachWorkshopFamilyIdentity(child);
                child.VariantMode = VariantModes.Standalone;
                child.VariantParentModId = "";
                child.VariantParentBaseArchetypeId = "";
                child.LastEditedUtc = now;
                child.PendingChanges = true;
                child.ProjectFormatVersion = CurrentProjectFormatVersion;
                WriteProject(child);
            }
            return children;
        }

        public List<ModProjectRecord> DeleteRecordAndConvertChildren(ModProjectRecord record)
        {
            if (record == null || string.IsNullOrEmpty(record.ModId))
                return new List<ModProjectRecord>();

            List<ModProjectRecord> converted = ConvertChildrenToStandalone(record.ModId);
            DeleteRecord(record);
            return converted;
        }

        public List<ModProjectRecord> DeleteRecordsAndConvertExternalChildren(List<ModProjectRecord> records)
        {
            List<ModProjectRecord> converted = new List<ModProjectRecord>();
            if (records == null || records.Count == 0)
                return converted;

            HashSet<string> selectedIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < records.Count; i++)
            {
                ModProjectRecord record = records[i];
                if (record != null && !string.IsNullOrEmpty(record.ModId))
                    selectedIds.Add(record.ModId);
            }

            string now = DateTime.UtcNow.ToString("o");
            HashSet<string> convertedIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < records.Count; i++)
            {
                ModProjectRecord parent = records[i];
                if (parent == null || string.IsNullOrEmpty(parent.ModId))
                    continue;

                List<ModProjectRecord> children = FindChildren(parent.ModId);
                for (int c = 0; c < children.Count; c++)
                {
                    ModProjectRecord child = children[c];
                    if (child == null || string.IsNullOrEmpty(child.ModId) || selectedIds.Contains(child.ModId))
                        continue;
                    if (!convertedIds.Add(child.ModId))
                        continue;

                    DetachWorkshopFamilyIdentity(child);
                    child.VariantMode = VariantModes.Standalone;
                    child.VariantParentModId = "";
                    child.VariantParentBaseArchetypeId = "";
                    child.LastEditedUtc = now;
                    child.PendingChanges = true;
                    child.ProjectFormatVersion = CurrentProjectFormatVersion;
                    WriteProject(child);
                    converted.Add(child);
                }
            }

            for (int i = 0; i < records.Count; i++)
                DeleteRecord(records[i]);

            return converted;
        }

        private void ApplyVariantState(ModProjectRecord record, string variantMode, string variantParentModId)
        {
            if (record == null)
                return;

            bool wallpaper = IsWallpaperTemplateKey(record.Template);
            string mode = wallpaper ? VariantModes.Standalone : VariantModes.Normalise(variantMode);
            ValidateVariantState(record, mode, variantParentModId);
            record.VariantMode = mode;
            record.VariantParentModId = mode == VariantModes.Modded ? (variantParentModId ?? "") : "";
            if (mode != VariantModes.BaseGame)
                record.VariantParentBaseArchetypeId = "";

            // Variants cannot be unlocked with Kudosh in-game. Wallpaper room visuals
            // are fixed at zero Cost/Kudosh and remain standalone.
            if (mode != VariantModes.Standalone)
                record.KudoshCost = 0;
            if (wallpaper)
            {
                record.ItemCost = 0;
                record.KudoshCost = 0;
                record.VariantParentBaseArchetypeId = "";
            }
        }

        private static bool IsWallpaperTemplateKey(string templateKey)
        {
            return string.Equals(templateKey ?? "", "Wallpaper", StringComparison.OrdinalIgnoreCase);
        }

        public void MarkProjectsPendingChanges(IEnumerable<ModProjectRecord> records)
        {
            if (records == null)
                return;

            string now = DateTime.UtcNow.ToString("o");
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (ModProjectRecord record in records)
            {
                if (record == null || string.IsNullOrEmpty(record.ModId) || !seen.Add(record.ModId))
                    continue;
                record.LastEditedUtc = now;
                record.PendingChanges = true;
                record.ProjectFormatVersion = CurrentProjectFormatVersion;
                WriteProject(record);
            }
        }

        private static void DetachWorkshopFamilyIdentity(ModProjectRecord record)
        {
            if (record == null || !BuildPackageModes.IsCombined(record.WorkshopPackageMode))
                return;
            string linkedId = record.WorkshopPublishedFileId ?? "";
            if (!string.IsNullOrEmpty(linkedId))
                record.WorkshopPreviousPublishedFileId = linkedId;
            record.WorkshopPublishedFileId = "";
            record.WorkshopPackageMode = BuildPackageModes.Single;
            record.WorkshopFamilyKey = "";
            record.WorkshopFamilyName = "";
            record.WorkshopFamilyMemberIds = new List<string>();
            record.WorkshopLinkState = "Unlinked";
            record.WorkshopLinkNeedsUpdate = false;
            record.WorkshopDependencyPublishedFileId = "";
            record.WorkshopLinkLastCheckedUtc = DateTime.UtcNow.ToString("o");
        }

        public void SaveVariantFamilyMembership(ModProjectRecord record, string variantMode,
            string variantParentModId, string variantParentBaseArchetypeId)
        {
            if (record == null || string.IsNullOrEmpty(record.ModId))
                throw new InvalidOperationException("The selected mod does not have a valid ItemModID.");

            string mode = VariantModes.Normalise(variantMode);
            ValidateVariantState(record, mode, variantParentModId);
            bool relationshipChanged = !string.Equals(VariantModes.Normalise(record.VariantMode), mode, StringComparison.Ordinal) ||
                !string.Equals(record.VariantParentModId ?? "", mode == VariantModes.Modded ? (variantParentModId ?? "") : "", StringComparison.Ordinal) ||
                !string.Equals(record.VariantParentBaseArchetypeId ?? "", mode == VariantModes.BaseGame ? (variantParentBaseArchetypeId ?? "") : "", StringComparison.Ordinal);
            if (relationshipChanged)
                DetachWorkshopFamilyIdentity(record);
            record.VariantMode = mode;
            record.VariantParentModId = mode == VariantModes.Modded ? (variantParentModId ?? "") : "";
            record.VariantParentBaseArchetypeId = mode == VariantModes.BaseGame
                ? (variantParentBaseArchetypeId ?? "") : "";
            if (mode != VariantModes.Standalone)
                record.KudoshCost = 0;
            record.LastEditedUtc = DateTime.UtcNow.ToString("o");
            record.PendingChanges = true;
            record.ProjectFormatVersion = CurrentProjectFormatVersion;
            WriteProject(record);
        }

        public List<ModProjectRecord> SaveDecorPackMembership(ModProjectRecord record, string packKey, string packName)
        {
            List<ModProjectRecord> affected = new List<ModProjectRecord>();
            if (record == null || string.IsNullOrEmpty(record.ModId))
                throw new InvalidOperationException("The selected mod does not have a valid ItemModID.");
            if (!IsWallpaperTemplateKey(record.Template))
                throw new InvalidOperationException("Only Wallpaper mods can belong to a Décor Pack.");

            string targetKey = string.IsNullOrWhiteSpace(packKey) ? "" : packKey.Trim();
            if (!string.IsNullOrEmpty(targetKey) && !targetKey.StartsWith("decorpack:", StringComparison.OrdinalIgnoreCase))
                targetKey = "decorpack:" + targetKey;
            if (!string.IsNullOrEmpty(targetKey) && string.IsNullOrWhiteSpace(packName))
                packName = "Wallpaper Pack";
            string targetName = string.IsNullOrWhiteSpace(packName) ? "" : packName.Trim();

            List<ModProjectRecord> all = LoadAll();
            Dictionary<string, ModProjectRecord> byId = new Dictionary<string, ModProjectRecord>(StringComparer.Ordinal);
            for (int i = 0; i < all.Count; i++)
            {
                ModProjectRecord candidate = all[i];
                if (candidate != null && !string.IsNullOrEmpty(candidate.ModId))
                    byId[candidate.ModId] = candidate;
            }

            ModProjectRecord current;
            if (!byId.TryGetValue(record.ModId, out current) || current == null)
                current = record;

            string previousMode = BuildPackageModes.Normalise(current.LastBuildPackageMode);
            string previousKey = previousMode == BuildPackageModes.DecorPack ? (current.LastBuiltFamilyKey ?? "") : "";
            string previousName = previousMode == BuildPackageModes.DecorPack ? (current.LastBuiltFamilyName ?? "") : "";
            string now = DateTime.UtcNow.ToString("o");
            HashSet<string> touchedIds = new HashSet<string>(StringComparer.Ordinal);

            Action<ModProjectRecord> touch = delegate(ModProjectRecord item)
            {
                if (item == null || string.IsNullOrEmpty(item.ModId))
                    return;
                item.LastEditedUtc = now;
                item.PendingChanges = true;
                item.ProjectFormatVersion = CurrentProjectFormatVersion;
                WriteProject(item);
                if (touchedIds.Add(item.ModId))
                    affected.Add(item);
            };

            if (!string.IsNullOrEmpty(previousKey) && !string.Equals(previousKey, targetKey, StringComparison.Ordinal))
            {
                List<ModProjectRecord> remainingMembers = new List<ModProjectRecord>();
                for (int i = 0; i < all.Count; i++)
                {
                    ModProjectRecord candidate = all[i];
                    if (candidate == null || string.IsNullOrEmpty(candidate.ModId) ||
                        string.Equals(candidate.ModId, current.ModId, StringComparison.Ordinal) ||
                        !IsWallpaperTemplateKey(candidate.Template) ||
                        BuildPackageModes.Normalise(candidate.LastBuildPackageMode) != BuildPackageModes.DecorPack ||
                        !string.Equals(candidate.LastBuiltFamilyKey ?? "", previousKey, StringComparison.Ordinal))
                        continue;
                    remainingMembers.Add(candidate);
                }

                List<string> remainingIds = new List<string>();
                for (int i = 0; i < remainingMembers.Count; i++)
                    remainingIds.Add(remainingMembers[i].ModId);
                remainingIds.Sort(StringComparer.Ordinal);

                string remainingName = string.IsNullOrWhiteSpace(previousName)
                    ? (remainingMembers.Count > 0 && !string.IsNullOrWhiteSpace(remainingMembers[0].LastBuiltFamilyName)
                        ? remainingMembers[0].LastBuiltFamilyName.Trim()
                        : "Wallpaper Pack")
                    : previousName.Trim();

                for (int i = 0; i < remainingMembers.Count; i++)
                {
                    ModProjectRecord member = remainingMembers[i];
                    member.LastBuildPackageMode = BuildPackageModes.DecorPack;
                    member.LastBuiltFamilyKey = previousKey;
                    member.LastBuiltFamilyName = remainingName;
                    member.LastBuiltFamilyMemberIds = new List<string>(remainingIds);
                    touch(member);
                }
            }

            if (string.IsNullOrEmpty(targetKey))
            {
                current.LastBuildPackageMode = BuildPackageModes.Single;
                current.LastBuiltFamilyKey = "";
                current.LastBuiltFamilyName = "";
                current.LastBuiltFamilyMemberIds = new List<string>();
                touch(current);
                return affected;
            }

            if (string.IsNullOrWhiteSpace(targetName))
                targetName = "Wallpaper Pack";

            List<ModProjectRecord> targetMembers = new List<ModProjectRecord>();
            for (int i = 0; i < all.Count; i++)
            {
                ModProjectRecord candidate = all[i];
                if (candidate == null || string.IsNullOrEmpty(candidate.ModId) ||
                    string.Equals(candidate.ModId, current.ModId, StringComparison.Ordinal) ||
                    !IsWallpaperTemplateKey(candidate.Template) ||
                    BuildPackageModes.Normalise(candidate.LastBuildPackageMode) != BuildPackageModes.DecorPack ||
                    !string.Equals(candidate.LastBuiltFamilyKey ?? "", targetKey, StringComparison.Ordinal))
                    continue;
                targetMembers.Add(candidate);
            }

            if (string.IsNullOrWhiteSpace(targetName))
            {
                for (int i = 0; i < targetMembers.Count; i++)
                {
                    if (!string.IsNullOrWhiteSpace(targetMembers[i].LastBuiltFamilyName))
                    {
                        targetName = targetMembers[i].LastBuiltFamilyName.Trim();
                        break;
                    }
                }
            }
            if (string.IsNullOrWhiteSpace(targetName))
                targetName = "Wallpaper Pack";

            List<string> targetIds = new List<string>();
            for (int i = 0; i < targetMembers.Count; i++)
                targetIds.Add(targetMembers[i].ModId);
            if (!targetIds.Contains(current.ModId))
                targetIds.Add(current.ModId);
            targetIds.Sort(StringComparer.Ordinal);

            for (int i = 0; i < targetMembers.Count; i++)
            {
                ModProjectRecord member = targetMembers[i];
                member.LastBuildPackageMode = BuildPackageModes.DecorPack;
                member.LastBuiltFamilyKey = targetKey;
                member.LastBuiltFamilyName = targetName;
                member.LastBuiltFamilyMemberIds = new List<string>(targetIds);
                touch(member);
            }

            current.LastBuildPackageMode = BuildPackageModes.DecorPack;
            current.LastBuiltFamilyKey = targetKey;
            current.LastBuiltFamilyName = targetName;
            current.LastBuiltFamilyMemberIds = new List<string>(targetIds);
            touch(current);
            return affected;
        }

        public static string CreateGeneratedDecorPackKey()
        {
            return "decorpack:" + Guid.NewGuid().ToString("N");
        }

        public List<ModProjectRecord> DetachFamilyPackage(string familyKey, IEnumerable<string> keepMemberIds, string sharedOutputPath)
        {
            List<ModProjectRecord> detached = new List<ModProjectRecord>();
            if (string.IsNullOrEmpty(familyKey))
                return detached;

            HashSet<string> keep = new HashSet<string>(StringComparer.Ordinal);
            if (keepMemberIds != null)
            {
                foreach (string id in keepMemberIds)
                    if (!string.IsNullOrEmpty(id))
                        keep.Add(id);
            }

            List<ModProjectRecord> records = LoadAll();
            for (int i = 0; i < records.Count; i++)
            {
                ModProjectRecord record = records[i];
                if (record == null || string.IsNullOrEmpty(record.ModId) || keep.Contains(record.ModId))
                    continue;
                if (BuildPackageModes.Normalise(record.LastBuildPackageMode) != BuildPackageModes.Family ||
                    !string.Equals(record.LastBuiltFamilyKey ?? "", familyKey, StringComparison.Ordinal))
                    continue;

                if (string.IsNullOrEmpty(sharedOutputPath) ||
                    string.Equals(record.LastBuiltOutputPath ?? "", sharedOutputPath, StringComparison.OrdinalIgnoreCase))
                {
                    record.LastBuiltOutputPath = "";
                }
                record.LastBuildPackageMode = BuildPackageModes.Single;
                record.LastBuiltFamilyKey = "";
                record.LastBuiltFamilyName = "";
                record.LastBuiltFamilyMemberIds = new List<string>();
                record.PendingChanges = true;
                record.ProjectFormatVersion = CurrentProjectFormatVersion;
                WriteProject(record);
                detached.Add(record);
            }
            return detached;
        }

        public List<ModProjectRecord> DetachDecorPack(string packKey, IEnumerable<string> keepMemberIds, string sharedOutputPath)
        {
            List<ModProjectRecord> detached = new List<ModProjectRecord>();
            if (string.IsNullOrEmpty(packKey))
                return detached;

            HashSet<string> keep = new HashSet<string>(StringComparer.Ordinal);
            if (keepMemberIds != null)
            {
                foreach (string id in keepMemberIds)
                    if (!string.IsNullOrEmpty(id))
                        keep.Add(id);
            }

            List<ModProjectRecord> records = LoadAll();
            for (int i = 0; i < records.Count; i++)
            {
                ModProjectRecord record = records[i];
                if (record == null || string.IsNullOrEmpty(record.ModId) || keep.Contains(record.ModId))
                    continue;
                if (BuildPackageModes.Normalise(record.LastBuildPackageMode) != BuildPackageModes.DecorPack ||
                    !string.Equals(record.LastBuiltFamilyKey ?? "", packKey, StringComparison.Ordinal))
                    continue;

                if (string.IsNullOrEmpty(sharedOutputPath) ||
                    string.Equals(record.LastBuiltOutputPath ?? "", sharedOutputPath, StringComparison.OrdinalIgnoreCase))
                {
                    record.LastBuiltOutputPath = "";
                }
                record.LastBuildPackageMode = BuildPackageModes.Single;
                record.LastBuiltFamilyKey = "";
                record.LastBuiltFamilyName = "";
                record.LastBuiltFamilyMemberIds = new List<string>();
                record.PendingChanges = true;
                record.ProjectFormatVersion = CurrentProjectFormatVersion;
                WriteProject(record);
                detached.Add(record);
            }
            return detached;
        }

        private void WriteProject(ModProjectRecord record)
        {
            if (record == null || string.IsNullOrEmpty(record.ModId))
                return;
            string projectDirectory = GetProjectDirectory(record.ModId);
            Directory.CreateDirectory(projectDirectory);
            record.ProjectFormatVersion = CurrentProjectFormatVersion;
            JsonFile.Write(Path.Combine(projectDirectory, "project.json"), record);
        }

        public void DeleteRecord(ModProjectRecord record)
        {
            if (record == null || string.IsNullOrEmpty(record.ModId))
                return;

            string directory = GetProjectDirectory(record.ModId);
            if (Directory.Exists(directory))
                DeleteDirectoryRobust(directory);
        }

        public string GetProjectDirectory(string modId)
        {
            return Path.Combine(_projectsRoot, MakeSafeFolderName(modId));
        }

        private static string CopySourceFile(string sourcePath, string projectDirectory, string baseName)
        {
            if (string.IsNullOrEmpty(sourcePath) || !File.Exists(sourcePath))
                throw new FileNotFoundException("The source image for the saved project could not be found.", sourcePath);

            string extension = Path.GetExtension(sourcePath);
            if (string.IsNullOrEmpty(extension))
                extension = ".png";
            string fileName = baseName + extension.ToLowerInvariant();
            string targetPath = Path.Combine(projectDirectory, fileName);

            string sourceFull = Path.GetFullPath(sourcePath);
            string targetFull = Path.GetFullPath(targetPath);
            if (string.Equals(sourceFull, targetFull, StringComparison.OrdinalIgnoreCase))
                return fileName;

            DeleteExistingNamedFiles(projectDirectory, baseName);
            File.Copy(sourcePath, targetPath, true);
            return fileName;
        }

        private static void DeleteExistingNamedFiles(string projectDirectory, string baseName)
        {
            if (!Directory.Exists(projectDirectory))
                return;

            foreach (string path in Directory.GetFiles(projectDirectory, baseName + ".*"))
            {
                try { File.Delete(path); } catch { }
            }
        }

        private static string MakeSafeFolderName(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "UnknownMod";
            foreach (char c in Path.GetInvalidFileNameChars())
                value = value.Replace(c, '_');
            return value;
        }

        private static void NormaliseFormatVersion(ModProjectRecord record)
        {
            if (record == null)
                return;

            // Projects created before variant support must keep their previous in-game
            // behaviour. They are therefore migrated as standalone rather than silently
            // becoming children of their base-game archetype.
            if (record.ProjectFormatVersion < 2 || string.IsNullOrEmpty(record.VariantMode))
            {
                record.VariantMode = VariantModes.Standalone;
                record.VariantParentModId = "";
            }
            else
            {
                record.VariantMode = VariantModes.Normalise(record.VariantMode);
                if (record.VariantMode != VariantModes.Modded)
                    record.VariantParentModId = "";
            }

            // Project format 3 adds persisted Create Mod artwork rotation. Older projects
            // simply migrate with the established 0-degree orientation.
            if (record.ProjectFormatVersion < 3)
                record.RotationDegrees = 0.0f;
            else
                record.RotationDegrees = NormaliseRotationDegrees(record.RotationDegrees);

            // Project format 5 allows a Base Game variant to target a base archetype other
            // than the one its own template is based on. Older projects keep the established
            // behaviour by leaving the override empty, which means "use my template base item".
            if (record.ProjectFormatVersion < 5)
                record.VariantParentBaseArchetypeId = "";
            if (record.VariantMode != VariantModes.BaseGame)
                record.VariantParentBaseArchetypeId = "";

            // Project format 4 adds an independent Right Banner artwork state for Double Banners.
            // Older projects simply migrate with an empty secondary source and default placement.
            if (record.ProjectFormatVersion < 4)
            {
                record.SecondaryArtworkFileName = "";
                record.SecondaryFitMode = "Fill";
                record.SecondaryZoom = 1.0f;
                record.SecondaryOffsetX = 0.0f;
                record.SecondaryOffsetY = 0.0f;
                record.SecondaryRotationDegrees = 0.0f;
                record.SecondaryGuideColor = "Yellow";
            }
            else
            {
                if (string.IsNullOrEmpty(record.SecondaryFitMode)) record.SecondaryFitMode = "Fill";
                if (record.SecondaryZoom <= 0.0f) record.SecondaryZoom = 1.0f;
                record.SecondaryRotationDegrees = NormaliseRotationDegrees(record.SecondaryRotationDegrees);
                if (string.IsNullOrEmpty(record.SecondaryGuideColor)) record.SecondaryGuideColor = "Yellow";
                if (record.SecondaryArtworkFileName == null) record.SecondaryArtworkFileName = "";
            }

            if (record.ItemCustomisationId == null)
                record.ItemCustomisationId = "";
            if (record.WorkshopDependencyPublishedFileId == null)
                record.WorkshopDependencyPublishedFileId = "";
            record.LastBuildPackageMode = BuildPackageModes.Normalise(record.LastBuildPackageMode);
            if (record.LastBuiltFamilyKey == null) record.LastBuiltFamilyKey = "";
            if (record.LastBuiltFamilyName == null) record.LastBuiltFamilyName = "";
            if (record.LastBuiltFamilyMemberIds == null) record.LastBuiltFamilyMemberIds = new List<string>();
            // Combined-package metadata belongs to both BL-020 Variant Families and
            // BL-022 Decor Packs. Preview 7 only preserved it for Family, which meant a
            // freshly-built Decor Pack lost its key/member snapshot as soon as My Mods
            // reloaded the project records. Without that snapshot the pack could not be
            // reconstructed as a collapsed group in the UI.
            if (!BuildPackageModes.IsCombined(record.LastBuildPackageMode))
            {
                record.LastBuiltFamilyKey = "";
                record.LastBuiltFamilyName = "";
                record.LastBuiltFamilyMemberIds.Clear();
            }

            record.WorkshopPackageMode = BuildPackageModes.Normalise(record.WorkshopPackageMode);
            if (record.WorkshopFamilyKey == null) record.WorkshopFamilyKey = "";
            if (record.WorkshopFamilyName == null) record.WorkshopFamilyName = "";
            if (record.WorkshopFamilyMemberIds == null) record.WorkshopFamilyMemberIds = new List<string>();
            if (!BuildPackageModes.IsCombined(record.WorkshopPackageMode))
            {
                record.WorkshopFamilyKey = "";
                record.WorkshopFamilyName = "";
                record.WorkshopFamilyMemberIds.Clear();
            }

            if (record.ProjectFormatVersion < CurrentProjectFormatVersion)
                record.ProjectFormatVersion = CurrentProjectFormatVersion;
        }

        private static float NormaliseRotationDegrees(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                return 0.0f;

            value %= 360.0f;
            if (value < 0.0f)
                value += 360.0f;
            if (Math.Abs(value - 360.0f) < 0.0001f || Math.Abs(value) < 0.0001f)
                return 0.0f;
            return value;
        }

        private static DateTime ParseDate(string value)
        {
            DateTime date;
            if (DateTime.TryParse(value, out date))
                return date;
            return DateTime.MinValue;
        }

        private static void DeleteDirectoryRobust(string directory)
        {
            foreach (string file in Directory.GetFiles(directory, "*", SearchOption.AllDirectories))
            {
                try { File.SetAttributes(file, FileAttributes.Normal); } catch { }
            }
            Directory.Delete(directory, true);
        }
    }
}
