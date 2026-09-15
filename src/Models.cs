using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.Serialization;
using System.Windows.Forms;

namespace TPMSimpleModMaker
{
    [DataContract]
    public sealed class AppSettings
    {
        [DataMember(Name = "settingsFormatVersion")]
        public int SettingsFormatVersion { get; set; }

        [DataMember(Name = "unityExePath")]
        public string UnityExePath { get; set; }

        [DataMember(Name = "sdkZipPath")]
        public string SdkZipPath { get; set; }

        [DataMember(Name = "lastOutputFolder")]
        public string LastOutputFolder { get; set; }

        [DataMember(Name = "keepUnityWorkerRunning")]
        public bool? KeepUnityWorkerRunning { get; set; }

        [DataMember(Name = "moddersName")]
        public string ModdersName { get; set; }

        [DataMember(Name = "workshopFamilyPreviewStyle")]
        public string WorkshopFamilyPreviewStyle { get; set; }
    }

    [DataContract]
    public sealed class TemplateCatalog
    {
        [DataMember(Name = "templates")]
        public List<TemplateDefinition> Templates { get; set; }
    }

    [DataContract]
    public sealed class TemplateDefinition
    {
        [DataMember(Name = "key")]
        public string Key { get; set; }

        [DataMember(Name = "displayName")]
        public string DisplayName { get; set; }

        [DataMember(Name = "baseItemName")]
        public string BaseItemName { get; set; }

        [DataMember(Name = "baseArchetypeId")]
        public long BaseArchetypeId { get; set; }

        [DataMember(Name = "defaultCost")]
        public int DefaultCost { get; set; }

        [DataMember(Name = "defaultKudosh")]
        public int DefaultKudosh { get; set; }

        [DataMember(Name = "imageHint")]
        public string ImageHint { get; set; }

        [DataMember(Name = "imageProcessingEnabled")]
        public bool ImageProcessingEnabled { get; set; }

        [DataMember(Name = "textureWidth")]
        public int TextureWidth { get; set; }

        [DataMember(Name = "textureHeight")]
        public int TextureHeight { get; set; }

        [DataMember(Name = "visibleX")]
        public int VisibleX { get; set; }

        [DataMember(Name = "visibleY")]
        public int VisibleY { get; set; }

        [DataMember(Name = "visibleWidth")]
        public int VisibleWidth { get; set; }

        [DataMember(Name = "visibleHeight")]
        public int VisibleHeight { get; set; }

        [DataMember(Name = "defaultFitMode")]
        public string DefaultFitMode { get; set; }

        [DataMember(Name = "mappingNote")]
        public string MappingNote { get; set; }

        public override string ToString()
        {
            return string.IsNullOrEmpty(DisplayName) ? Key : DisplayName;
        }
    }

    public sealed class ImagePlacementState
    {
        public ImagePlacementState()
        {
            Zoom = 1.0f;
            OffsetX = 0.0f;
            OffsetY = 0.0f;
            RotationDegrees = 0.0f;
            GuideColorName = "Yellow";
            ReferenceWidth = 0;
            ReferenceHeight = 0;
        }

        public float Zoom { get; set; }
        public float OffsetX { get; set; }
        public float OffsetY { get; set; }
        public float RotationDegrees { get; set; }
        public string GuideColorName { get; set; }

        // Transient editor reference dimensions used by multi-size item families.
        // These are deliberately not stored in ModProjectRecord/project format. When a
        // saved project is opened, the current size becomes the reference again.
        public int ReferenceWidth { get; set; }
        public int ReferenceHeight { get; set; }
    }

    [DataContract]
    public sealed class BuildJob
    {
        [DataMember(Name = "template")]
        public string Template { get; set; }

        [DataMember(Name = "modName")]
        public string ModName { get; set; }

        [DataMember(Name = "moddersName")]
        public string ModdersName { get; set; }

        [DataMember(Name = "description")]
        public string Description { get; set; }

        [DataMember(Name = "imagePath")]
        public string ImagePath { get; set; }

        [DataMember(Name = "outputPath")]
        public string OutputPath { get; set; }

        [DataMember(Name = "itemCost")]
        public int ItemCost { get; set; }

        [DataMember(Name = "kudoshCost")]
        public int KudoshCost { get; set; }

        [DataMember(Name = "itemModId")]
        public string ItemModId { get; set; }

        [DataMember(Name = "variantMode")]
        public string VariantMode { get; set; }

        [DataMember(Name = "variantParentModId")]
        public string VariantParentModId { get; set; }

        [DataMember(Name = "variantParentBaseArchetypeId")]
        public string VariantParentBaseArchetypeId { get; set; }

        [DataMember(Name = "itemCustomisationId")]
        public string ItemCustomisationId { get; set; }

        [DataMember(Name = "iconPath")]
        public string IconPath { get; set; }

        [DataMember(Name = "useItemImageAsIcon")]
        public bool UseItemImageAsIcon { get; set; }

        [DataMember(Name = "keepGeneratedAssets")]
        public bool KeepGeneratedAssets { get; set; }
    }

    [DataContract]
    public sealed class BatchBuildManifest
    {
        [DataMember(Name = "jobPaths")]
        public List<string> JobPaths { get; set; }
    }

    [DataContract]
    public sealed class FamilyBuildManifest
    {
        [DataMember(Name = "jobPaths")]
        public List<string> JobPaths { get; set; }

        [DataMember(Name = "familyKey")]
        public string FamilyKey { get; set; }

        [DataMember(Name = "familyName")]
        public string FamilyName { get; set; }

        [DataMember(Name = "outputPath")]
        public string OutputPath { get; set; }

        [DataMember(Name = "packageMode")]
        public string PackageMode { get; set; }
    }

    [DataContract]
    public sealed class FamilyBuildResultFile
    {
        [DataMember(Name = "success")]
        public bool Success { get; set; }

        [DataMember(Name = "message")]
        public string Message { get; set; }

        [DataMember(Name = "familyKey")]
        public string FamilyKey { get; set; }

        [DataMember(Name = "familyName")]
        public string FamilyName { get; set; }

        [DataMember(Name = "outputPath")]
        public string OutputPath { get; set; }

        [DataMember(Name = "packageMode")]
        public string PackageMode { get; set; }

        [DataMember(Name = "memberResults")]
        public List<BuildResultFile> MemberResults { get; set; }

        [DataMember(Name = "utcCompleted")]
        public string UtcCompleted { get; set; }
    }

    internal sealed class BatchBuildOutcome
    {
        public string JobPath { get; set; }
        public BuildResultFile Result { get; set; }
        public string ErrorMessage { get; set; }

        public bool Success
        {
            get { return Result != null && Result.Success && string.IsNullOrEmpty(ErrorMessage); }
        }
    }

    [DataContract]
    public sealed class BuildResultFile
    {
        [DataMember(Name = "success")]
        public bool Success { get; set; }

        [DataMember(Name = "message")]
        public string Message { get; set; }

        [DataMember(Name = "template")]
        public string Template { get; set; }

        [DataMember(Name = "baseItemName")]
        public string BaseItemName { get; set; }

        [DataMember(Name = "baseArchetypeId")]
        public string BaseArchetypeId { get; set; }

        [DataMember(Name = "modName")]
        public string ModName { get; set; }

        [DataMember(Name = "itemModId")]
        public string ItemModId { get; set; }

        [DataMember(Name = "itemCustomisationId")]
        public string ItemCustomisationId { get; set; }

        [DataMember(Name = "outputPath")]
        public string OutputPath { get; set; }

        [DataMember(Name = "generatedAssetFolder")]
        public string GeneratedAssetFolder { get; set; }

        [DataMember(Name = "customIconCreated")]
        public bool CustomIconCreated { get; set; }

        [DataMember(Name = "iconSourcePath")]
        public string IconSourcePath { get; set; }

        [DataMember(Name = "utcCompleted")]
        public string UtcCompleted { get; set; }
    }

    [DataContract]
    public sealed class ModProjectRecord
    {
        [DataMember(Name = "projectFormatVersion")]
        public int ProjectFormatVersion { get; set; }

        [DataMember(Name = "projectVersion")]
        public int ProjectVersion { get; set; }

        [DataMember(Name = "modId")]
        public string ModId { get; set; }

        [DataMember(Name = "name")]
        public string Name { get; set; }

        [DataMember(Name = "description")]
        public string Description { get; set; }

        [DataMember(Name = "template")]
        public string Template { get; set; }

        [DataMember(Name = "itemCost")]
        public int ItemCost { get; set; }

        [DataMember(Name = "kudoshCost")]
        public int KudoshCost { get; set; }

        // Variant relationship. Existing pre-0.9.5 projects migrate to Standalone so
        // their in-game behaviour never changes merely because Memento Maker was upgraded.
        [DataMember(Name = "variantMode")]
        public string VariantMode { get; set; }

        [DataMember(Name = "variantParentModId")]
        public string VariantParentModId { get; set; }

        // Project format 5: optional override for a Base Game variant family. Empty means
        // use this project's own template archetype, preserving all pre-BL020 behaviour.
        [DataMember(Name = "variantParentBaseArchetypeId")]
        public string VariantParentBaseArchetypeId { get; set; }

        [DataMember(Name = "itemCustomisationId")]
        public string ItemCustomisationId { get; set; }

        [DataMember(Name = "fitMode")]
        public string FitMode { get; set; }

        [DataMember(Name = "zoom")]
        public float Zoom { get; set; }

        [DataMember(Name = "offsetX")]
        public float OffsetX { get; set; }

        [DataMember(Name = "offsetY")]
        public float OffsetY { get; set; }

        [DataMember(Name = "rotationDegrees")]
        public float RotationDegrees { get; set; }

        [DataMember(Name = "guideColor")]
        public string GuideColor { get; set; }

        // Additive project-format-4 fields: preserve the editor's fixed artwork reference
        // when a multi-size Poster/Hanging Sign/Wall Sign is saved after switching size. Older
        // project files omit these values and safely fall back to the saved current size.
        [DataMember(Name = "referenceWidth")]
        public int ReferenceWidth { get; set; }

        [DataMember(Name = "referenceHeight")]
        public int ReferenceHeight { get; set; }

        [DataMember(Name = "iconMode")]
        public string IconMode { get; set; }

        [DataMember(Name = "artworkFileName")]
        public string ArtworkFileName { get; set; }

        // Project format 4: Double Banners use a second independently positioned artwork source.
        // Existing artwork fields remain the primary/Left Banner source for backward compatibility.
        [DataMember(Name = "secondaryArtworkFileName")]
        public string SecondaryArtworkFileName { get; set; }

        [DataMember(Name = "secondaryFitMode")]
        public string SecondaryFitMode { get; set; }

        [DataMember(Name = "secondaryZoom")]
        public float SecondaryZoom { get; set; }

        [DataMember(Name = "secondaryOffsetX")]
        public float SecondaryOffsetX { get; set; }

        [DataMember(Name = "secondaryOffsetY")]
        public float SecondaryOffsetY { get; set; }

        [DataMember(Name = "secondaryRotationDegrees")]
        public float SecondaryRotationDegrees { get; set; }

        [DataMember(Name = "secondaryGuideColor")]
        public string SecondaryGuideColor { get; set; }

        [DataMember(Name = "secondaryReferenceWidth")]
        public int SecondaryReferenceWidth { get; set; }

        [DataMember(Name = "secondaryReferenceHeight")]
        public int SecondaryReferenceHeight { get; set; }

        [DataMember(Name = "customIconFileName")]
        public string CustomIconFileName { get; set; }

        [DataMember(Name = "outputRoot")]
        public string OutputRoot { get; set; }

        [DataMember(Name = "lastBuiltOutputPath")]
        public string LastBuiltOutputPath { get; set; }

        [DataMember(Name = "createdUtc")]
        public string CreatedUtc { get; set; }

        [DataMember(Name = "lastBuiltUtc")]
        public string LastBuiltUtc { get; set; }

        [DataMember(Name = "lastEditedUtc")]
        public string LastEditedUtc { get; set; }

        [DataMember(Name = "pendingChanges")]
        public bool PendingChanges { get; set; }

        // Project format 6: records whether the latest installed output is a standalone
        // package or one combined BL-020 variant-family package. Family builds deliberately
        // give every member the same output folder and membership snapshot.
        [DataMember(Name = "lastBuildPackageMode")]
        public string LastBuildPackageMode { get; set; }

        [DataMember(Name = "lastBuiltFamilyKey")]
        public string LastBuiltFamilyKey { get; set; }

        [DataMember(Name = "lastBuiltFamilyName")]
        public string LastBuiltFamilyName { get; set; }

        [DataMember(Name = "lastBuiltFamilyMemberIds")]
        public List<string> LastBuiltFamilyMemberIds { get; set; }

        // Project format 7: Workshop publication can now belong to the complete BL-020
        // family package rather than one individual project. Family state is intentionally
        // duplicated across members so any child can resolve the shared Workshop identity
        // without introducing a second database/source of truth.
        [DataMember(Name = "workshopPackageMode")]
        public string WorkshopPackageMode { get; set; }

        [DataMember(Name = "workshopFamilyKey")]
        public string WorkshopFamilyKey { get; set; }

        [DataMember(Name = "workshopFamilyName")]
        public string WorkshopFamilyName { get; set; }

        [DataMember(Name = "workshopFamilyMemberIds")]
        public List<string> WorkshopFamilyMemberIds { get; set; }

        // Steam Workshop publication state is stored with the local project so
        // a later update always targets the same PublishedFileId rather than creating a duplicate.
        [DataMember(Name = "workshopPublishedFileId")]
        public string WorkshopPublishedFileId { get; set; }

        [DataMember(Name = "workshopTitle")]
        public string WorkshopTitle { get; set; }

        [DataMember(Name = "workshopDescription")]
        public string WorkshopDescription { get; set; }

        [DataMember(Name = "workshopVisibility")]
        public string WorkshopVisibility { get; set; }

        [DataMember(Name = "workshopPreviewFileName")]
        public string WorkshopPreviewFileName { get; set; }

        [DataMember(Name = "workshopLastChangeNote")]
        public string WorkshopLastChangeNote { get; set; }

        [DataMember(Name = "workshopLastPublishedUtc")]
        public string WorkshopLastPublishedUtc { get; set; }

        [DataMember(Name = "workshopLastUpdatedUtc")]
        public string WorkshopLastUpdatedUtc { get; set; }

        [DataMember(Name = "workshopNeedsLegalAgreement")]
        public bool WorkshopNeedsLegalAgreement { get; set; }

        // Last Workshop "Required Item" dependency that Memento Maker successfully
        // established for this item's Workshop page. Kept even when a local link is
        // temporarily removed so a later update can safely remove/change the dependency.
        [DataMember(Name = "workshopDependencyPublishedFileId")]
        public string WorkshopDependencyPublishedFileId { get; set; }

        // A Workshop ID is a link that must be revalidated against Steam.
        // Missing links are cleared from the active id before any future upload can occur;
        // the previous id is retained for audit/recovery purposes only.
        [DataMember(Name = "workshopLinkState")]
        public string WorkshopLinkState { get; set; }

        [DataMember(Name = "workshopPreviousPublishedFileId")]
        public string WorkshopPreviousPublishedFileId { get; set; }

        [DataMember(Name = "workshopLinkLastCheckedUtc")]
        public string WorkshopLinkLastCheckedUtc { get; set; }

        // Relinking an existing Steam item establishes identity, but does not
        // prove that the Workshop content matches the current local build. Keep the
        // Workshop state yellow until one successful update has been submitted.
        [DataMember(Name = "workshopLinkNeedsUpdate")]
        public bool WorkshopLinkNeedsUpdate { get; set; }
    }

    internal static class VariantModes
    {
        public const string Standalone = "Standalone";
        public const string BaseGame = "BaseGame";
        public const string Modded = "Modded";

        public static string Normalise(string value)
        {
            if (string.Equals(value, BaseGame, StringComparison.OrdinalIgnoreCase))
                return BaseGame;
            if (string.Equals(value, Modded, StringComparison.OrdinalIgnoreCase))
                return Modded;
            return Standalone;
        }
    }

    internal static class BuildPackageModes
    {
        public const string Single = "Single";
        public const string Family = "Family";
        public const string DecorPack = "DecorPack";

        public static string Normalise(string value)
        {
            if (string.Equals(value, Family, StringComparison.OrdinalIgnoreCase))
                return Family;
            if (string.Equals(value, DecorPack, StringComparison.OrdinalIgnoreCase))
                return DecorPack;
            return Single;
        }

        public static bool IsCombined(string value)
        {
            string mode = Normalise(value);
            return mode == Family || mode == DecorPack;
        }
    }

    internal sealed class VariantParentChoice
    {
        public string Mode { get; set; }
        public string ModId { get; set; }
        public string Name { get; set; }
        public string ItemType { get; set; }
        public bool IsSeparator { get; set; }

        public override string ToString()
        {
            if (IsSeparator)
                return string.Empty;
            string mode = VariantModes.Normalise(Mode);
            if (mode == VariantModes.BaseGame)
                return "Base Game Item - " + (Name ?? "");
            if (mode == VariantModes.Standalone)
                return "Standalone Item / Parent Item";
            string suffix = string.IsNullOrEmpty(ItemType) ? "" : " (" + ItemType + ")";
            return (Name ?? ModId ?? "Mod") + suffix;
        }
    }

    // Variant selection needs a visual group break, but the separator must never become a
    // valid project value. This owner-drawn combo renders a compact rule and immediately
    // restores the previous real choice if the separator is clicked or reached by keyboard.
    internal sealed class VariantParentComboBox : ComboBox
    {
        private int _lastValidIndex = -1;
        private bool _restoringSelection;

        public VariantParentComboBox()
        {
            DrawMode = DrawMode.OwnerDrawVariable;
        }

        protected override void OnDropDown(EventArgs e)
        {
            if (!IsSeparatorIndex(SelectedIndex))
                _lastValidIndex = SelectedIndex;
            base.OnDropDown(e);
        }

        protected override void OnSelectedIndexChanged(EventArgs e)
        {
            if (_restoringSelection)
                return;

            if (IsSeparatorIndex(SelectedIndex))
            {
                int restore = FindValidIndex(_lastValidIndex);
                _restoringSelection = true;
                try { SelectedIndex = restore; }
                finally { _restoringSelection = false; }
                return;
            }

            _lastValidIndex = SelectedIndex;
            base.OnSelectedIndexChanged(e);
        }

        protected override void OnMeasureItem(MeasureItemEventArgs e)
        {
            if (e.Index >= 0 && IsSeparatorIndex(e.Index))
            {
                e.ItemHeight = 9;
                e.ItemWidth = Math.Max(1, Width - 12);
                return;
            }

            e.ItemHeight = Math.Max(20, Font == null ? 20 : Font.Height + 6);
            e.ItemWidth = Math.Max(1, Width - 12);
        }

        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            if (e.Index < 0)
                return;

            if (IsSeparatorIndex(e.Index))
            {
                using (SolidBrush background = new SolidBrush(BackColor))
                    e.Graphics.FillRectangle(background, e.Bounds);
                int y = e.Bounds.Top + (e.Bounds.Height / 2);
                using (Pen line = new Pen(Color.FromArgb(184, 163, 120)))
                    e.Graphics.DrawLine(line, e.Bounds.Left + 8, y, e.Bounds.Right - 8, y);
                return;
            }

            e.DrawBackground();
            VariantParentChoice choice = Items[e.Index] as VariantParentChoice;
            string label = choice == null ? Convert.ToString(Items[e.Index]) : choice.ToString();
            Color textColor = (e.State & DrawItemState.Selected) == DrawItemState.Selected
                ? SystemColors.HighlightText
                : ForeColor;
            TextRenderer.DrawText(e.Graphics, label ?? string.Empty, Font,
                new Rectangle(e.Bounds.Left + 3, e.Bounds.Top, Math.Max(1, e.Bounds.Width - 6), e.Bounds.Height),
                textColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            e.DrawFocusRectangle();
        }

        private bool IsSeparatorIndex(int index)
        {
            if (index < 0 || index >= Items.Count)
                return false;
            VariantParentChoice choice = Items[index] as VariantParentChoice;
            return choice != null && choice.IsSeparator;
        }

        private int FindValidIndex(int preferred)
        {
            if (preferred >= 0 && preferred < Items.Count && !IsSeparatorIndex(preferred))
                return preferred;
            for (int i = 0; i < Items.Count; i++)
            {
                if (!IsSeparatorIndex(i))
                    return i;
            }
            return -1;
        }
    }

    public sealed class MyModsSelection
    {
        public string Action { get; set; }
        public ModProjectRecord Project { get; set; }
        public List<ModProjectRecord> Projects { get; set; }
        public string FamilyKey { get; set; }
        public string FamilyName { get; set; }
        public string PackageMode { get; set; }
    }

    [DataContract]
    public sealed class EnvironmentMarker
    {
        [DataMember(Name = "markerFormatVersion")]
        public int MarkerFormatVersion { get; set; }

        [DataMember(Name = "automationVersion")]
        public string AutomationVersion { get; set; }

        [DataMember(Name = "automationFingerprint")]
        public string AutomationFingerprint { get; set; }

        [DataMember(Name = "sdkZipPath")]
        public string SdkZipPath { get; set; }

        [DataMember(Name = "sdkZipLength")]
        public long SdkZipLength { get; set; }

        [DataMember(Name = "sdkZipLastWriteUtc")]
        public string SdkZipLastWriteUtc { get; set; }
    }


    [DataContract]
    public sealed class WorkshopFamilyState
    {
        [DataMember(Name = "formatVersion")]
        public int FormatVersion { get; set; }

        [DataMember(Name = "familyKey")]
        public string FamilyKey { get; set; }

        [DataMember(Name = "familyName")]
        public string FamilyName { get; set; }

        [DataMember(Name = "packageMode")]
        public string PackageMode { get; set; }

        [DataMember(Name = "publishedFileId")]
        public string PublishedFileId { get; set; }

        [DataMember(Name = "previousPublishedFileId")]
        public string PreviousPublishedFileId { get; set; }

        [DataMember(Name = "memberIds")]
        public List<string> MemberIds { get; set; }

        [DataMember(Name = "linkState")]
        public string LinkState { get; set; }

        [DataMember(Name = "linkNeedsUpdate")]
        public bool LinkNeedsUpdate { get; set; }

        [DataMember(Name = "title")]
        public string Title { get; set; }

        [DataMember(Name = "description")]
        public string Description { get; set; }

        [DataMember(Name = "visibility")]
        public string Visibility { get; set; }

        [DataMember(Name = "lastChangeNote")]
        public string LastChangeNote { get; set; }

        [DataMember(Name = "lastPublishedUtc")]
        public string LastPublishedUtc { get; set; }

        [DataMember(Name = "lastUpdatedUtc")]
        public string LastUpdatedUtc { get; set; }

        [DataMember(Name = "lastCheckedUtc")]
        public string LastCheckedUtc { get; set; }

        [DataMember(Name = "needsLegalAgreement")]
        public bool NeedsLegalAgreement { get; set; }
    }

    [DataContract]
    public sealed class WorkshopItemSummary
    {
        [DataMember(Name = "publishedFileId")]
        public string PublishedFileId { get; set; }

        [DataMember(Name = "title")]
        public string Title { get; set; }

        [DataMember(Name = "description")]
        public string Description { get; set; }

        [DataMember(Name = "tags")]
        public string Tags { get; set; }

        [DataMember(Name = "visibility")]
        public string Visibility { get; set; }

        [DataMember(Name = "metadata")]
        public string Metadata { get; set; }
    }

    [DataContract]
    public sealed class WorkshopQueryResult
    {
        [DataMember(Name = "success")]
        public bool Success { get; set; }

        [DataMember(Name = "message")]
        public string Message { get; set; }

        [DataMember(Name = "steamUserId")]
        public string SteamUserId { get; set; }

        [DataMember(Name = "steamPersonaName")]
        public string SteamPersonaName { get; set; }

        [DataMember(Name = "items")]
        public List<WorkshopItemSummary> Items { get; set; }

        [DataMember(Name = "utcCompleted")]
        public string UtcCompleted { get; set; }
    }



    [DataContract]
    public sealed class WorkshopLegacyItem
    {
        [DataMember(Name = "publishedFileId")]
        public string PublishedFileId { get; set; }

        [DataMember(Name = "title")]
        public string Title { get; set; }
    }

    [DataContract]
    public sealed class WorkshopPublishJob
    {
        [DataMember(Name = "publishedFileId")]
        public string PublishedFileId { get; set; }

        [DataMember(Name = "title")]
        public string Title { get; set; }

        [DataMember(Name = "description")]
        public string Description { get; set; }

        [DataMember(Name = "contentPath")]
        public string ContentPath { get; set; }

        [DataMember(Name = "previewPath")]
        public string PreviewPath { get; set; }

        [DataMember(Name = "additionalPreviewPaths")]
        public List<string> AdditionalPreviewPaths { get; set; }

        [DataMember(Name = "replaceAdditionalPreviews")]
        public bool ReplaceAdditionalPreviews { get; set; }

        [DataMember(Name = "legacyItemsToDeprecate")]
        public List<WorkshopLegacyItem> LegacyItemsToDeprecate { get; set; }

        [DataMember(Name = "visibility")]
        public string Visibility { get; set; }

        [DataMember(Name = "tags")]
        public List<string> Tags { get; set; }

        [DataMember(Name = "changeNote")]
        public string ChangeNote { get; set; }

        [DataMember(Name = "mementoModId")]
        public string MementoModId { get; set; }

        [DataMember(Name = "requiredPublishedFileId")]
        public string RequiredPublishedFileId { get; set; }

        [DataMember(Name = "previousRequiredPublishedFileId")]
        public string PreviousRequiredPublishedFileId { get; set; }
    }

    [DataContract]
    public sealed class WorkshopPublishResult
    {
        [DataMember(Name = "success")]
        public bool Success { get; set; }

        [DataMember(Name = "message")]
        public string Message { get; set; }

        [DataMember(Name = "publishedFileId")]
        public string PublishedFileId { get; set; }

        [DataMember(Name = "createdNew")]
        public bool CreatedNew { get; set; }

        [DataMember(Name = "needsLegalAgreement")]
        public bool NeedsLegalAgreement { get; set; }

        [DataMember(Name = "dependencyUpdated")]
        public bool DependencyUpdated { get; set; }

        [DataMember(Name = "dependencyPublishedFileId")]
        public string DependencyPublishedFileId { get; set; }

        [DataMember(Name = "steamUserId")]
        public string SteamUserId { get; set; }

        [DataMember(Name = "steamPersonaName")]
        public string SteamPersonaName { get; set; }

        [DataMember(Name = "additionalPreviewCount")]
        public int AdditionalPreviewCount { get; set; }

        [DataMember(Name = "deprecatedItemCount")]
        public int DeprecatedItemCount { get; set; }

        [DataMember(Name = "warnings")]
        public List<string> Warnings { get; set; }

        [DataMember(Name = "utcCompleted")]
        public string UtcCompleted { get; set; }
    }

    [DataContract]
    public sealed class UnityWorkerCommandRequest
    {
        [DataMember(Name = "commandId")]
        public string CommandId { get; set; }

        [DataMember(Name = "command")]
        public string Command { get; set; }

        [DataMember(Name = "jobPath")]
        public string JobPath { get; set; }

        [DataMember(Name = "batchJobPath")]
        public string BatchJobPath { get; set; }

        [DataMember(Name = "resultPath")]
        public string ResultPath { get; set; }

        [DataMember(Name = "workshopJobPath")]
        public string WorkshopJobPath { get; set; }

        [DataMember(Name = "utcCreated")]
        public string UtcCreated { get; set; }
    }

    [DataContract]
    public sealed class UnityWorkerCommandResult
    {
        [DataMember(Name = "success")]
        public bool Success { get; set; }

        [DataMember(Name = "commandId")]
        public string CommandId { get; set; }

        [DataMember(Name = "message")]
        public string Message { get; set; }

        [DataMember(Name = "utcCompleted")]
        public string UtcCompleted { get; set; }
    }

    public sealed class EnvironmentHealthReport
    {
        public string Status { get; set; }
        public string Message { get; set; }
        public bool ProjectValid { get; set; }
        public bool AutomationPresent { get; set; }
        public bool AutomationCurrent { get; set; }
        public bool SdkChanged { get; set; }
        public bool NeedsRepair { get; set; }
        public bool NeedsRebuild { get; set; }
        public bool IsUsable { get; set; }
    }

    public sealed class PrerequisiteState
    {
        public string UnityExePath { get; set; }
        public string UnityHubPath { get; set; }
        public string SdkZipPath { get; set; }
        public bool UnityFound { get { return !string.IsNullOrEmpty(UnityExePath); } }
        public bool UnityHubFound { get { return !string.IsNullOrEmpty(UnityHubPath); } }
        public bool SdkFound { get { return !string.IsNullOrEmpty(SdkZipPath); } }
        public bool EnvironmentReady { get; set; }
        public string EnvironmentProjectPath { get; set; }
        public EnvironmentHealthReport EnvironmentHealth { get; set; }
        public string EnvironmentStatusMessage { get; set; }
    }
}
