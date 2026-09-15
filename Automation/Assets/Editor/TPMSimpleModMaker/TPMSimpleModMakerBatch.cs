using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using TPS.Core.Rendering;
using TPS.Game;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.U2D;

namespace TPMSimpleModMaker
{
    /// <summary>
    /// Automation backend for the Two Point Museum Simple Mod Maker.
    /// Supports Mural, Small/Standard/Tall Poster, Single Banner and Double Banner themes, plus the unified 16-rug replacement-mesh/material pipeline and custom item icons via SpriteAtlas.
    ///
    /// Called from Unity with:
    /// -executeMethod TPMSimpleModMaker.TPMSimpleModMakerBatch.BuildFromCommandLine
    /// -jobPath "C:\\path\\to\\job.json"
    /// </summary>
    public static class TPMSimpleModMakerBatch
    {
        private const string TemplateMural = "Mural";
        private const string TemplateWallpaper = "Wallpaper";
        private const string TemplateLegacyRug = "Rug";
        private const string TemplateLegacyPoster = "Poster";
        private const string TemplateSmallPoster = "Small Poster";
        private const string TemplateStandardPoster = "Standard Poster";
        private const string TemplateTallPoster = "Tall Poster";

        // The official Two Point mural example remains our safe ScriptableObject scaffold.
        private const string MuralTemplateConfigPath = "Assets/ExampleMods/MuralMod/ModConfig/MuralModConfig.asset";

        // BL-022 Wallpaper uses the user's known-working RoomVisual gold standard as its
        // serialization/material scaffold. Creating RoomVisualModConfig with CreateInstance()
        // produced an export that built successfully but was ignored by the game. The gold
        // standard carries the exact RoomVisual config version/default serialization expected
        // by the current Two Point Museum SDK. Generated wallpapers copy these two assets and
        // then replace all user-specific values, IDs, textures and icon references.
        private const string WallpaperConfigTemplatePath = "Assets/TPMSimpleModMaker/WallpaperTemplate/GoldStandard/Wallpaper.asset";
        private const string WallpaperMaterialTemplatePath = "Assets/TPMSimpleModMaker/WallpaperTemplate/GoldStandard/Wallpaper_Mat.mat";

        // Material templates taken from the user's manually working rug mods.
        // They preserve the exact Two Point/Lit settings used by the official non-texture-only workflow.
        private const string MarketingRugMaterialTemplatePath = "Assets/TPMSimpleModMaker/RugMaterialTemplates/MarketingRugTemplate.mat";
        private const string StaffRugMaterialTemplatePath = "Assets/TPMSimpleModMaker/RugMaterialTemplates/StaffRugTemplate.mat";
        private const string StaffRugTintMaskPath = "Assets/TPMSimpleModMaker/RugMaterialTemplates/StaffRugTintMask.png";

        private const long MuralBaseArchetypeId = -135028373L;
        private const string MuralBaseItemName = "Unihornus Mural";
        private const string MuralMeshName = "A_Prop_WhiteboxMural_V2";

        private sealed class RugDefinition
        {
            public string TemplateName;
            public long BaseArchetypeId;
            public string BaseItemName;
            public bool Recolourable;
            public string[] MeshNames;
            public string ReplacementFbxAssetPath;
        }

        private sealed class BannerDefinition
        {
            public string TemplateName;
            public long BaseArchetypeId;
            public string BaseItemName;
            public bool Fantasy;
            public bool DigiverseSpecialLayout;
            public string ArtworkMeshName;
            public string[] HiddenArtworkMeshNames;
            public string ReplacementFbxAssetPath;
            public string MaterialTemplatePath;
        }

        private const string GeneralBannerArtworkMeshPath = "Assets/TPMSimpleModMaker/BannerTemplates/General/A_Decoration_General_IndoorBanner_Image_V1.fbx";
        private const string DigiverseBannerArtworkMeshPath = "Assets/TPMSimpleModMaker/BannerTemplates/General/A_Decoration_General_IndoorBanner_Image_V2.fbx";
        private const string GeneralBannerArtworkMaterialPath = "Assets/TPMSimpleModMaker/BannerTemplates/General/M_Banner_Image.mat";
        private const string FantasyBannerArtworkMeshPath = "Assets/TPMSimpleModMaker/BannerTemplates/Fantasy/Banner_Artwork_V1.fbx";
        private const string FantasyBannerArtworkMaterialPath = "Assets/TPMSimpleModMaker/BannerTemplates/Fantasy/M_Decoration_Fantasy_Banner_Artwork_V1.mat";

        // Digiverse differs from the other standard Single Banners because its active artwork
        // surface is Image_V2. Image_V1 must be explicitly hidden. Structural LOD mesh/material
        // references remain inherited from the archetype because their GUIDs are base-game-only
        // and cannot be resolved from the Modding SDK.

        // BL-008 Single Banner themes. ItemDefIDs are supplied by the user from the SDK.
        // The user-facing General option resolves to Winter Banner inside the SDK.
        private static readonly BannerDefinition[] BannerDefinitions =
        {
            new BannerDefinition { TemplateName = "Prehistory Banner", BaseArchetypeId = -1853073767L, BaseItemName = "Prehistory Banner", ArtworkMeshName = "A_Decoration_General_IndoorBanner_Image_V1", ReplacementFbxAssetPath = GeneralBannerArtworkMeshPath, MaterialTemplatePath = GeneralBannerArtworkMaterialPath },
            new BannerDefinition { TemplateName = "Botany Banner", BaseArchetypeId = -1744970628L, BaseItemName = "Botany Banner", ArtworkMeshName = "A_Decoration_General_IndoorBanner_Image_V1", ReplacementFbxAssetPath = GeneralBannerArtworkMeshPath, MaterialTemplatePath = GeneralBannerArtworkMaterialPath },
            new BannerDefinition { TemplateName = "Marine Life Banner", BaseArchetypeId = -2051644094L, BaseItemName = "Marine Life Banner", ArtworkMeshName = "A_Decoration_General_IndoorBanner_Image_V1", ReplacementFbxAssetPath = GeneralBannerArtworkMeshPath, MaterialTemplatePath = GeneralBannerArtworkMaterialPath },
            new BannerDefinition { TemplateName = "Supernatural Banner", BaseArchetypeId = -632094197L, BaseItemName = "Supernatural Banner", ArtworkMeshName = "A_Decoration_General_IndoorBanner_Image_V1", ReplacementFbxAssetPath = GeneralBannerArtworkMeshPath, MaterialTemplatePath = GeneralBannerArtworkMaterialPath },
            new BannerDefinition { TemplateName = "Space Banner", BaseArchetypeId = -902001229L, BaseItemName = "Space Banner", ArtworkMeshName = "A_Decoration_General_IndoorBanner_Image_V1", ReplacementFbxAssetPath = GeneralBannerArtworkMeshPath, MaterialTemplatePath = GeneralBannerArtworkMaterialPath },
            new BannerDefinition { TemplateName = "Science Banner", BaseArchetypeId = -646703342L, BaseItemName = "Science Banner", ArtworkMeshName = "A_Decoration_General_IndoorBanner_Image_V1", ReplacementFbxAssetPath = GeneralBannerArtworkMeshPath, MaterialTemplatePath = GeneralBannerArtworkMaterialPath },
            new BannerDefinition { TemplateName = "Fantasy Banner", BaseArchetypeId = -98622681L, BaseItemName = "Fantasy Banner", Fantasy = true, ArtworkMeshName = "Banner_Artwork_V1", HiddenArtworkMeshNames = new[] { "Banner_Artwork_V2", "Banner_Artwork_V3", "Banner_Artwork_V4" }, ReplacementFbxAssetPath = FantasyBannerArtworkMeshPath, MaterialTemplatePath = FantasyBannerArtworkMaterialPath },
            new BannerDefinition { TemplateName = "Digiverse Banner", BaseArchetypeId = -1842906215L, BaseItemName = "Digiverse Banner", DigiverseSpecialLayout = true, ArtworkMeshName = "A_Decoration_General_IndoorBanner_Image_V2", HiddenArtworkMeshNames = new[] { "A_Decoration_General_IndoorBanner_Image_V1" }, ReplacementFbxAssetPath = DigiverseBannerArtworkMeshPath, MaterialTemplatePath = GeneralBannerArtworkMaterialPath },
            new BannerDefinition { TemplateName = "Wildlife Banner", BaseArchetypeId = -1375898410L, BaseItemName = "Wildlife Banner", ArtworkMeshName = "A_Decoration_General_IndoorBanner_Image_V1", ReplacementFbxAssetPath = GeneralBannerArtworkMeshPath, MaterialTemplatePath = GeneralBannerArtworkMaterialPath },
            new BannerDefinition { TemplateName = "Art Banner", BaseArchetypeId = -925923576L, BaseItemName = "Art Banner", ArtworkMeshName = "A_Decoration_General_IndoorBanner_Image_V1", ReplacementFbxAssetPath = GeneralBannerArtworkMeshPath, MaterialTemplatePath = GeneralBannerArtworkMaterialPath },
            new BannerDefinition { TemplateName = "General Banner", BaseArchetypeId = -2096313153L, BaseItemName = "Winter Banner", ArtworkMeshName = "A_Decoration_General_IndoorBanner_Image_V1", ReplacementFbxAssetPath = GeneralBannerArtworkMeshPath, MaterialTemplatePath = GeneralBannerArtworkMaterialPath }
        };

        private sealed class DoubleBannerDefinition
        {
            public string TemplateName;
            public long BaseArchetypeId;
            public string BaseItemName;
            public bool Fantasy;
            public string[] ArtworkMeshNames;
            public string[] HiddenArtworkMeshNames;
            public string[] ReplacementFbxAssetPaths;
            public string MaterialTemplatePath;
        }

        private sealed class HangingSignDefinition
        {
            public string TemplateName;
            public int BaseArchetypeId;
            public string BaseItemName;
            public string ConfigTemplatePath;
            public string ReplacementFbxAssetPath;
            public string SignMaterialTemplatePath;
            public string FrameMaterialPath;
            public string SuspendersMaterialPath;
        }

        private sealed class WallSignDefinition
        {
            public string TemplateName;
            public long BaseArchetypeId;
            public string BaseItemName;
            public string ConfigTemplatePath;
            public string ReplacementFbxAssetPath;
            public string SignMaterialTemplatePath;
            public string BracketMaterialPath;
            public string FrameMaterialPath;
        }

        private const string GeneralDoubleBannerMeshV1Path = "Assets/TPMSimpleModMaker/DoubleBannerTemplates/General/A_Decoration_General_OutdoorBanner_Image_V1.fbx";
        private const string GeneralDoubleBannerMeshV2Path = "Assets/TPMSimpleModMaker/DoubleBannerTemplates/General/A_Decoration_General_OutdoorBanner_Image_V2.fbx";
        private const string GeneralDoubleBannerMaterialPath = "Assets/TPMSimpleModMaker/DoubleBannerTemplates/General/M_Banner_Image.mat";
        private const string FantasyDoubleBannerMeshPath = "Assets/TPMSimpleModMaker/DoubleBannerTemplates/Fantasy/Banner_Artwork_V1.fbx";
        private const string FantasyDoubleBannerMaterialPath = "Assets/TPMSimpleModMaker/DoubleBannerTemplates/Fantasy/M_Decoration_Fantasy_Banner_Artwork_V1.mat";

        // BL-009 Double Banner themes. There is intentionally no Digiverse Double Banner in the SDK.
        private static readonly DoubleBannerDefinition[] DoubleBannerDefinitions =
        {
            new DoubleBannerDefinition { TemplateName = "Prehistory Double Banner", BaseArchetypeId = -527940804L, BaseItemName = "Prehistory Double Banner", ArtworkMeshNames = new[] { "A_Decoration_General_OutdoorBanner_Image_V1", "A_Decoration_General_OutdoorBanner_Image_V2" }, ReplacementFbxAssetPaths = new[] { GeneralDoubleBannerMeshV1Path, GeneralDoubleBannerMeshV2Path }, MaterialTemplatePath = GeneralDoubleBannerMaterialPath },
            new DoubleBannerDefinition { TemplateName = "Botany Double Banner", BaseArchetypeId = -2122111580L, BaseItemName = "Botany Double Banner", ArtworkMeshNames = new[] { "A_Decoration_General_OutdoorBanner_Image_V1", "A_Decoration_General_OutdoorBanner_Image_V2" }, ReplacementFbxAssetPaths = new[] { GeneralDoubleBannerMeshV1Path, GeneralDoubleBannerMeshV2Path }, MaterialTemplatePath = GeneralDoubleBannerMaterialPath },
            new DoubleBannerDefinition { TemplateName = "Marine Life Double Banner", BaseArchetypeId = -464023912L, BaseItemName = "Marine Life Double Banner", ArtworkMeshNames = new[] { "A_Decoration_General_OutdoorBanner_Image_V1", "A_Decoration_General_OutdoorBanner_Image_V2" }, ReplacementFbxAssetPaths = new[] { GeneralDoubleBannerMeshV1Path, GeneralDoubleBannerMeshV2Path }, MaterialTemplatePath = GeneralDoubleBannerMaterialPath },
            new DoubleBannerDefinition { TemplateName = "Supernatural Double Banner", BaseArchetypeId = -194980220L, BaseItemName = "Supernatural Double Banner", ArtworkMeshNames = new[] { "A_Decoration_General_OutdoorBanner_Image_V1", "A_Decoration_General_OutdoorBanner_Image_V2" }, ReplacementFbxAssetPaths = new[] { GeneralDoubleBannerMeshV1Path, GeneralDoubleBannerMeshV2Path }, MaterialTemplatePath = GeneralDoubleBannerMaterialPath },
            new DoubleBannerDefinition { TemplateName = "Space Double Banner", BaseArchetypeId = -1092587038L, BaseItemName = "Space Double Banner", ArtworkMeshNames = new[] { "A_Decoration_General_OutdoorBanner_Image_V1", "A_Decoration_General_OutdoorBanner_Image_V2" }, ReplacementFbxAssetPaths = new[] { GeneralDoubleBannerMeshV1Path, GeneralDoubleBannerMeshV2Path }, MaterialTemplatePath = GeneralDoubleBannerMaterialPath },
            new DoubleBannerDefinition { TemplateName = "Science Double Banner", BaseArchetypeId = -1384196411L, BaseItemName = "Science Double Banner", ArtworkMeshNames = new[] { "A_Decoration_General_OutdoorBanner_Image_V1", "A_Decoration_General_OutdoorBanner_Image_V2" }, ReplacementFbxAssetPaths = new[] { GeneralDoubleBannerMeshV1Path, GeneralDoubleBannerMeshV2Path }, MaterialTemplatePath = GeneralDoubleBannerMaterialPath },
            new DoubleBannerDefinition { TemplateName = "Fantasy Double Banner", BaseArchetypeId = -788679179L, BaseItemName = "Fantasy Double Banner", Fantasy = true, ArtworkMeshNames = new[] { "Banner_Artwork_V1" }, HiddenArtworkMeshNames = new[] { "Banner_Artwork_V2", "Banner_Artwork_V3", "Banner_Artwork_V4" }, ReplacementFbxAssetPaths = new[] { FantasyDoubleBannerMeshPath }, MaterialTemplatePath = FantasyDoubleBannerMaterialPath },
            new DoubleBannerDefinition { TemplateName = "Wildlife Double Banner", BaseArchetypeId = -1654606174L, BaseItemName = "Wildlife Double Banner", ArtworkMeshNames = new[] { "A_Decoration_General_OutdoorBanner_Image_V1", "A_Decoration_General_OutdoorBanner_Image_V2" }, ReplacementFbxAssetPaths = new[] { GeneralDoubleBannerMeshV1Path, GeneralDoubleBannerMeshV2Path }, MaterialTemplatePath = GeneralDoubleBannerMaterialPath },
            new DoubleBannerDefinition { TemplateName = "Art Double Banner", BaseArchetypeId = -834184718L, BaseItemName = "Art Double Banner", ArtworkMeshNames = new[] { "A_Decoration_General_OutdoorBanner_Image_V1", "A_Decoration_General_OutdoorBanner_Image_V2" }, ReplacementFbxAssetPaths = new[] { GeneralDoubleBannerMeshV1Path, GeneralDoubleBannerMeshV2Path }, MaterialTemplatePath = GeneralDoubleBannerMaterialPath },
            new DoubleBannerDefinition { TemplateName = "General Double Banner", BaseArchetypeId = -1050588136L, BaseItemName = "General Double Banner", ArtworkMeshNames = new[] { "A_Decoration_General_OutdoorBanner_Image_V1", "A_Decoration_General_OutdoorBanner_Image_V2" }, ReplacementFbxAssetPaths = new[] { GeneralDoubleBannerMeshV1Path, GeneralDoubleBannerMeshV2Path }, MaterialTemplatePath = GeneralDoubleBannerMaterialPath }
        };

        private const string HangingSignTemplateRoot = "Assets/TPMSimpleModMaker/HangingSignTemplates";
        private const string SmallHangingSignConfigTemplatePath = HangingSignTemplateRoot + "/Small/Small_Hanging_Sign.asset";
        private const string SmallHangingSignSignMaterialTemplatePath = HangingSignTemplateRoot + "/Small/Sign.mat";
        private const string SmallHangingSignFrameMaterialPath = HangingSignTemplateRoot + "/Small/00_Frame.mat";
        private const string SmallHangingSignSuspendersMaterialPath = HangingSignTemplateRoot + "/Small/00_Suspenders.mat";
        private const string LargeHangingSignConfigTemplatePath = HangingSignTemplateRoot + "/Large/Large_Hanging_Sign.asset";
        private const string LargeHangingSignSignMaterialTemplatePath = HangingSignTemplateRoot + "/Large/Sign.mat";
        private const string LargeHangingSignFrameMaterialPath = HangingSignTemplateRoot + "/Large/00_Frame.mat";
        private const string LargeHangingSignSuspendersMaterialPath = HangingSignTemplateRoot + "/Large/00_Suspenders.mat";

        private static readonly HangingSignDefinition[] HangingSignDefinitions =
        {
            new HangingSignDefinition { TemplateName = "Small Hanging Sign", BaseArchetypeId = -316190909, BaseItemName = "Marketing Rectangle Rug", ConfigTemplatePath = SmallHangingSignConfigTemplatePath, ReplacementFbxAssetPath = HangingSignTemplateRoot + "/Small/Hanging_Sign_Small.fbx", SignMaterialTemplatePath = SmallHangingSignSignMaterialTemplatePath, FrameMaterialPath = SmallHangingSignFrameMaterialPath, SuspendersMaterialPath = SmallHangingSignSuspendersMaterialPath },
            new HangingSignDefinition { TemplateName = "Large Hanging Sign", BaseArchetypeId = -316190909, BaseItemName = "Marketing Rectangle Rug", ConfigTemplatePath = LargeHangingSignConfigTemplatePath, ReplacementFbxAssetPath = HangingSignTemplateRoot + "/Large/Hanging_Sign_Large.fbx", SignMaterialTemplatePath = LargeHangingSignSignMaterialTemplatePath, FrameMaterialPath = LargeHangingSignFrameMaterialPath, SuspendersMaterialPath = LargeHangingSignSuspendersMaterialPath },
        };

        private const string WallSignTemplateRoot = "Assets/TPMSimpleModMaker/WallSignTemplates";
        private const string SmallWallSignConfigTemplatePath = WallSignTemplateRoot + "/Small/Wall Sign - Small.asset";
        private const string SmallWallSignFbxPath = WallSignTemplateRoot + "/Small/SmallWallSign.fbx";
        private const string SmallWallSignSignMaterialPath = WallSignTemplateRoot + "/Small/Sign.mat";
        private const string SmallWallSignBracketMaterialPath = WallSignTemplateRoot + "/Small/Bracket.mat";
        private const string SmallWallSignFrameMaterialPath = WallSignTemplateRoot + "/Small/Frame.mat";
        private const string LargeWallSignConfigTemplatePath = WallSignTemplateRoot + "/Large/Wall Sign - Large.asset";
        private const string LargeWallSignFbxPath = WallSignTemplateRoot + "/Large/LargeWallSign.fbx";
        private const string LargeWallSignSignMaterialPath = WallSignTemplateRoot + "/Large/Sign.mat";
        private const string LargeWallSignBracketMaterialPath = WallSignTemplateRoot + "/Large/Bracket.mat";
        private const string LargeWallSignFrameMaterialPath = WallSignTemplateRoot + "/Large/Frame.mat";

        private static readonly WallSignDefinition[] WallSignDefinitions =
        {
            new WallSignDefinition { TemplateName = "Small Wall Sign", BaseArchetypeId = -2142682447L, BaseItemName = "Wall Clock", ConfigTemplatePath = SmallWallSignConfigTemplatePath, ReplacementFbxAssetPath = SmallWallSignFbxPath, SignMaterialTemplatePath = SmallWallSignSignMaterialPath, BracketMaterialPath = SmallWallSignBracketMaterialPath, FrameMaterialPath = SmallWallSignFrameMaterialPath },
            new WallSignDefinition { TemplateName = "Large Wall Sign", BaseArchetypeId = -2142682447L, BaseItemName = "Wall Clock", ConfigTemplatePath = LargeWallSignConfigTemplatePath, ReplacementFbxAssetPath = LargeWallSignFbxPath, SignMaterialTemplatePath = LargeWallSignSignMaterialPath, BracketMaterialPath = LargeWallSignBracketMaterialPath, FrameMaterialPath = LargeWallSignFrameMaterialPath },
        };

        // Rug data verified against Assets/Data/Game/ModItemSpecs/ItemModSpecs.asset in the supplied SDK.
        // Staff rugs use the V1 rug material and are the recolourable family.
        // Marketing rugs use the V2 rug material and are the non-recolourable family.
        private static readonly RugDefinition[] RugDefinitions =
        {
            new RugDefinition { TemplateName = "Staff Circle Rug", BaseArchetypeId = -7643922L, BaseItemName = "Staff Circle Rug", Recolourable = true, MeshNames = new[] { "A_Room_General_Rug_Circle_V1_LOD0", "A_Room_General_Rug_Circle_V1_LOD1", "A_Room_General_Rug_Circle_V1_LOD2" }, ReplacementFbxAssetPath = "Assets/TPMSimpleModMaker/RugMeshes/A_Room_General_Rug_Circle_V1.fbx" },
            new RugDefinition { TemplateName = "Large Staff Circle Rug", BaseArchetypeId = -328974937L, BaseItemName = "Large Staff Circle Rug", Recolourable = true, MeshNames = new[] { "A_Room_General_Rug_CircleLarge_V1_LOD0", "A_Room_General_Rug_CircleLarge_V1_LOD1", "A_Room_General_Rug_CircleLarge_V1_LOD2" }, ReplacementFbxAssetPath = "Assets/TPMSimpleModMaker/RugMeshes/A_Room_General_Rug_CircleLarge_V1.fbx" },
            new RugDefinition { TemplateName = "Staff Rectangle Rug", BaseArchetypeId = -1248015080L, BaseItemName = "Staff Rectangle Rug", Recolourable = true, MeshNames = new[] { "A_Room_General_Rug_Rectangle_V1_LOD0", "A_Room_General_Rug_Rectangle_V1_LOD1" }, ReplacementFbxAssetPath = "Assets/TPMSimpleModMaker/RugMeshes/A_Room_General_Rug_Rectangle_V1.fbx" },
            new RugDefinition { TemplateName = "Large Staff Rectangle Rug", BaseArchetypeId = -1051801767L, BaseItemName = "Large Staff Rectangle Rug", Recolourable = true, MeshNames = new[] { "A_Room_General_Rug_Rectangle_V1_LOD0", "A_Room_General_Rug_Rectangle_V1_LOD1" }, ReplacementFbxAssetPath = "Assets/TPMSimpleModMaker/RugMeshes/A_Room_General_Rug_RectangleLarge_V1.fbx" },
            new RugDefinition { TemplateName = "Staff Square Rug", BaseArchetypeId = -1831907384L, BaseItemName = "Staff Square Rug", Recolourable = true, MeshNames = new[] { "A_Room_General_Rug_Square_V1_LOD0", "A_Room_General_Rug_Square_V1_LOD1" }, ReplacementFbxAssetPath = "Assets/TPMSimpleModMaker/RugMeshes/A_Room_General_Rug_Square_V1.fbx" },
            new RugDefinition { TemplateName = "Large Staff Square Rug", BaseArchetypeId = -769340165L, BaseItemName = "Large Staff Square Rug", Recolourable = true, MeshNames = new[] { "A_Room_General_Rug_SquareLarge_V1_LOD0", "A_Room_General_Rug_SquareLarge_V1_LOD1" }, ReplacementFbxAssetPath = "Assets/TPMSimpleModMaker/RugMeshes/A_Room_General_Rug_SquareLarge_V1.fbx" },
            new RugDefinition { TemplateName = "Staff Octagon Rug", BaseArchetypeId = -2004620632L, BaseItemName = "Staff Octagon Rug", Recolourable = true, MeshNames = new[] { "A_Room_General_Rug_Octagon_V1_LOD0", "A_Room_General_Rug_Octagon_V1_LOD1" }, ReplacementFbxAssetPath = "Assets/TPMSimpleModMaker/RugMeshes/A_Room_General_Rug_Octagon_V1.fbx" },
            new RugDefinition { TemplateName = "Large Staff Octagon Rug", BaseArchetypeId = -357852689L, BaseItemName = "Large Staff Octagon Rug", Recolourable = true, MeshNames = new[] { "A_Room_General_Rug_OctagonLarge_V1_LOD0", "A_Room_General_Rug_OctagonLarge_V1_LOD1" }, ReplacementFbxAssetPath = "Assets/TPMSimpleModMaker/RugMeshes/A_Room_General_Rug_OctagonLarge_V1.fbx" },

            new RugDefinition { TemplateName = "Marketing Circle Rug", BaseArchetypeId = -528198501L, BaseItemName = "Marketing Circle Rug", Recolourable = false, MeshNames = new[] { "A_Room_General_Rug_Circle_V1_LOD0", "A_Room_General_Rug_Circle_V1_LOD1", "A_Room_General_Rug_Circle_V1_LOD2" }, ReplacementFbxAssetPath = "Assets/TPMSimpleModMaker/RugMeshes/A_Room_General_Rug_Circle_V1.fbx" },
            new RugDefinition { TemplateName = "Large Marketing Circle Rug", BaseArchetypeId = -824837195L, BaseItemName = "Large Marketing Circle Rug", Recolourable = false, MeshNames = new[] { "A_Room_General_Rug_CircleLarge_V1_LOD0", "A_Room_General_Rug_CircleLarge_V1_LOD1", "A_Room_General_Rug_CircleLarge_V1_LOD2" }, ReplacementFbxAssetPath = "Assets/TPMSimpleModMaker/RugMeshes/A_Room_General_Rug_CircleLarge_V1.fbx" },
            new RugDefinition { TemplateName = "Marketing Rectangle Rug", BaseArchetypeId = -316190909L, BaseItemName = "Marketing Rectangle Rug", Recolourable = false, MeshNames = new[] { "A_Room_General_Rug_Rectangle_V1_LOD0", "A_Room_General_Rug_Rectangle_V1_LOD1" }, ReplacementFbxAssetPath = "Assets/TPMSimpleModMaker/RugMeshes/A_Room_General_Rug_Rectangle_V1.fbx" },
            new RugDefinition { TemplateName = "Large Marketing Rectangle Rug", BaseArchetypeId = -1039203871L, BaseItemName = "Large Marketing Rectangle Rug", Recolourable = false, MeshNames = new[] { "A_Room_General_Rug_Rectangle_V1_LOD0", "A_Room_General_Rug_Rectangle_V1_LOD1" }, ReplacementFbxAssetPath = "Assets/TPMSimpleModMaker/RugMeshes/A_Room_General_Rug_RectangleLarge_V1.fbx" },
            new RugDefinition { TemplateName = "Marketing Square Rug", BaseArchetypeId = -348367332L, BaseItemName = "Marketing Square Rug", Recolourable = false, MeshNames = new[] { "A_Room_General_Rug_Square_V1_LOD0", "A_Room_General_Rug_Square_V1_LOD1" }, ReplacementFbxAssetPath = "Assets/TPMSimpleModMaker/RugMeshes/A_Room_General_Rug_Square_V1.fbx" },
            new RugDefinition { TemplateName = "Large Marketing Square Rug", BaseArchetypeId = -1780470970L, BaseItemName = "Large Marketing Square Rug", Recolourable = false, MeshNames = new[] { "A_Room_General_Rug_SquareLarge_V1_LOD0", "A_Room_General_Rug_SquareLarge_V1_LOD1" }, ReplacementFbxAssetPath = "Assets/TPMSimpleModMaker/RugMeshes/A_Room_General_Rug_SquareLarge_V1.fbx" },
            new RugDefinition { TemplateName = "Marketing Octagon Rug", BaseArchetypeId = -783418398L, BaseItemName = "Marketing Octagon Rug", Recolourable = false, MeshNames = new[] { "A_Room_General_Rug_Octagon_V1_LOD0", "A_Room_General_Rug_Octagon_V1_LOD1" }, ReplacementFbxAssetPath = "Assets/TPMSimpleModMaker/RugMeshes/A_Room_General_Rug_Octagon_V1.fbx" },
            new RugDefinition { TemplateName = "Large Marketing Octagon Rug", BaseArchetypeId = -178498331L, BaseItemName = "Large Marketing Octagon Rug", Recolourable = false, MeshNames = new[] { "A_Room_General_Rug_OctagonLarge_V1_LOD0", "A_Room_General_Rug_OctagonLarge_V1_LOD1" }, ReplacementFbxAssetPath = "Assets/TPMSimpleModMaker/RugMeshes/A_Room_General_Rug_OctagonLarge_V1.fbx" }
        };

        // Poster size pipeline. All three user-supplied gold standards are based on
        // Charity Heroes Poster, but replace both the four frame LODs and the Poster face.
        // Custom artwork is assigned through a generated Two Point/Lit material on Poster.fbx.
        private sealed class PosterDefinition
        {
            public string TemplateName;
            public long BaseArchetypeId;
            public string BaseItemName;
            public string FrameFbxAssetPath;
            public string PosterFbxAssetPath;
            public string MaterialTemplatePath;
        }

        private const long PosterBaseArchetypeId = -1917287632L;
        private const string PosterBaseItemName = "Charity Heroes Poster";
        private const string PosterTextureMeshName = "Poster";
        private static readonly string[] PosterFrameMeshNames =
        {
            "Frame_LOD0",
            "Frame_LOD1",
            "Frame_LOD2",
            "Frame_LOD3"
        };

        private static readonly PosterDefinition[] PosterDefinitions =
        {
            new PosterDefinition
            {
                TemplateName = TemplateSmallPoster,
                BaseArchetypeId = PosterBaseArchetypeId,
                BaseItemName = PosterBaseItemName,
                FrameFbxAssetPath = "Assets/TPMSimpleModMaker/PosterTemplates/Small/Frame.fbx",
                PosterFbxAssetPath = "Assets/TPMSimpleModMaker/PosterTemplates/Small/Poster.fbx",
                MaterialTemplatePath = "Assets/TPMSimpleModMaker/PosterTemplates/Small/Poster.mat"
            },
            new PosterDefinition
            {
                TemplateName = TemplateStandardPoster,
                BaseArchetypeId = PosterBaseArchetypeId,
                BaseItemName = PosterBaseItemName,
                FrameFbxAssetPath = "Assets/TPMSimpleModMaker/PosterTemplates/Standard/Frame.fbx",
                PosterFbxAssetPath = "Assets/TPMSimpleModMaker/PosterTemplates/Standard/Poster.fbx",
                MaterialTemplatePath = "Assets/TPMSimpleModMaker/PosterTemplates/Standard/Poster.mat"
            },
            new PosterDefinition
            {
                TemplateName = TemplateTallPoster,
                BaseArchetypeId = PosterBaseArchetypeId,
                BaseItemName = PosterBaseItemName,
                FrameFbxAssetPath = "Assets/TPMSimpleModMaker/PosterTemplates/Tall/Frame.fbx",
                PosterFbxAssetPath = "Assets/TPMSimpleModMaker/PosterTemplates/Tall/Poster.fbx",
                MaterialTemplatePath = "Assets/TPMSimpleModMaker/PosterTemplates/Tall/Poster.mat"
            }
        };

        [Serializable]
        public class BuildJob
        {
            public string template = TemplateStandardPoster;
            public string modName = "My Item";
            public string moddersName = "Modder";
            public string description = "Created with TPM Simple Mod Maker";
            public string imagePath = "";
            public string outputPath = "";
            public int itemCost = 500;
            public int kudoshCost = 0;

            // Optional. Leave empty to generate a new ID. Reuse this value when rebuilding
            // an existing mod so the game treats the new build as an update to the same item.
            public string itemModId = "";

            // Variant relationship supplied by Memento Maker. New projects default to
            // BaseGame; older saved projects migrate to Standalone.
            public string variantMode = "BaseGame";
            public string variantParentModId = "";
            public string variantParentBaseArchetypeId = "";
            public string itemCustomisationId = "";

            // Optional icon input. If provided, this image is imported as a Sprite, packed into
            // a generated SpriteAtlas and assigned to ItemIconReference.
            public string iconPath = "";

            // If true and iconPath is empty, reuse imagePath as the icon source.
            public bool useItemImageAsIcon = false;

            // During the proof-of-concept we keep generated Unity assets so they can be inspected.
            public bool keepGeneratedAssets = true;
        }

        [Serializable]
        public class BuildResultFile
        {
            public bool success;
            public string message;
            public string template;
            public string baseItemName;
            public string baseArchetypeId;
            public string modName;
            public string itemModId;
            public string itemCustomisationId;
            public string outputPath;
            public string generatedAssetFolder;
            public bool customIconCreated;
            public string iconSourcePath;
            public string utcCompleted;
        }

        [Serializable]
        public class BatchBuildManifest
        {
            public string[] jobPaths;
        }

        [Serializable]
        public class FamilyBuildManifest
        {
            public string[] jobPaths;
            public string familyKey = "";
            public string familyName = "Variant Family";
            public string outputPath = "";
            public string packageMode = "Family";
        }

        [Serializable]
        public class FamilyBuildResultFile
        {
            public bool success;
            public string message;
            public string familyKey;
            public string familyName;
            public string outputPath;
            public string packageMode;
            public BuildResultFile[] memberResults;
            public string utcCompleted;
        }

        private sealed class PreparedFamilyMember
        {
            public BuildJob Job;
            public string TemplateName;
            public string GeneratedAssetFolder;
            public string ConfigAssetPath;
            public string TextureAssetPath;
            public List<string> PackedAssetPaths;
            public BuildResultFile Result;
        }

        private sealed class EntrySnapshot
        {
            public string Guid;
            public string Address;
            public bool ReadOnly;
            public string[] Labels;
        }

        private sealed class GeneratedIconAssets
        {
            public string IconSpriteAssetPath;
            public string SpriteAtlasAssetPath;
            public Sprite IconSprite;
            public SpriteAtlas SpriteAtlas;
        }

        public static void BuildFamilyFromCommandLine()
        {
            string familyJobPath = null;
            int exitCode = 0;
            try
            {
                familyJobPath = GetCommandLineValue("-familyJobPath");
                if (string.IsNullOrEmpty(familyJobPath))
                    throw new ArgumentException("Missing required -familyJobPath argument.");
                familyJobPath = Path.GetFullPath(familyJobPath);
                FamilyBuildResultFile result = BuildFamily(familyJobPath);
                WriteFamilyResult(familyJobPath, result);
                Debug.Log("[TPM Simple Mod Maker] FAMILY SUCCESS: " + result.outputPath);
            }
            catch (Exception ex)
            {
                exitCode = 1;
                Debug.LogException(ex);
                if (!string.IsNullOrEmpty(familyJobPath))
                {
                    try
                    {
                        WriteFamilyResult(familyJobPath, new FamilyBuildResultFile
                        {
                            success = false,
                            message = ex.ToString(),
                            familyKey = "",
                            familyName = "",
                            outputPath = "",
                            memberResults = new BuildResultFile[0],
                            utcCompleted = DateTime.UtcNow.ToString("o")
                        });
                    }
                    catch (Exception writeEx) { Debug.LogException(writeEx); }
                }
            }

            if (Application.isBatchMode)
                EditorApplication.Exit(exitCode);
            else if (exitCode != 0)
                throw new Exception("Memento Maker combined family build failed. See the Console for details.");
        }

        public static void BuildFamilyToResultFile(string familyJobPath)
        {
            if (string.IsNullOrWhiteSpace(familyJobPath))
                throw new ArgumentException("Family build manifest path is empty.");
            familyJobPath = Path.GetFullPath(familyJobPath);
            try
            {
                FamilyBuildResultFile result = BuildFamily(familyJobPath);
                WriteFamilyResult(familyJobPath, result);
                Debug.Log("[TPM Simple Mod Maker] WORKER FAMILY SUCCESS: " + result.outputPath);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                WriteFamilyResult(familyJobPath, new FamilyBuildResultFile
                {
                    success = false,
                    message = ex.ToString(),
                    familyKey = "",
                    familyName = "",
                    outputPath = "",
                    memberResults = new BuildResultFile[0],
                    utcCompleted = DateTime.UtcNow.ToString("o")
                });
                Debug.LogError("[TPM Simple Mod Maker] WORKER FAMILY BUILD FAILED: " + ex.Message);
            }
        }

        public static FamilyBuildResultFile BuildFamily(string familyJobPath)
        {
            if (!File.Exists(familyJobPath))
                throw new FileNotFoundException("Family build manifest was not found.", familyJobPath);

            FamilyBuildManifest manifest = JsonUtility.FromJson<FamilyBuildManifest>(File.ReadAllText(familyJobPath));
            if (manifest == null || manifest.jobPaths == null || manifest.jobPaths.Length == 0)
                throw new InvalidDataException("The family build manifest contains no member jobs: " + familyJobPath);
            if (string.IsNullOrWhiteSpace(manifest.outputPath))
                throw new InvalidDataException("The family build manifest does not contain an output path.");

            string manifestDirectory = Path.GetDirectoryName(familyJobPath) ?? Directory.GetCurrentDirectory();
            string outputRoot = ResolveExternalPath(manifest.outputPath, manifestDirectory);
            Directory.CreateDirectory(outputRoot);
            string packageMode = string.Equals(manifest.packageMode, "DecorPack", StringComparison.OrdinalIgnoreCase) ? "DecorPack" : "Family";
            string familyName = string.IsNullOrWhiteSpace(manifest.familyName)
                ? (packageMode == "DecorPack" ? "Wallpaper Pack" : "Variant Family")
                : manifest.familyName.Trim();
            List<PreparedFamilyMember> prepared = new List<PreparedFamilyMember>();

            try
            {
                for (int i = 0; i < manifest.jobPaths.Length; i++)
                {
                    string memberJobPath = manifest.jobPaths[i];
                    if (string.IsNullOrWhiteSpace(memberJobPath))
                        throw new InvalidDataException("The family manifest contains an empty member job path at index " + i + ".");
                    memberJobPath = Path.GetFullPath(memberJobPath);
                    Debug.Log("[TPM Simple Mod Maker] [FAMILY " + (i + 1) + "/" + manifest.jobPaths.Length + "] PREPARE: " + memberJobPath);
                    prepared.Add(PrepareFamilyMember(memberJobPath));
                }

                string exportRoot = Path.Combine(Application.dataPath, "Exported~");
                if (Directory.Exists(exportRoot))
                    Directory.Delete(exportRoot, true);

                List<string> configAssetPaths = new List<string>();
                List<string> packedAssetPaths = new List<string>();
                for (int i = 0; i < prepared.Count; i++)
                {
                    configAssetPaths.Add(prepared[i].ConfigAssetPath);
                    if (prepared[i].PackedAssetPaths != null)
                        packedAssetPaths.AddRange(prepared[i].PackedAssetPaths);
                }

                Debug.Log("[TPM Simple Mod Maker] COMBINED ADDRESSABLES BUILD: " + configAssetPaths.Count + " ModConfig(s), " + packedAssetPaths.Count + " packed asset reference(s).");
                BuildAddressablesForFamily(configAssetPaths, packedAssetPaths);

                string pcBuildPath = Path.Combine(exportRoot, "PC");
                if (!Directory.Exists(pcBuildPath))
                    throw new DirectoryNotFoundException("The Two Point family build completed but no PC output folder was produced: " + pcBuildPath);

                string familyStamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + "_" + Guid.NewGuid().ToString("N").Substring(0, 6);
                string packageSuffix = packageMode == "DecorPack" ? "_DecorPack_" : "_Family_";
                string finalOutput = Path.Combine(outputRoot, MakeSafeName(familyName) + packageSuffix + familyStamp);
                CopyDirectory(pcBuildPath, finalOutput);

                BuildResultFile[] memberResults = new BuildResultFile[prepared.Count];
                for (int i = 0; i < prepared.Count; i++)
                {
                    prepared[i].Result.outputPath = finalOutput;
                    prepared[i].Result.utcCompleted = DateTime.UtcNow.ToString("o");
                    memberResults[i] = prepared[i].Result;
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                for (int i = 0; i < prepared.Count; i++)
                {
                    if (!prepared[i].Job.keepGeneratedAssets && !string.IsNullOrEmpty(prepared[i].GeneratedAssetFolder))
                        AssetDatabase.DeleteAsset(prepared[i].GeneratedAssetFolder);
                }
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                return new FamilyBuildResultFile
                {
                    success = true,
                    message = packageMode == "DecorPack"
                        ? "Wallpaper Decor Pack built successfully as one combined mod package."
                        : "Variant family built successfully as one combined mod package.",
                    familyKey = manifest.familyKey ?? "",
                    familyName = familyName,
                    outputPath = finalOutput,
                    packageMode = packageMode,
                    memberResults = memberResults,
                    utcCompleted = DateTime.UtcNow.ToString("o")
                };
            }
            catch
            {
                // A failed family build must not alter the user's installed packages. Clean up
                // temporary generated Unity assets where possible, then leave the previous
                // installed family/individual folders untouched for the desktop app to preserve.
                for (int i = 0; i < prepared.Count; i++)
                {
                    try
                    {
                        if (!prepared[i].Job.keepGeneratedAssets && !string.IsNullOrEmpty(prepared[i].GeneratedAssetFolder))
                            AssetDatabase.DeleteAsset(prepared[i].GeneratedAssetFolder);
                    }
                    catch { }
                }
                try
                {
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                }
                catch { }
                throw;
            }
        }

        private static PreparedFamilyMember PrepareFamilyMember(string jobPath)
        {
            if (!File.Exists(jobPath))
                throw new FileNotFoundException("Family member build job JSON was not found.", jobPath);

            BuildJob job = JsonUtility.FromJson<BuildJob>(File.ReadAllText(jobPath));
            if (job == null)
                throw new InvalidDataException("Could not parse a family member build job JSON.");
            ValidateJob(job, jobPath);

            string templateName = NormalizeTemplate(job.template);
            string jobDirectory = Path.GetDirectoryName(jobPath) ?? Directory.GetCurrentDirectory();
            string imagePath = ResolveExternalPath(job.imagePath, jobDirectory);
            if (!File.Exists(imagePath))
                throw new FileNotFoundException("The family member source image was not found.", imagePath);

            string safeName = MakeSafeName(job.modName);
            string buildStamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + "_" + Guid.NewGuid().ToString("N").Substring(0, 6);
            string generatedAssetFolder = "Assets/GeneratedMods/" + safeName + "_" + buildStamp;

            if (string.Equals(templateName, TemplateWallpaper, StringComparison.OrdinalIgnoreCase))
            {
                string wallpaperTextureExtension = Path.GetExtension(imagePath).ToLowerInvariant();
                string wallpaperTextureAssetPath = generatedAssetFolder + "/Textures/RoomVisualTexture" + wallpaperTextureExtension;
                string wallpaperConfigAssetPath = generatedAssetFolder + "/ModConfig/" + BuildItemConfigFileName(job.modName, job.moddersName);

                CreateUnityFolderForAsset(wallpaperTextureAssetPath);
                CreateUnityFolderForAsset(wallpaperConfigAssetPath);
                CopyFileIntoProject(imagePath, wallpaperTextureAssetPath);
                Texture2D wallpaperImportedTexture = ImportAndConfigureTexture(wallpaperTextureAssetPath, TemplateWallpaper);

                long roomVisualModId;
                CreateWallpaperConfig(wallpaperConfigAssetPath, wallpaperImportedTexture, job, out roomVisualModId);

                GeneratedIconAssets wallpaperGeneratedIconAssets = null;
                string wallpaperResolvedIconSourcePath = ResolveIconSourcePath(job, jobDirectory, imagePath);
                if (!string.IsNullOrEmpty(wallpaperResolvedIconSourcePath))
                    wallpaperGeneratedIconAssets = CreateAndAssignRoomVisualIcon(wallpaperConfigAssetPath, generatedAssetFolder, wallpaperResolvedIconSourcePath);

                List<string> wallpaperPackedPaths = new List<string>();
                if (wallpaperGeneratedIconAssets != null)
                    wallpaperPackedPaths.Add(wallpaperGeneratedIconAssets.SpriteAtlasAssetPath);

                return new PreparedFamilyMember
                {
                    Job = job,
                    TemplateName = templateName,
                    GeneratedAssetFolder = generatedAssetFolder,
                    ConfigAssetPath = wallpaperConfigAssetPath,
                    TextureAssetPath = wallpaperTextureAssetPath,
                    PackedAssetPaths = wallpaperPackedPaths,
                    Result = new BuildResultFile
                    {
                        success = true,
                        message = "Wallpaper pack member prepared successfully.",
                        template = TemplateWallpaper,
                        baseItemName = "Room Wall",
                        baseArchetypeId = "0",
                        modName = job.modName,
                        itemModId = roomVisualModId.ToString(),
                        itemCustomisationId = "",
                        outputPath = "",
                        generatedAssetFolder = job.keepGeneratedAssets ? generatedAssetFolder : "",
                        customIconCreated = wallpaperGeneratedIconAssets != null,
                        iconSourcePath = wallpaperGeneratedIconAssets != null ? wallpaperResolvedIconSourcePath : "",
                        utcCompleted = ""
                    }
                };
            }
            PosterDefinition activePoster = FindPosterDefinition(templateName);
            RugDefinition activeRug = IsRugTemplate(templateName) ? GetRugDefinition(templateName) : null;
            BannerDefinition activeBanner = FindBannerDefinition(templateName);
            DoubleBannerDefinition activeDoubleBanner = FindDoubleBannerDefinition(templateName);
            HangingSignDefinition activeHangingSign = FindHangingSignDefinition(templateName);
            WallSignDefinition activeWallSign = FindWallSignDefinition(templateName);
            bool isPoster = activePoster != null;
            bool isRug = activeRug != null;
            bool isBanner = activeBanner != null;
            bool isDoubleBanner = activeDoubleBanner != null;
            bool isHangingSign = activeHangingSign != null;
            bool isWallSign = activeWallSign != null;
            string textureExtension = (isPoster || isRug || isBanner || isDoubleBanner || isHangingSign || isWallSign)
                ? ".png" : Path.GetExtension(imagePath).ToLowerInvariant();
            string textureAssetPath = generatedAssetFolder + "/Textures/" + MakeSafeName(templateName) + "Texture" + textureExtension;
            string configAssetPath = generatedAssetFolder + "/ModConfig/" + BuildItemConfigFileName(job.modName, job.moddersName);

            CreateUnityFolderForAsset(textureAssetPath);
            CreateUnityFolderForAsset(configAssetPath);
            if (isRug)
            {
                ValidateRugReplacementDefinition(activeRug);
                CopyFileIntoProject(imagePath, textureAssetPath);
            }
            else
            {
                CopyFileIntoProject(imagePath, textureAssetPath);
            }
            Texture2D importedTexture = ImportAndConfigureTexture(textureAssetPath, templateName);

            ItemModConfig config;
            if (templateName == TemplateMural)
                config = CreateMuralConfig(configAssetPath, importedTexture, textureAssetPath, job);
            else if (activeRug != null)
                config = CreateRugConfig(configAssetPath, textureAssetPath, job, activeRug);
            else if (activeDoubleBanner != null)
                config = CreateDoubleBannerConfig(configAssetPath, textureAssetPath, job, activeDoubleBanner);
            else if (activeHangingSign != null)
                config = CreateHangingSignConfig(configAssetPath, textureAssetPath, job, activeHangingSign);
            else if (activeWallSign != null)
                config = CreateWallSignConfig(configAssetPath, textureAssetPath, job, activeWallSign);
            else if (activeBanner != null)
                config = CreateBannerConfig(configAssetPath, textureAssetPath, job, activeBanner);
            else if (activePoster != null)
                config = CreatePosterConfig(configAssetPath, textureAssetPath, job, activePoster);
            else
                throw new NotSupportedException("Unsupported template '" + templateName + "'.");

            GeneratedIconAssets generatedIconAssets = null;
            string resolvedIconSourcePath = ResolveIconSourcePath(job, jobDirectory, imagePath);
            if (!string.IsNullOrEmpty(resolvedIconSourcePath))
                generatedIconAssets = CreateAndAssignCustomIcon(config, generatedAssetFolder, resolvedIconSourcePath);

            List<string> packedPaths = new List<string>();
            packedPaths.Add(textureAssetPath);
            if (generatedIconAssets != null)
                packedPaths.Add(generatedIconAssets.SpriteAtlasAssetPath);
            if (isPoster)
            {
                packedPaths.Add(activePoster.FrameFbxAssetPath);
                packedPaths.Add(activePoster.PosterFbxAssetPath);
            }
            if (isRug)
                packedPaths.Add(activeRug.ReplacementFbxAssetPath);
            if (isBanner)
                packedPaths.Add(activeBanner.ReplacementFbxAssetPath);
            if (isDoubleBanner && activeDoubleBanner.ReplacementFbxAssetPaths != null)
            {
                foreach (string path in activeDoubleBanner.ReplacementFbxAssetPaths)
                    if (!string.IsNullOrEmpty(path)) packedPaths.Add(path);
            }
            if (isHangingSign)
            {
                packedPaths.Add(activeHangingSign.ReplacementFbxAssetPath);
                packedPaths.Add(activeHangingSign.FrameMaterialPath);
                packedPaths.Add(activeHangingSign.SuspendersMaterialPath);
            }
            if (isWallSign)
            {
                packedPaths.Add(activeWallSign.ReplacementFbxAssetPath);
                packedPaths.Add(activeWallSign.BracketMaterialPath);
                packedPaths.Add(activeWallSign.FrameMaterialPath);
            }

            GetBaseItemDetails(templateName, out string baseItemName, out long baseArchetypeId);
            return new PreparedFamilyMember
            {
                Job = job,
                TemplateName = templateName,
                GeneratedAssetFolder = generatedAssetFolder,
                ConfigAssetPath = configAssetPath,
                TextureAssetPath = textureAssetPath,
                PackedAssetPaths = packedPaths,
                Result = new BuildResultFile
                {
                    success = true,
                    message = templateName + " family member prepared successfully.",
                    template = templateName,
                    baseItemName = baseItemName,
                    baseArchetypeId = baseArchetypeId.ToString(),
                    modName = job.modName,
                    itemModId = config.ItemModID.ToString(),
                    itemCustomisationId = config.ItemCustomisationID.ToString(),
                    outputPath = "",
                    generatedAssetFolder = job.keepGeneratedAssets ? generatedAssetFolder : "",
                    customIconCreated = generatedIconAssets != null,
                    iconSourcePath = generatedIconAssets != null ? resolvedIconSourcePath : "",
                    utcCompleted = ""
                }
            };
        }

        private static void WriteFamilyResult(string familyJobPath, FamilyBuildResultFile result)
        {
            string resultPath = familyJobPath + ".result.json";
            File.WriteAllText(resultPath, JsonUtility.ToJson(result, true), new UTF8Encoding(false));
        }

        public static void BuildBatchFromCommandLine()
        {
            string batchJobPath = null;
            int exitCode = 0;

            try
            {
                batchJobPath = GetCommandLineValue("-batchJobPath");
                if (string.IsNullOrEmpty(batchJobPath))
                    throw new ArgumentException("Missing required -batchJobPath argument.");

                batchJobPath = Path.GetFullPath(batchJobPath);
                if (!File.Exists(batchJobPath))
                    throw new FileNotFoundException("Batch build manifest was not found.", batchJobPath);

                BatchBuildManifest manifest = JsonUtility.FromJson<BatchBuildManifest>(File.ReadAllText(batchJobPath));
                if (manifest == null || manifest.jobPaths == null || manifest.jobPaths.Length == 0)
                    throw new InvalidDataException("The batch build manifest contains no jobs: " + batchJobPath);

                int total = manifest.jobPaths.Length;
                Debug.Log("[TPM Simple Mod Maker] Single-session batch queue started with " + total + " job(s).");

                for (int i = 0; i < total; i++)
                {
                    string jobPath = manifest.jobPaths[i];
                    string marker = "[TPM Simple Mod Maker] [QUEUE " + (i + 1) + "/" + total + "] ";
                    Debug.Log(marker + "BEGIN: " + jobPath);

                    try
                    {
                        if (string.IsNullOrWhiteSpace(jobPath))
                            throw new InvalidDataException("The batch manifest contains an empty job path at index " + i + ".");

                        jobPath = Path.GetFullPath(jobPath);
                        BuildResultFile result = Build(jobPath);
                        WriteResult(jobPath, result);
                        Debug.Log(marker + "SUCCESS: " + result.modName + " -> " + result.outputPath);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogException(ex);

                        try
                        {
                            if (!string.IsNullOrWhiteSpace(jobPath))
                            {
                                jobPath = Path.GetFullPath(jobPath);
                                WriteResult(jobPath, new BuildResultFile
                                {
                                    success = false,
                                    message = ex.ToString(),
                                    template = "",
                                    baseItemName = "",
                                    baseArchetypeId = "",
                                    modName = "",
                                    itemModId = "",
                                    outputPath = "",
                                    generatedAssetFolder = "",
                                    customIconCreated = false,
                                    iconSourcePath = "",
                                    utcCompleted = DateTime.UtcNow.ToString("o")
                                });
                            }
                        }
                        catch (Exception writeEx)
                        {
                            Debug.LogException(writeEx);
                        }

                        Debug.Log(marker + "FAILED: " + ex.Message);
                    }

                    // Keep the one Unity process alive, but give Addressables/AssetDatabase a
                    // short moment to release build-cache file handles before the next mod.
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    System.Threading.Thread.Sleep(750);
                }

                Debug.Log("[TPM Simple Mod Maker] Single-session batch queue complete.");
            }
            catch (Exception ex)
            {
                exitCode = 1;
                Debug.LogException(ex);
            }

            if (Application.isBatchMode)
                EditorApplication.Exit(exitCode);
            else if (exitCode != 0)
                throw new Exception("TPM Simple Mod Maker batch queue failed. See the Console for details.");
        }

        public static void BuildFromCommandLine()
        {
            string jobPath = null;
            int exitCode = 0;

            try
            {
                jobPath = GetCommandLineValue("-jobPath");
                if (string.IsNullOrEmpty(jobPath))
                    throw new ArgumentException("Missing required -jobPath argument.");

                jobPath = Path.GetFullPath(jobPath);
                BuildResultFile result = Build(jobPath);
                WriteResult(jobPath, result);
                Debug.Log("[TPM Simple Mod Maker] SUCCESS: " + result.outputPath);
            }
            catch (Exception ex)
            {
                exitCode = 1;
                Debug.LogException(ex);

                if (!string.IsNullOrEmpty(jobPath))
                {
                    try
                    {
                        WriteResult(jobPath, new BuildResultFile
                        {
                            success = false,
                            message = ex.ToString(),
                            template = "",
                            baseItemName = "",
                            baseArchetypeId = "",
                            modName = "",
                            itemModId = "",
                            outputPath = "",
                            generatedAssetFolder = "",
                            customIconCreated = false,
                            iconSourcePath = "",
                            utcCompleted = DateTime.UtcNow.ToString("o")
                        });
                    }
                    catch (Exception writeEx)
                    {
                        Debug.LogException(writeEx);
                    }
                }
            }

            if (Application.isBatchMode)
            {
                EditorApplication.Exit(exitCode);
            }
            else if (exitCode != 0)
            {
                throw new Exception("TPM Simple Mod Maker build failed. See the Console for details.");
            }
        }

        /// <summary>
        /// Executes one build inside the persistent Step 9F Unity worker and always writes
        /// the same result JSON used by the legacy command-line path. This method never
        /// exits Unity; failures are captured in the result file so Memento Maker can
        /// continue using the same editor session for later jobs.
        /// </summary>
        public static void BuildToResultFile(string jobPath)
        {
            if (string.IsNullOrWhiteSpace(jobPath))
                throw new ArgumentException("Build job path is empty.");

            jobPath = Path.GetFullPath(jobPath);
            try
            {
                BuildResultFile result = Build(jobPath);
                WriteResult(jobPath, result);
                Debug.Log("[TPM Simple Mod Maker] WORKER SUCCESS: " + result.outputPath);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                WriteResult(jobPath, new BuildResultFile
                {
                    success = false,
                    message = ex.ToString(),
                    template = "",
                    baseItemName = "",
                    baseArchetypeId = "",
                    modName = "",
                    itemModId = "",
                    outputPath = "",
                    generatedAssetFolder = "",
                    customIconCreated = false,
                    iconSourcePath = "",
                    utcCompleted = DateTime.UtcNow.ToString("o")
                });
                Debug.LogError("[TPM Simple Mod Maker] WORKER BUILD FAILED: " + ex.Message);
            }
        }

        public static BuildResultFile Build(string jobPath)
        {
            if (!File.Exists(jobPath))
                throw new FileNotFoundException("Build job JSON was not found.", jobPath);

            string jobJson = File.ReadAllText(jobPath);
            BuildJob job = JsonUtility.FromJson<BuildJob>(jobJson);
            if (job == null)
                throw new InvalidDataException("Could not parse the build job JSON.");

            ValidateJob(job, jobPath);
            string templateName = NormalizeTemplate(job.template);

            string jobDirectory = Path.GetDirectoryName(jobPath) ?? Directory.GetCurrentDirectory();
            string imagePath = ResolveExternalPath(job.imagePath, jobDirectory);
            string outputRoot = ResolveExternalPath(job.outputPath, jobDirectory);

            if (!File.Exists(imagePath))
                throw new FileNotFoundException("The source image was not found.", imagePath);

            Directory.CreateDirectory(outputRoot);

            if (string.Equals(templateName, TemplateWallpaper, StringComparison.OrdinalIgnoreCase))
                return BuildWallpaper(job, jobDirectory, imagePath, outputRoot);

            string safeName = MakeSafeName(job.modName);
            string buildStamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + "_" + Guid.NewGuid().ToString("N").Substring(0, 6);
            string generatedAssetFolder = "Assets/GeneratedMods/" + safeName + "_" + buildStamp;
            PosterDefinition activePoster = FindPosterDefinition(templateName);
            RugDefinition activeRug = IsRugTemplate(templateName) ? GetRugDefinition(templateName) : null;
            BannerDefinition activeBanner = FindBannerDefinition(templateName);
            DoubleBannerDefinition activeDoubleBanner = FindDoubleBannerDefinition(templateName);
            HangingSignDefinition activeHangingSign = FindHangingSignDefinition(templateName);
            WallSignDefinition activeWallSign = FindWallSignDefinition(templateName);
            bool isPoster = activePoster != null;
            bool isRug = activeRug != null;
            bool isBanner = activeBanner != null;
            bool isDoubleBanner = activeDoubleBanner != null;
            bool isHangingSign = activeHangingSign != null;
            bool isWallSign = activeWallSign != null;
            string textureExtension = (isPoster || isRug || isBanner || isDoubleBanner || isHangingSign || isWallSign) ? ".png" : Path.GetExtension(imagePath).ToLowerInvariant();
            string textureAssetPath = generatedAssetFolder + "/Textures/" + MakeSafeName(templateName) + "Texture" + textureExtension;
            string configAssetPath = generatedAssetFolder + "/ModConfig/" + BuildItemConfigFileName(job.modName, job.moddersName);
            Debug.Log("[TPM Simple Mod Maker] Generated ItemModConfig asset filename: " + Path.GetFileName(configAssetPath));

            CreateUnityFolderForAsset(textureAssetPath);
            CreateUnityFolderForAsset(configAssetPath);

            if (isRug)
            {
                ValidateRugReplacementDefinition(activeRug);
                // The desktop app has already prepared the final rug artwork as a complete
                // 1024x1024 texture. Copy that image into Unity byte-for-byte. Do NOT bake
                // it back through the replacement mesh: that rasterisation only writes pixels
                // covered by mesh UV triangles and therefore recreates the rug-shaped/black
                // background problem on side UV islands.
                CopyFileIntoProject(imagePath, textureAssetPath);
                Debug.Log(string.Format(
                    "[TPM Simple Mod Maker] Using complete prepared 1024x1024 rug texture directly: {0}. No mesh-shape bake applied.",
                    textureAssetPath));
            }
            else
            {
                CopyFileIntoProject(imagePath, textureAssetPath);
            }
            Texture2D importedTexture = ImportAndConfigureTexture(textureAssetPath, templateName);

            ItemModConfig config;
            if (templateName == TemplateMural)
            {
                config = CreateMuralConfig(configAssetPath, importedTexture, textureAssetPath, job);
            }
            else if (activeRug != null)
            {
                config = CreateRugConfig(configAssetPath, textureAssetPath, job, activeRug);
            }
            else if (activeDoubleBanner != null)
            {
                config = CreateDoubleBannerConfig(configAssetPath, textureAssetPath, job, activeDoubleBanner);
            }
            else if (activeHangingSign != null)
            {
                config = CreateHangingSignConfig(configAssetPath, textureAssetPath, job, activeHangingSign);
            }
            else if (activeWallSign != null)
            {
                config = CreateWallSignConfig(configAssetPath, textureAssetPath, job, activeWallSign);
            }
            else if (activeBanner != null)
            {
                config = CreateBannerConfig(configAssetPath, textureAssetPath, job, activeBanner);
            }
            else if (activePoster != null)
            {
                config = CreatePosterConfig(configAssetPath, textureAssetPath, job, activePoster);
            }
            else
            {
                throw new NotSupportedException("Unsupported template '" + templateName + "'.");
            }

            GeneratedIconAssets generatedIconAssets = null;
            string resolvedIconSourcePath = ResolveIconSourcePath(job, jobDirectory, imagePath);
            if (!string.IsNullOrEmpty(resolvedIconSourcePath))
            {
                generatedIconAssets = CreateAndAssignCustomIcon(config, generatedAssetFolder, resolvedIconSourcePath);
            }

            long itemModId = config.ItemModID;
            long itemCustomisationId = config.ItemCustomisationID;
            string exportRoot = Path.Combine(Application.dataPath, "Exported~");
            if (Directory.Exists(exportRoot))
                Directory.Delete(exportRoot, true);

            List<string> additionalAddressablePaths = new List<string>();
            if (generatedIconAssets != null)
            {
                additionalAddressablePaths.Add(generatedIconAssets.SpriteAtlasAssetPath);
            }
            if (isPoster)
            {
                additionalAddressablePaths.Add(activePoster.FrameFbxAssetPath);
                additionalAddressablePaths.Add(activePoster.PosterFbxAssetPath);
            }
            if (isRug)
            {
                additionalAddressablePaths.Add(activeRug.ReplacementFbxAssetPath);
            }
            if (isBanner)
            {
                additionalAddressablePaths.Add(activeBanner.ReplacementFbxAssetPath);
            }
            if (isDoubleBanner && activeDoubleBanner.ReplacementFbxAssetPaths != null)
            {
                foreach (string path in activeDoubleBanner.ReplacementFbxAssetPaths)
                    if (!string.IsNullOrEmpty(path)) additionalAddressablePaths.Add(path);
            }
            if (isHangingSign)
            {
                additionalAddressablePaths.Add(activeHangingSign.ReplacementFbxAssetPath);
                additionalAddressablePaths.Add(activeHangingSign.FrameMaterialPath);
                additionalAddressablePaths.Add(activeHangingSign.SuspendersMaterialPath);
            }
            if (isWallSign)
            {
                additionalAddressablePaths.Add(activeWallSign.ReplacementFbxAssetPath);
                additionalAddressablePaths.Add(activeWallSign.BracketMaterialPath);
                additionalAddressablePaths.Add(activeWallSign.FrameMaterialPath);
            }

            BuildAddressablesForOnlyThisMod(configAssetPath, textureAssetPath, additionalAddressablePaths.ToArray());

            string pcBuildPath = Path.Combine(exportRoot, "PC");
            if (!Directory.Exists(pcBuildPath))
                throw new DirectoryNotFoundException("The Two Point build completed but no PC output folder was produced: " + pcBuildPath);

            string finalOutput = Path.Combine(outputRoot, safeName + "_" + buildStamp);
            CopyDirectory(pcBuildPath, finalOutput);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (!job.keepGeneratedAssets)
            {
                AssetDatabase.DeleteAsset(generatedAssetFolder);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            GetBaseItemDetails(templateName, out string baseItemName, out long baseArchetypeId);
            return new BuildResultFile
            {
                success = true,
                message = templateName + " mod built successfully.",
                template = templateName,
                baseItemName = baseItemName,
                baseArchetypeId = baseArchetypeId.ToString(),
                modName = job.modName,
                itemModId = itemModId.ToString(),
                itemCustomisationId = itemCustomisationId.ToString(),
                outputPath = finalOutput,
                generatedAssetFolder = job.keepGeneratedAssets ? generatedAssetFolder : "",
                customIconCreated = generatedIconAssets != null,
                iconSourcePath = generatedIconAssets != null ? resolvedIconSourcePath : "",
                utcCompleted = DateTime.UtcNow.ToString("o")
            };
        }

        private static BuildResultFile BuildWallpaper(BuildJob job, string jobDirectory, string imagePath, string outputRoot)
        {
            string safeName = MakeSafeName(job.modName);
            string buildStamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + "_" + Guid.NewGuid().ToString("N").Substring(0, 6);
            string generatedAssetFolder = "Assets/GeneratedMods/" + safeName + "_" + buildStamp;
            string textureExtension = Path.GetExtension(imagePath).ToLowerInvariant();
            // Keep the generated RoomVisual structure close to the known-working gold standard.
            string textureAssetPath = generatedAssetFolder + "/Textures/RoomVisualTexture" + textureExtension;
            string configAssetPath = generatedAssetFolder + "/ModConfig/" + BuildItemConfigFileName(job.modName, job.moddersName);

            CreateUnityFolderForAsset(textureAssetPath);
            CreateUnityFolderForAsset(configAssetPath);
            CopyFileIntoProject(imagePath, textureAssetPath);
            Texture2D importedTexture = ImportAndConfigureTexture(textureAssetPath, TemplateWallpaper);

            long roomVisualModId;
            CreateWallpaperConfig(configAssetPath, importedTexture, job, out roomVisualModId);

            GeneratedIconAssets generatedIconAssets = null;
            string resolvedIconSourcePath = ResolveIconSourcePath(job, jobDirectory, imagePath);
            if (!string.IsNullOrEmpty(resolvedIconSourcePath))
                generatedIconAssets = CreateAndAssignRoomVisualIcon(configAssetPath, generatedAssetFolder, resolvedIconSourcePath);

            string exportRoot = Path.Combine(Application.dataPath, "Exported~");
            if (Directory.Exists(exportRoot))
                Directory.Delete(exportRoot, true);

            List<string> packedAssets = new List<string>();
            if (generatedIconAssets != null)
                packedAssets.Add(generatedIconAssets.SpriteAtlasAssetPath);

            // The official Wallpaper guide only requires the RoomVisualModConfig itself in
            // ModConfigs. Its Material/Texture are normal Unity dependencies. The icon atlas
            // remains Addressable, matching the SDK's sprite-atlas guidance.
            BuildAddressablesForFamily(new List<string> { configAssetPath }, packedAssets);

            string pcBuildPath = Path.Combine(exportRoot, "PC");
            if (!Directory.Exists(pcBuildPath))
                throw new DirectoryNotFoundException("The Two Point build completed but no PC output folder was produced: " + pcBuildPath);

            string finalOutput = Path.Combine(outputRoot, safeName + "_" + buildStamp);
            CopyDirectory(pcBuildPath, finalOutput);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            if (!job.keepGeneratedAssets)
            {
                AssetDatabase.DeleteAsset(generatedAssetFolder);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            return new BuildResultFile
            {
                success = true,
                message = "Wallpaper mod built successfully.",
                template = TemplateWallpaper,
                baseItemName = "Room Wall",
                baseArchetypeId = "0",
                modName = job.modName,
                itemModId = roomVisualModId.ToString(),
                itemCustomisationId = "",
                outputPath = finalOutput,
                generatedAssetFolder = job.keepGeneratedAssets ? generatedAssetFolder : "",
                customIconCreated = generatedIconAssets != null,
                iconSourcePath = generatedIconAssets != null ? resolvedIconSourcePath : "",
                utcCompleted = DateTime.UtcNow.ToString("o")
            };
        }

        private static RoomVisualModConfig CreateWallpaperConfig(string configAssetPath, Texture2D wallpaperTexture, BuildJob job, out long roomVisualModId)
        {
            if (AssetDatabase.LoadAssetAtPath<RoomVisualModConfig>(WallpaperConfigTemplatePath) == null)
                throw new FileNotFoundException("The Wallpaper gold-standard RoomVisualModConfig template is missing: " + WallpaperConfigTemplatePath);
            if (!AssetDatabase.CopyAsset(WallpaperConfigTemplatePath, configAssetPath))
                throw new IOException("Unity could not duplicate the Wallpaper gold-standard RoomVisualModConfig into: " + configAssetPath);

            AssetDatabase.ImportAsset(configAssetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            RoomVisualModConfig config = AssetDatabase.LoadAssetAtPath<RoomVisualModConfig>(configAssetPath);
            if (config == null)
                throw new InvalidOperationException("The generated Wallpaper RoomVisualModConfig could not be loaded.");

            roomVisualModId = GetOrCreateItemModId(job.itemModId);
            config.RoomVisualModID = roomVisualModId;

            // Match the known-working gold-standard config exactly for the core RoomVisual fields.
            // In particular Version=1 is important: CreateInstance() did not reliably reproduce
            // the serialized RoomVisual config state created by the SDK's Create menu.
            SetRoomVisualMember(config, new[] { "Version" }, 1, false);
            SetRoomVisualMember(config, new[] { "DisplayName", "RoomVisualName", "VisualName" }, job.modName ?? "Wallpaper", true);
            SetRoomVisualMember(config, new[] { "Cost", "ItemCost", "PurchaseCost" }, 0, true);
            SetRoomVisualMember(config, new[] { "KudoshCost", "Kudosh" }, 0, true);
            SetRoomVisualWallCustomisationType(config);

            string generatedRoot = configAssetPath.Substring(0, configAssetPath.LastIndexOf("/ModConfig/", StringComparison.OrdinalIgnoreCase));
            string materialAssetPath = generatedRoot + "/Materials/Wallpaper_Mat.mat";
            CreateUnityFolderForAsset(materialAssetPath);
            if (AssetDatabase.LoadAssetAtPath<Material>(WallpaperMaterialTemplatePath) == null)
                throw new FileNotFoundException("The Wallpaper gold-standard material template is missing: " + WallpaperMaterialTemplatePath);
            if (!AssetDatabase.CopyAsset(WallpaperMaterialTemplatePath, materialAssetPath))
                throw new IOException("Unity could not duplicate the Wallpaper gold-standard material into: " + materialAssetPath);

            AssetDatabase.ImportAsset(materialAssetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialAssetPath);
            if (material == null)
                throw new InvalidOperationException("The generated Wallpaper material could not be loaded.");
            ConfigureWallpaperMaterial(material, wallpaperTexture);
            config.Material = material;

            // Clear the template's cached TPSMaterialData so the official Two Point build script
            // regenerates it from the newly generated material/texture during export.
            config.MaterialData = new TPSMaterialData();

            EditorUtility.SetDirty(material);
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(configAssetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

            ValidateWallpaperConfigAgainstGoldStandard(config, material);
            Debug.Log(string.Format("[Memento Maker] Created Wallpaper from gold-standard RoomVisual scaffold '{0}' with RoomVisualModID {1}, Version 1, Room Wall customisation and Two Point/Wall material.", configAssetPath, roomVisualModId));
            return config;
        }

        private static void ValidateWallpaperConfigAgainstGoldStandard(RoomVisualModConfig config, Material material)
        {
            if (config == null)
                throw new InvalidOperationException("The Wallpaper RoomVisual config is null after generation.");
            if (config.RoomVisualModID == 0)
                throw new InvalidDataException("The generated Wallpaper RoomVisualModID is zero.");
            if (config.Material == null || material == null)
                throw new InvalidDataException("The generated Wallpaper RoomVisual config has no material.");
            if (material.shader == null || !string.Equals(material.shader.name, "Two Point/Wall", StringComparison.Ordinal))
                throw new InvalidDataException("The generated Wallpaper material is not using the Two Point/Wall shader.");

            SerializedObject serialized = new SerializedObject(config);
            SerializedProperty version = serialized.FindProperty("Version");
            if (version != null && version.intValue != 1)
                throw new InvalidDataException("The generated Wallpaper RoomVisual config version is " + version.intValue + "; the working gold standard requires Version 1.");
            SerializedProperty customisationType = serialized.FindProperty("CustomisationType");
            if (customisationType != null && customisationType.intValue != 1)
                throw new InvalidDataException("The generated Wallpaper CustomisationType is " + customisationType.intValue + "; the working gold standard requires Room Wall (1).");
        }

        private static void ConfigureWallpaperMaterial(Material material, Texture2D wallpaperTexture)
        {
            if (material == null)
                throw new ArgumentNullException("material");
            if (wallpaperTexture == null)
                throw new ArgumentNullException("wallpaperTexture");

            // The supplied working gold standard uses Two Point/Wall with the wallpaper
            // assigned to _MainTex, Metallic=0 and GPU instancing enabled. Preserve every
            // other authored material value from that scaffold rather than constructing a
            // fresh Material and guessing which shader properties need initialising.
            if (material.shader == null || !string.Equals(material.shader.name, "Two Point/Wall", StringComparison.Ordinal))
            {
                Shader shader = Shader.Find("Two Point/Wall");
                if (shader == null)
                    throw new InvalidOperationException("The Two Point/Wall shader could not be found. The configured SDK/private environment may be incomplete.");
                material.shader = shader;
            }

            bool albedoAssigned = false;
            if (material.HasProperty("_MainTex"))
            {
                material.SetTexture("_MainTex", wallpaperTexture);
                material.SetTextureScale("_MainTex", Vector2.one);
                material.SetTextureOffset("_MainTex", Vector2.zero);
                albedoAssigned = true;
            }

            // Fallback only for a future SDK shader rename. Current gold standard is _MainTex.
            if (!albedoAssigned)
            {
                string[] fallbackNames = { "_Albedo", "_AlbedoMap", "_BaseColorMap", "_Diffuse", "_DiffuseMap", "_BaseMap" };
                for (int i = 0; i < fallbackNames.Length; i++)
                {
                    string propertyName = fallbackNames[i];
                    if (!material.HasProperty(propertyName))
                        continue;
                    material.SetTexture(propertyName, wallpaperTexture);
                    material.SetTextureScale(propertyName, Vector2.one);
                    material.SetTextureOffset(propertyName, Vector2.zero);
                    albedoAssigned = true;
                    break;
                }
            }

            if (!albedoAssigned)
                throw new InvalidOperationException("The Two Point/Wall gold-standard material does not expose its expected Albedo texture property.");

            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
            if (material.HasProperty("_MetallicStrength")) material.SetFloat("_MetallicStrength", 0f);
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
        }

        private static void SetRoomVisualMember(RoomVisualModConfig config, string[] candidateNames, object value, bool required)
        {
            Type type = config.GetType();
            BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            for (int c = 0; c < candidateNames.Length; c++)
            {
                string candidate = candidateNames[c];
                FieldInfo field = type.GetField(candidate, flags);
                if (field == null)
                    field = type.GetFields(flags).FirstOrDefault(f => string.Equals(f.Name, candidate, StringComparison.OrdinalIgnoreCase));
                if (field != null)
                {
                    field.SetValue(config, ConvertMemberValue(value, field.FieldType));
                    return;
                }
                PropertyInfo property = type.GetProperty(candidate, flags);
                if (property == null)
                    property = type.GetProperties(flags).FirstOrDefault(q => string.Equals(q.Name, candidate, StringComparison.OrdinalIgnoreCase));
                if (property != null && property.CanWrite)
                {
                    property.SetValue(config, ConvertMemberValue(value, property.PropertyType), null);
                    return;
                }
            }
            if (required)
                throw new InvalidOperationException("Could not find the expected RoomVisualModConfig field: " + string.Join(" / ", candidateNames));
        }

        private static object ConvertMemberValue(object value, Type targetType)
        {
            if (targetType == typeof(string)) return Convert.ToString(value);
            if (targetType == typeof(int)) return Convert.ToInt32(value);
            if (targetType == typeof(long)) return Convert.ToInt64(value);
            if (targetType == typeof(float)) return Convert.ToSingle(value);
            if (targetType == typeof(double)) return Convert.ToDouble(value);
            return value;
        }

        private static void SetRoomVisualWallCustomisationType(RoomVisualModConfig config)
        {
            if (config == null)
                throw new ArgumentNullException("config");

            // The user's working gold standard serializes Room Wall as integer value 1. Set the
            // serialized value directly so a future enum display-name change cannot accidentally
            // choose a different option merely because it also contains the word "Wall".
            SerializedObject serialized = new SerializedObject(config);
            SerializedProperty property = serialized.FindProperty("CustomisationType");
            if (property != null)
            {
                property.intValue = 1;
                serialized.ApplyModifiedProperties();
                EditorUtility.SetDirty(config);
                return;
            }

            // Compatibility fallback for a future SDK that changes the serialized field name.
            Type type = config.GetType();
            BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            MemberInfo member = type.GetField("CustomisationType", flags) as MemberInfo ?? type.GetProperty("CustomisationType", flags);
            if (member == null)
                member = type.GetMembers(flags).FirstOrDefault(m => m.Name.IndexOf("CustomisationType", StringComparison.OrdinalIgnoreCase) >= 0 || m.Name.IndexOf("CustomizationType", StringComparison.OrdinalIgnoreCase) >= 0);
            Type enumType = member is FieldInfo ? ((FieldInfo)member).FieldType : (member is PropertyInfo ? ((PropertyInfo)member).PropertyType : null);
            if (enumType == null || !enumType.IsEnum)
                throw new InvalidOperationException("Could not locate the RoomVisualModConfig Customisation Type enum.");
            object enumValue = Enum.ToObject(enumType, 1);
            if (member is FieldInfo) ((FieldInfo)member).SetValue(config, enumValue);
            else ((PropertyInfo)member).SetValue(config, enumValue, null);
        }

        private static GeneratedIconAssets CreateAndAssignRoomVisualIcon(string configAssetPath, string generatedAssetFolder, string iconSourcePath)
        {
            string iconExtension = Path.GetExtension(iconSourcePath).ToLowerInvariant();
            string iconSpriteAssetPath = generatedAssetFolder + "/Icons/Wallpaper_Icon" + iconExtension;
            string spriteAtlasAssetPath = generatedAssetFolder + "/Icons/Wallpaper_Atlas.spriteatlas";
            CreateUnityFolderForAsset(iconSpriteAssetPath);
            CreateUnityFolderForAsset(spriteAtlasAssetPath);
            CopyFileIntoProject(iconSourcePath, iconSpriteAssetPath);
            Sprite iconSprite = ImportAndConfigureIconSprite(iconSpriteAssetPath);
            if (iconSprite == null)
                throw new InvalidOperationException("Unity could not import the generated Wallpaper icon sprite: " + iconSpriteAssetPath);
            string iconSpriteName = iconSprite.name;

            // Creating and packing a SpriteAtlas performs AssetDatabase refreshes. Those refreshes can
            // invalidate native UnityEngine.Object handles that were loaded before the atlas operation.
            // Therefore the RoomVisualModConfig MUST be loaded only after the atlas has been created and
            // all refreshes have completed. Loading it earlier was the cause of the repeated
            // SerializedObject 'Object at index 0 is null' failure seen in Wallpaper Preview 4.
            SpriteAtlas iconAtlas = CreateWallpaperSpriteAtlasV1(spriteAtlasAssetPath, iconSprite);

            AssetDatabase.ImportAsset(configAssetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            RoomVisualModConfig config = AssetDatabase.LoadAssetAtPath<RoomVisualModConfig>(configAssetPath);
            if (config == null)
                throw new InvalidOperationException("The generated Wallpaper RoomVisualModConfig could not be reloaded after creating its SpriteAtlas: " + configAssetPath);

            string atlasGuid = AssetDatabase.AssetPathToGUID(spriteAtlasAssetPath);
            if (string.IsNullOrEmpty(atlasGuid))
                throw new InvalidOperationException("Unity did not assign a GUID to the generated Wallpaper SpriteAtlas: " + spriteAtlasAssetPath);

            SerializedObject serializedConfig = new SerializedObject(config);
            serializedConfig.UpdateIfRequiredOrScript();
            SerializedProperty iconReferenceProperty = FindRoomVisualIconReferenceProperty(serializedConfig);
            if (iconReferenceProperty == null)
                throw new InvalidOperationException("Could not locate the icon reference field on RoomVisualModConfig.");

            SerializedProperty assetGuidProperty = iconReferenceProperty.FindPropertyRelative("m_AssetGUID");
            SerializedProperty subObjectNameProperty = iconReferenceProperty.FindPropertyRelative("m_SubObjectName");
            SerializedProperty subObjectTypeProperty = iconReferenceProperty.FindPropertyRelative("m_SubObjectType");
            SerializedProperty editorAssetChangedProperty = iconReferenceProperty.FindPropertyRelative("m_EditorAssetChanged");
            if (assetGuidProperty == null || subObjectNameProperty == null || subObjectTypeProperty == null)
                throw new InvalidOperationException("The RoomVisual icon reference is not an Addressables atlased-sprite reference.");

            assetGuidProperty.stringValue = atlasGuid;
            subObjectNameProperty.stringValue = iconSpriteName;
            subObjectTypeProperty.stringValue = typeof(Sprite).AssemblyQualifiedName;
            if (editorAssetChangedProperty != null) editorAssetChangedProperty.boolValue = false;
            serializedConfig.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();

            // Re-resolve the icon assets too, so the object references returned to the caller are live
            // after the SpriteAtlas refresh cycle. The build path only requires the asset paths, but this
            // keeps the GeneratedIconAssets object internally consistent for future use.
            iconSprite = AssetDatabase.LoadAssetAtPath<Sprite>(iconSpriteAssetPath);
            iconAtlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(spriteAtlasAssetPath);

            return new GeneratedIconAssets
            {
                IconSpriteAssetPath = iconSpriteAssetPath,
                SpriteAtlasAssetPath = spriteAtlasAssetPath,
                IconSprite = iconSprite,
                SpriteAtlas = iconAtlas
            };
        }

        private static SpriteAtlas CreateWallpaperSpriteAtlasV1(string atlasAssetPath, Sprite sprite)
        {
            string spritePath = AssetDatabase.GetAssetPath(sprite);
            string spriteGuid = AssetDatabase.AssetPathToGUID(spritePath);
            if (string.IsNullOrEmpty(spriteGuid))
                throw new InvalidOperationException("Unity could not determine the GUID for the generated Wallpaper icon sprite.");

            string absoluteAtlasPath = AssetPathToAbsolutePath(atlasAssetPath);
            Directory.CreateDirectory(Path.GetDirectoryName(absoluteAtlasPath));
            File.WriteAllText(absoluteAtlasPath, BuildWallpaperSpriteAtlasV1Yaml(spriteGuid, sprite.name));

            AssetDatabase.ImportAsset(atlasAssetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            SpriteAtlas atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(atlasAssetPath);
            if (atlas == null)
                throw new InvalidOperationException("Unity imported the Wallpaper SpriteAtlas but could not load it: " + atlasAssetPath);
            SpriteAtlasUtility.PackAtlases(new[] { atlas }, EditorUserBuildSettings.activeBuildTarget, false);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(atlasAssetPath);
            if (atlas == null || !atlas.CanBindTo(sprite))
                throw new InvalidOperationException("The generated Wallpaper SpriteAtlas cannot bind to its icon sprite.");
            if (atlas.GetSprite(sprite.name) == null)
                throw new InvalidOperationException("The generated Wallpaper SpriteAtlas does not contain the expected sprite: " + sprite.name);

            Debug.Log("[Memento Maker] Created Wallpaper SpriteAtlas V1 to match the working gold standard: " + atlasAssetPath);
            return atlas;
        }

        private static string BuildWallpaperSpriteAtlasV1Yaml(string spriteGuid, string spriteName)
        {
            // Mirrors the user's known-working Wallpaper_Atlas.spriteatlas. The packable is
            // the imported icon Texture2D (fileID 2800000); Unity populates the packed-sprite
            // cache when SpriteAtlasUtility.PackAtlases runs.
            return
                "%YAML 1.1\n" +
                "%TAG !u! tag:unity3d.com,2011:\n" +
                "--- !u!687078895 &4343727234628468602\n" +
                "SpriteAtlas:\n" +
                "  m_ObjectHideFlags: 0\n" +
                "  m_CorrespondingSourceObject: {fileID: 0}\n" +
                "  m_PrefabInstance: {fileID: 0}\n" +
                "  m_PrefabAsset: {fileID: 0}\n" +
                "  m_Name: Wallpaper_Atlas\n" +
                "  m_EditorData:\n" +
                "    serializedVersion: 2\n" +
                "    textureSettings:\n" +
                "      serializedVersion: 2\n" +
                "      anisoLevel: 1\n" +
                "      compressionQuality: 50\n" +
                "      maxTextureSize: 2048\n" +
                "      textureCompression: 0\n" +
                "      filterMode: 1\n" +
                "      generateMipMaps: 0\n" +
                "      readable: 0\n" +
                "      crunchedCompression: 0\n" +
                "      sRGB: 1\n" +
                "    platformSettings: []\n" +
                "    packingSettings:\n" +
                "      serializedVersion: 2\n" +
                "      padding: 4\n" +
                "      blockOffset: 1\n" +
                "      allowAlphaSplitting: 0\n" +
                "      enableRotation: 0\n" +
                "      enableTightPacking: 0\n" +
                "    secondaryTextureSettings: {}\n" +
                "    variantMultiplier: 1\n" +
                "    packables:\n" +
                "    - {fileID: 2800000, guid: " + spriteGuid + ", type: 3}\n" +
                "    bindAsDefault: 1\n" +
                "    isAtlasV2: 0\n" +
                "    cachedData: {fileID: 0}\n" +
                "  m_MasterAtlas: {fileID: 0}\n" +
                "  m_PackedSprites: []\n" +
                "  m_PackedSpriteNamesToIndex: []\n" +
                "  m_RenderDataMap: {}\n" +
                "  m_Tag: Wallpaper_Atlas\n" +
                "  m_IsVariant: 0\n";
        }

        private static SerializedProperty FindRoomVisualIconReferenceProperty(SerializedObject serializedConfig)
        {
            string[] names = { "ItemIconReference", "IconReference", "RoomVisualIconReference", "RoomIconReference" };
            for (int i = 0; i < names.Length; i++)
            {
                SerializedProperty direct = serializedConfig.FindProperty(names[i]);
                if (direct != null && direct.FindPropertyRelative("m_AssetGUID") != null)
                    return direct;
            }

            SerializedProperty iterator = serializedConfig.GetIterator();
            bool enterChildren = true;
            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (iterator.name.IndexOf("Icon", StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                SerializedProperty copy = iterator.Copy();
                if (copy.FindPropertyRelative("m_AssetGUID") != null)
                    return copy;
            }
            return null;
        }

        private static string NormaliseUnityAssetPath(string path)
        {
            string result = (path ?? "").Replace('\\', '/');
            while (result.Contains("/../"))
            {
                string[] parts = result.Split('/');
                List<string> clean = new List<string>();
                for (int i = 0; i < parts.Length; i++)
                {
                    if (parts[i] == ".." && clean.Count > 0) clean.RemoveAt(clean.Count - 1);
                    else if (parts[i] != "." && parts[i].Length > 0) clean.Add(parts[i]);
                }
                result = string.Join("/", clean.ToArray());
            }
            return result;
        }

        private static string ResolveIconSourcePath(BuildJob job, string jobDirectory, string resolvedImagePath)
        {
            if (!string.IsNullOrWhiteSpace(job.iconPath))
            {
                string iconPath = ResolveExternalPath(job.iconPath, jobDirectory);
                if (!File.Exists(iconPath))
                    throw new FileNotFoundException("The icon source image was not found.", iconPath);
                return iconPath;
            }

            if (job.useItemImageAsIcon)
                return resolvedImagePath;

            return null;
        }

        private static GeneratedIconAssets CreateAndAssignCustomIcon(ItemModConfig config, string generatedAssetFolder, string iconSourcePath)
        {
            string iconExtension = Path.GetExtension(iconSourcePath).ToLowerInvariant();
            string iconSpriteAssetPath = generatedAssetFolder + "/Icons/ItemIcon" + iconExtension;
            string spriteAtlasAssetPath = generatedAssetFolder + "/Icons/ItemIconAtlas.spriteatlasv2";

            CreateUnityFolderForAsset(iconSpriteAssetPath);
            CreateUnityFolderForAsset(spriteAtlasAssetPath);

            CopyFileIntoProject(iconSourcePath, iconSpriteAssetPath);
            Sprite iconSprite = ImportAndConfigureIconSprite(iconSpriteAssetPath);
            SpriteAtlas iconAtlas = CreateSpriteAtlas(spriteAtlasAssetPath, iconSprite);

            // AssetReferenceAtlasedSprite in the Addressables package shipped with this Unity 2020.3
            // project has an editor-only type mismatch when SetEditorSubObject() is used with an atlas:
            // the generic AssetReferenceT<Sprite> path tries to load the atlas itself as a Sprite.
            // Two Point's supplied configs ultimately serialize only three values for an atlased icon:
            // atlas GUID, sub-object sprite name, and Sprite assembly-qualified type. Write those fields
            // directly through SerializedObject so the generated config matches the official YAML.
            string atlasGuid = AssetDatabase.AssetPathToGUID(spriteAtlasAssetPath);
            if (string.IsNullOrEmpty(atlasGuid))
                throw new InvalidOperationException("Unity did not assign a GUID to the generated SpriteAtlas.");

            config.ItemIconReference = new AssetReferenceAtlasedSprite(atlasGuid);
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();

            SerializedObject serializedConfig = new SerializedObject(config);
            SerializedProperty iconReferenceProperty = serializedConfig.FindProperty("ItemIconReference");
            if (iconReferenceProperty == null)
                throw new InvalidOperationException("Could not find serialized ItemIconReference on the generated ItemModConfig.");

            SerializedProperty assetGuidProperty = iconReferenceProperty.FindPropertyRelative("m_AssetGUID");
            SerializedProperty subObjectNameProperty = iconReferenceProperty.FindPropertyRelative("m_SubObjectName");
            SerializedProperty subObjectTypeProperty = iconReferenceProperty.FindPropertyRelative("m_SubObjectType");
            SerializedProperty editorAssetChangedProperty = iconReferenceProperty.FindPropertyRelative("m_EditorAssetChanged");

            if (assetGuidProperty == null || subObjectNameProperty == null || subObjectTypeProperty == null)
                throw new InvalidOperationException("The Addressables AssetReference serialized fields could not be located.");

            assetGuidProperty.stringValue = atlasGuid;
            subObjectNameProperty.stringValue = iconSprite.name;
            subObjectTypeProperty.stringValue = typeof(Sprite).AssemblyQualifiedName;
            if (editorAssetChangedProperty != null)
                editorAssetChangedProperty.boolValue = false;

            serializedConfig.ApplyModifiedProperties();
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(config), ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

            // Validate the values after Unity has serialized/reimported the config.
            serializedConfig.Update();
            iconReferenceProperty = serializedConfig.FindProperty("ItemIconReference");
            assetGuidProperty = iconReferenceProperty.FindPropertyRelative("m_AssetGUID");
            subObjectNameProperty = iconReferenceProperty.FindPropertyRelative("m_SubObjectName");
            if (assetGuidProperty.stringValue != atlasGuid || subObjectNameProperty.stringValue != iconSprite.name)
                throw new InvalidOperationException("The generated ItemIconReference did not persist the expected atlas GUID and sprite name.");

            Debug.Log(string.Format("[TPM Simple Mod Maker] Assigned custom icon by serialized atlas reference. Atlas GUID: {0}, Sprite: {1}", atlasGuid, iconSprite.name));

            return new GeneratedIconAssets
            {
                IconSpriteAssetPath = iconSpriteAssetPath,
                SpriteAtlasAssetPath = spriteAtlasAssetPath,
                IconSprite = iconSprite,
                SpriteAtlas = iconAtlas
            };
        }

        private static Sprite ImportAndConfigureIconSprite(string assetPath)
        {
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
                throw new InvalidOperationException("Unity did not import the selected icon image as a texture: " + assetPath);

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.sRGBTexture = true;
            importer.mipmapEnabled = false;
            importer.streamingMipmaps = false;
            importer.isReadable = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.anisoLevel = 1;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.maxTextureSize = 1024;

            TextureImporterPlatformSettings defaultSettings = importer.GetDefaultPlatformTextureSettings();
            defaultSettings.maxTextureSize = 512;
            defaultSettings.textureCompression = TextureImporterCompression.Compressed;
            defaultSettings.compressionQuality = 50;
            importer.SetPlatformTextureSettings(defaultSettings);

            importer.SaveAndReimport();

            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (sprite == null)
                throw new InvalidOperationException("The imported icon sprite could not be loaded: " + assetPath);

            Debug.Log(string.Format("[TPM Simple Mod Maker] Imported icon sprite {0} ({1}x{2}).", assetPath, Mathf.RoundToInt(sprite.rect.width), Mathf.RoundToInt(sprite.rect.height)));
            return sprite;
        }

        private static SpriteAtlas CreateSpriteAtlas(string atlasAssetPath, Sprite sprite)
        {
            // Unity 2020.3 with this TPM SDK uses SpriteAtlas V2 (ProjectSettings m_SpritePackerMode=5).
            // AssetDatabase.CreateAsset(new SpriteAtlas(), "*.spriteatlasv2") creates a file but does
            // not create a loadable V2 atlas. The SDK itself warns that SpriteAtlas.Add does not work
            // with packing mode V2 in Unity 2020.3. Therefore generate the same V2 YAML structure used
            // by the supplied AlienPlant example, referencing our imported Sprite by GUID/local file ID.
            string spriteGuid;
            long spriteLocalId;
            if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(sprite, out spriteGuid, out spriteLocalId))
                throw new InvalidOperationException("Unity could not determine the GUID/local file ID for the generated icon sprite.");

            string absoluteAtlasPath = AssetPathToAbsolutePath(atlasAssetPath);
            Directory.CreateDirectory(Path.GetDirectoryName(absoluteAtlasPath));

            string atlasYaml = BuildSpriteAtlasV2Yaml(spriteGuid, spriteLocalId);
            File.WriteAllText(absoluteAtlasPath, atlasYaml);

            AssetDatabase.ImportAsset(atlasAssetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            SpriteAtlas atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(atlasAssetPath);
            if (atlas == null)
                throw new InvalidOperationException("Unity imported the generated SpriteAtlas V2 file but could not load it as a SpriteAtlas: " + atlasAssetPath);

            SpriteAtlasUtility.PackAtlases(new[] { atlas }, EditorUserBuildSettings.activeBuildTarget, false);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(atlasAssetPath);
            if (atlas == null)
                throw new InvalidOperationException("The generated SpriteAtlas was lost after packing: " + atlasAssetPath);

            if (!atlas.CanBindTo(sprite))
                throw new InvalidOperationException("The generated SpriteAtlas cannot bind to the imported icon sprite.");

            Sprite atlasSprite = atlas.GetSprite(sprite.name);
            if (atlasSprite == null)
                throw new InvalidOperationException("The generated SpriteAtlas does not contain the expected sprite: " + sprite.name);

            Debug.Log("[TPM Simple Mod Maker] Created SpriteAtlas V2: " + atlasAssetPath);
            return atlas;
        }

        private static string BuildSpriteAtlasV2Yaml(string spriteGuid, long spriteLocalId)
        {
            // Mirrors Assets/ExampleMods/AlienPlant/Icons/PlantIconSpriteAtlas 1.spriteatlasv2
            // supplied by Two Point Studios, but with the packable replaced by our generated Sprite.
            return
                "%YAML 1.1\n" +
                "%TAG !u! tag:unity3d.com,2011:\n" +
                "--- !u!612988286 &1\n" +
                "SpriteAtlasAsset:\n" +
                "  m_ObjectHideFlags: 0\n" +
                "  m_CorrespondingSourceObject: {fileID: 0}\n" +
                "  m_PrefabInstance: {fileID: 0}\n" +
                "  m_PrefabAsset: {fileID: 0}\n" +
                "  m_Name: \n" +
                "  m_MasterAtlas: {fileID: 0}\n" +
                "  m_ImporterData:\n" +
                "    serializedVersion: 2\n" +
                "    textureSettings:\n" +
                "      serializedVersion: 2\n" +
                "      anisoLevel: 1\n" +
                "      compressionQuality: 50\n" +
                "      maxTextureSize: 1024\n" +
                "      textureCompression: 0\n" +
                "      filterMode: 1\n" +
                "      generateMipMaps: 0\n" +
                "      readable: 0\n" +
                "      crunchedCompression: 0\n" +
                "      sRGB: 1\n" +
                "    platformSettings: []\n" +
                "    packingSettings:\n" +
                "      serializedVersion: 2\n" +
                "      padding: 4\n" +
                "      blockOffset: 1\n" +
                "      allowAlphaSplitting: 0\n" +
                "      enableRotation: 0\n" +
                "      enableTightPacking: 0\n" +
                "    secondaryTextureSettings: {}\n" +
                "    variantMultiplier: 1\n" +
                "    packables:\n" +
                "    - {fileID: " + spriteLocalId + ", guid: " + spriteGuid + ", type: 3}\n" +
                "    bindAsDefault: 1\n" +
                "    isAtlasV2: 1\n" +
                "    cachedData: {fileID: 0}\n" +
                "  m_IsVariant: 0\n";
        }

        private static void GetBaseItemDetails(string templateName, out string baseItemName, out long baseArchetypeId)
        {
            if (string.Equals(templateName, TemplateWallpaper, StringComparison.OrdinalIgnoreCase))
            {
                baseItemName = "Room Wall";
                baseArchetypeId = 0L;
                return;
            }
            if (string.Equals(templateName, TemplateMural, StringComparison.OrdinalIgnoreCase))
            {
                baseItemName = MuralBaseItemName;
                baseArchetypeId = MuralBaseArchetypeId;
                return;
            }

            PosterDefinition poster = FindPosterDefinition(templateName);
            if (poster != null)
            {
                baseItemName = poster.BaseItemName;
                baseArchetypeId = poster.BaseArchetypeId;
                return;
            }

            RugDefinition rug = FindRugDefinition(templateName);
            if (rug != null)
            {
                baseItemName = rug.BaseItemName;
                baseArchetypeId = rug.BaseArchetypeId;
                return;
            }

            DoubleBannerDefinition doubleBanner = FindDoubleBannerDefinition(templateName);
            if (doubleBanner != null)
            {
                baseItemName = doubleBanner.BaseItemName;
                baseArchetypeId = doubleBanner.BaseArchetypeId;
                return;
            }

            HangingSignDefinition hangingSign = FindHangingSignDefinition(templateName);
            if (hangingSign != null)
            {
                baseItemName = hangingSign.BaseItemName;
                baseArchetypeId = hangingSign.BaseArchetypeId;
                return;
            }

            WallSignDefinition wallSign = FindWallSignDefinition(templateName);
            if (wallSign != null)
            {
                baseItemName = wallSign.BaseItemName;
                baseArchetypeId = wallSign.BaseArchetypeId;
                return;
            }

            BannerDefinition banner = FindBannerDefinition(templateName);
            if (banner != null)
            {
                baseItemName = banner.BaseItemName;
                baseArchetypeId = banner.BaseArchetypeId;
                return;
            }

            throw new NotSupportedException("Unsupported template '" + templateName + "'.");
        }

        private static RugDefinition FindRugDefinition(string template)
        {
            if (string.IsNullOrWhiteSpace(template))
                return null;

            // Step 7 compatibility: the original single 'Rug' template was Staff Rectangle Rug.
            if (string.Equals(template, TemplateLegacyRug, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(template, "Rectangle Rug", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(template, "RectangleRug", StringComparison.OrdinalIgnoreCase))
            {
                template = "Staff Rectangle Rug";
            }

            foreach (RugDefinition rug in RugDefinitions)
            {
                if (string.Equals(template, rug.TemplateName, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(template, rug.BaseItemName, StringComparison.OrdinalIgnoreCase))
                    return rug;
            }
            return null;
        }

        private static PosterDefinition FindPosterDefinition(string template)
        {
            if (string.IsNullOrWhiteSpace(template))
                return null;

            foreach (PosterDefinition poster in PosterDefinitions)
            {
                if (string.Equals(template, poster.TemplateName, StringComparison.OrdinalIgnoreCase))
                    return poster;
            }

            if (string.Equals(template, TemplateLegacyPoster, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(template, PosterBaseItemName, StringComparison.OrdinalIgnoreCase))
            {
                return PosterDefinitions.FirstOrDefault(p => string.Equals(p.TemplateName, TemplateStandardPoster, StringComparison.OrdinalIgnoreCase));
            }

            return null;
        }

        private static bool IsPosterTemplate(string template)
        {
            return FindPosterDefinition(template) != null;
        }

        private static DoubleBannerDefinition FindDoubleBannerDefinition(string template)
        {
            if (string.IsNullOrWhiteSpace(template)) return null;
            foreach (DoubleBannerDefinition banner in DoubleBannerDefinitions)
            {
                if (string.Equals(template, banner.TemplateName, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(template, banner.BaseItemName, StringComparison.OrdinalIgnoreCase))
                    return banner;
            }
            return null;
        }

        private static bool IsDoubleBannerTemplate(string template)
        {
            return FindDoubleBannerDefinition(template) != null;
        }

        private static HangingSignDefinition FindHangingSignDefinition(string template)
        {
            if (string.IsNullOrWhiteSpace(template)) return null;
            foreach (HangingSignDefinition sign in HangingSignDefinitions)
            {
                if (string.Equals(template, sign.TemplateName, StringComparison.OrdinalIgnoreCase))
                    return sign;
            }
            return null;
        }

        private static HangingSignDefinition GetHangingSignDefinition(string template)
        {
            HangingSignDefinition sign = FindHangingSignDefinition(template);
            if (sign == null)
                throw new NotSupportedException("Unsupported hanging sign template '" + (template ?? "") + "'.");
            return sign;
        }

        private static bool IsHangingSignTemplate(string template)
        {
            return FindHangingSignDefinition(template) != null;
        }

        private static WallSignDefinition FindWallSignDefinition(string template)
        {
            if (string.IsNullOrWhiteSpace(template)) return null;
            foreach (WallSignDefinition sign in WallSignDefinitions)
            {
                if (string.Equals(template, sign.TemplateName, StringComparison.OrdinalIgnoreCase))
                    return sign;
            }
            return null;
        }

        private static WallSignDefinition GetWallSignDefinition(string template)
        {
            WallSignDefinition sign = FindWallSignDefinition(template);
            if (sign == null)
                throw new NotSupportedException("Unsupported wall sign template '" + (template ?? "") + "'.");
            return sign;
        }

        private static bool IsWallSignTemplate(string template)
        {
            return FindWallSignDefinition(template) != null;
        }

        private static BannerDefinition FindBannerDefinition(string template)
        {
            if (string.IsNullOrWhiteSpace(template))
                return null;

            foreach (BannerDefinition banner in BannerDefinitions)
            {
                if (string.Equals(template, banner.TemplateName, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(template, banner.BaseItemName, StringComparison.OrdinalIgnoreCase))
                    return banner;
            }

            // The General Banner is labelled Winter Banner by the SDK.
            if (string.Equals(template, "Winter Banner", StringComparison.OrdinalIgnoreCase))
                return BannerDefinitions.FirstOrDefault(b => string.Equals(b.TemplateName, "General Banner", StringComparison.OrdinalIgnoreCase));

            return null;
        }

        private static bool IsBannerTemplate(string template)
        {
            return FindBannerDefinition(template) != null;
        }

        private static RugDefinition GetRugDefinition(string template)
        {
            RugDefinition rug = FindRugDefinition(template);
            if (rug == null)
                throw new NotSupportedException("Unsupported rug template '" + (template ?? "") + "'.");
            return rug;
        }

        private static bool IsRugTemplate(string template)
        {
            return FindRugDefinition(template) != null;
        }

        private static string NormalizeTemplate(string template)
        {
            if (string.Equals(template, TemplateWallpaper, StringComparison.OrdinalIgnoreCase))
                return TemplateWallpaper;
            if (string.Equals(template, TemplateMural, StringComparison.OrdinalIgnoreCase))
                return TemplateMural;

            if (string.Equals(template, TemplateLegacyPoster, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(template, "Charity Heroes Poster", StringComparison.OrdinalIgnoreCase))
                return TemplateStandardPoster;

            PosterDefinition poster = FindPosterDefinition(template);
            if (poster != null)
                return poster.TemplateName;

            DoubleBannerDefinition doubleBanner = FindDoubleBannerDefinition(template);
            if (doubleBanner != null)
                return doubleBanner.TemplateName;

            HangingSignDefinition hangingSign = FindHangingSignDefinition(template);
            if (hangingSign != null)
                return hangingSign.TemplateName;

            WallSignDefinition wallSign = FindWallSignDefinition(template);
            if (wallSign != null)
                return wallSign.TemplateName;

            BannerDefinition banner = FindBannerDefinition(template);
            if (banner != null)
                return banner.TemplateName;

            RugDefinition rug = FindRugDefinition(template);
            if (rug != null)
                return rug.TemplateName;

            throw new NotSupportedException(
                "Unsupported template '" + (template ?? "") + "'. Memento Maker supports Wallpaper, Mural, Small/Standard/Tall Poster, Single Banner themes, Double Banner themes, Small/Large Hanging Sign, Small/Large Wall Sign and 16 Staff/Marketing rug templates.");
        }

        private static void ValidateJob(BuildJob job, string jobPath)
        {
            NormalizeTemplate(job.template);

            if (string.IsNullOrWhiteSpace(job.modName))
                throw new ArgumentException("modName cannot be empty.");

            if (string.IsNullOrWhiteSpace(job.moddersName) || string.IsNullOrEmpty(SanitiseAssetComponent(job.moddersName, 0)))
                throw new ArgumentException("moddersName must contain at least one letter or number.");

            if (string.IsNullOrWhiteSpace(job.imagePath))
                throw new ArgumentException("imagePath cannot be empty.");

            ValidateImageExtension(job.imagePath, "imagePath");

            if (!string.IsNullOrWhiteSpace(job.iconPath))
                ValidateImageExtension(job.iconPath, "iconPath");

            if (string.IsNullOrWhiteSpace(job.outputPath))
            {
                string jobDirectory = Path.GetDirectoryName(jobPath) ?? Directory.GetCurrentDirectory();
                job.outputPath = Path.Combine(jobDirectory, "Output");
            }

            if (job.itemCost < 0)
                throw new ArgumentOutOfRangeException("itemCost", "itemCost cannot be negative.");

            if (job.kudoshCost < 0)
                throw new ArgumentOutOfRangeException("kudoshCost", "kudoshCost cannot be negative.");
        }

        private static void ValidateImageExtension(string pathValue, string fieldName)
        {
            string extension = Path.GetExtension(pathValue).ToLowerInvariant();
            if (extension != ".png" && extension != ".jpg" && extension != ".jpeg")
                throw new NotSupportedException(fieldName + " supports PNG and JPG/JPEG images only.");
        }

        private static ItemModConfig CreateMuralConfig(string configAssetPath, Texture2D texture, string textureAssetPath, BuildJob job)
        {
            if (AssetDatabase.LoadAssetAtPath<ItemModConfig>(MuralTemplateConfigPath) == null)
                throw new FileNotFoundException("The official Two Point mural example config is missing: " + MuralTemplateConfigPath);

            if (!AssetDatabase.CopyAsset(MuralTemplateConfigPath, configAssetPath))
                throw new IOException("Unity could not copy the official mural example config to: " + configAssetPath);

            AssetDatabase.ImportAsset(configAssetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

            ItemModConfig config = AssetDatabase.LoadAssetAtPath<ItemModConfig>(configAssetPath);
            if (config == null)
                throw new InvalidOperationException("The generated ItemModConfig could not be loaded.");

            config.Version = ItemModConfigVersion.OnePastLatest - 1;
            config.BaseArchetypeID = MuralBaseArchetypeId;
            ApplyItemCustomisation(config, job, MuralBaseArchetypeId);
            config.ItemName = job.modName;
            config.ItemDescription = job.description ?? "";
            config.ItemModID = GetOrCreateItemModId(job.itemModId);
            config.ItemCost = job.itemCost;
            config.KudoshCost = job.kudoshCost;
            config.HideAllBaseItemMeshes = false;
            config.UseExistingCustomisationVariants = true;
            config.UseExistingRandomVariants = true;
            config.CurrentItemFilter = MuralBaseItemName;
            config.SelectedBySearchFilter = true;

            string textureGuid = AssetDatabase.AssetPathToGUID(textureAssetPath);
            if (string.IsNullOrEmpty(textureGuid))
                throw new InvalidOperationException("Unity did not assign a GUID to the imported texture.");

            if (config.Textures == null)
                config.Textures = new List<ItemModTextureDef>();

            int textureIndex = FindTextureEntry(config.Textures, MuralMeshName);
            if (textureIndex < 0)
            {
                config.Textures.Add(new ItemModTextureDef
                {
                    MeshName = MuralMeshName,
                    Texture = new AssetReferenceT<Texture>(textureGuid)
                });
            }
            else
            {
                ItemModTextureDef textureDef = config.Textures[textureIndex];
                textureDef.MeshName = MuralMeshName;
                textureDef.Texture = new AssetReferenceT<Texture>(textureGuid);
                config.Textures[textureIndex] = textureDef;
            }

            if (config.Meshes != null)
            {
                for (int i = 0; i < config.Meshes.Count; i++)
                {
                    ItemModMeshDef mesh = config.Meshes[i];
                    if (mesh.MeshName != MuralMeshName)
                        continue;

                    if (mesh.MaterialsData != null && mesh.MaterialsData.Count > 0)
                        mesh.MaterialsData[0].AlbedoTexture = texture;

                    config.Meshes[i] = mesh;
                    break;
                }
            }

            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(configAssetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            return config;
        }

        private static void ValidateRugReplacementDefinition(RugDefinition rug)
        {
            if (rug == null)
                throw new ArgumentNullException("rug");
            if (string.IsNullOrWhiteSpace(rug.ReplacementFbxAssetPath))
                throw new InvalidDataException("Rug template '" + rug.TemplateName + "' does not define a replacement FBX.");
            if (rug.MeshNames == null || rug.MeshNames.Length == 0)
                throw new InvalidDataException("Rug template '" + rug.TemplateName + "' does not define any LOD mesh names.");
        }

        private static AssetReferenceT<Mesh> CreateReplacementMeshReference(string fbxAssetPath, string expectedMeshName)
        {
            AssetDatabase.ImportAsset(fbxAssetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            string guid = AssetDatabase.AssetPathToGUID(fbxAssetPath);
            if (string.IsNullOrEmpty(guid))
                throw new InvalidOperationException("Unity did not assign a GUID to replacement FBX: " + fbxAssetPath);

            Mesh replacementMesh = FindReplacementMesh(fbxAssetPath, expectedMeshName);
            if (replacementMesh == null)
                throw new InvalidOperationException("Could not find replacement mesh '" + expectedMeshName + "' inside " + fbxAssetPath);

            AssetReferenceT<Mesh> meshReference = new AssetReferenceT<Mesh>(guid);
            if (!meshReference.SetEditorSubObject(replacementMesh))
                throw new InvalidOperationException("Unity could not assign replacement mesh sub-object '" + replacementMesh.name + "' from " + fbxAssetPath);
            return meshReference;
        }

        private static Mesh FindReplacementMesh(string fbxAssetPath, string expectedMeshName)
        {
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(fbxAssetPath);
            Mesh prefixMatch = null;
            Mesh normalizedNameMatch = null;

            // The supplied small-rug FBXs use an internal naming convention such as
            // A_Room_General_Rug_Circle_V1s_LOD0 while the game's target mesh name is
            // A_Room_General_Rug_Circle_V1_LOD0.  MeshName must continue to use the
            // game's original name, but the replacement MeshReference may point to the
            // corresponding V1s mesh inside the FBX.
            string smallRugAlternative = expectedMeshName.Replace("_V1_LOD", "_V1s_LOD");

            foreach (UnityEngine.Object asset in assets)
            {
                Mesh mesh = asset as Mesh;
                if (mesh == null)
                    continue;

                if (string.Equals(mesh.name, expectedMeshName, StringComparison.OrdinalIgnoreCase))
                    return mesh;

                if (string.Equals(mesh.name, smallRugAlternative, StringComparison.OrdinalIgnoreCase))
                    normalizedNameMatch = mesh;

                // Blender can occasionally leave a .001/.002 suffix on the geometry sub-object.
                if (mesh.name.StartsWith(expectedMeshName + ".", StringComparison.OrdinalIgnoreCase) ||
                    mesh.name.StartsWith(smallRugAlternative + ".", StringComparison.OrdinalIgnoreCase))
                {
                    prefixMatch = mesh;
                }
            }

            if (normalizedNameMatch != null)
                return normalizedNameMatch;

            return prefixMatch;
        }

        private static void BakeReplacementRugTexture(string designImagePath, RugDefinition rug, string outputTextureAssetPath)
        {
            ValidateRugReplacementDefinition(rug);

            AssetDatabase.ImportAsset(rug.ReplacementFbxAssetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            Mesh mesh = FindReplacementMesh(rug.ReplacementFbxAssetPath, rug.MeshNames[0]);
            if (mesh == null)
                throw new InvalidOperationException("Could not locate LOD0 replacement mesh '" + rug.MeshNames[0] + "' in " + rug.ReplacementFbxAssetPath);

            byte[] designBytes = File.ReadAllBytes(designImagePath);
            Texture2D design = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!design.LoadImage(designBytes, false))
            {
                UnityEngine.Object.DestroyImmediate(design);
                throw new InvalidDataException("Unity could not load the prepared rug design image: " + designImagePath);
            }
            design.wrapMode = TextureWrapMode.Clamp;
            design.filterMode = FilterMode.Bilinear;

            const int textureSize = 1024;
            Texture2D baked = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);
            Color32[] pixels = new Color32[textureSize * textureSize];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = new Color32(0, 0, 0, 255);

            Vector3[] vertices = mesh.vertices;
            Vector2[] uvs = mesh.uv;
            int[] triangles = mesh.triangles;
            if (vertices == null || vertices.Length == 0 || uvs == null || uvs.Length != vertices.Length || triangles == null || triangles.Length < 3)
            {
                UnityEngine.Object.DestroyImmediate(design);
                UnityEngine.Object.DestroyImmediate(baked);
                throw new InvalidDataException("The replacement rug mesh does not contain usable vertices, UVs and triangles: " + mesh.name);
            }

            float minX = float.MaxValue;
            float maxX = float.MinValue;
            float minY = float.MaxValue;
            float maxY = float.MinValue;
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 v = vertices[i];
                if (v.x < minX) minX = v.x;
                if (v.x > maxX) maxX = v.x;
                if (v.z < minY) minY = v.z;
                if (v.z > maxY) maxY = v.z;
            }
            float spanX = Math.Max(0.000001f, maxX - minX);
            float spanY = Math.Max(0.000001f, maxY - minY);

            for (int triangleIndex = 0; triangleIndex + 2 < triangles.Length; triangleIndex += 3)
            {
                int i0 = triangles[triangleIndex];
                int i1 = triangles[triangleIndex + 1];
                int i2 = triangles[triangleIndex + 2];
                RasteriseReplacementRugTriangle(
                    design,
                    pixels,
                    textureSize,
                    vertices[i0], vertices[i1], vertices[i2],
                    uvs[i0], uvs[i1], uvs[i2],
                    minX, minY, spanX, spanY);
            }

            baked.SetPixels32(pixels);
            baked.Apply(false, false);

            string absoluteOutput = AssetPathToAbsolutePath(outputTextureAssetPath);
            Directory.CreateDirectory(Path.GetDirectoryName(absoluteOutput));
            File.WriteAllBytes(absoluteOutput, baked.EncodeToPNG());

            UnityEngine.Object.DestroyImmediate(design);
            UnityEngine.Object.DestroyImmediate(baked);

            AssetDatabase.ImportAsset(outputTextureAssetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            Debug.Log(string.Format("[TPM Simple Mod Maker] Baked rug design through replacement mesh {0} into {1} using X/Z projection.", mesh.name, outputTextureAssetPath));
        }

        private static void RasteriseReplacementRugTriangle(
            Texture2D design,
            Color32[] outputPixels,
            int textureSize,
            Vector3 v0, Vector3 v1, Vector3 v2,
            Vector2 uv0, Vector2 uv1, Vector2 uv2,
            float minX, float minY, float spanX, float spanY)
        {
            Vector2 p0 = new Vector2(uv0.x * (textureSize - 1), uv0.y * (textureSize - 1));
            Vector2 p1 = new Vector2(uv1.x * (textureSize - 1), uv1.y * (textureSize - 1));
            Vector2 p2 = new Vector2(uv2.x * (textureSize - 1), uv2.y * (textureSize - 1));

            float denominator = ((p1.y - p2.y) * (p0.x - p2.x)) + ((p2.x - p1.x) * (p0.y - p2.y));
            if (Math.Abs(denominator) < 0.000001f)
                return;

            int minPx = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(p0.x, Mathf.Min(p1.x, p2.x))), 0, textureSize - 1);
            int maxPx = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(p0.x, Mathf.Max(p1.x, p2.x))), 0, textureSize - 1);
            int minPy = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(p0.y, Mathf.Min(p1.y, p2.y))), 0, textureSize - 1);
            int maxPy = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(p0.y, Mathf.Max(p1.y, p2.y))), 0, textureSize - 1);

            const float epsilon = -0.0005f;
            for (int y = minPy; y <= maxPy; y++)
            {
                for (int x = minPx; x <= maxPx; x++)
                {
                    float px = x + 0.5f;
                    float py = y + 0.5f;
                    float b0 = (((p1.y - p2.y) * (px - p2.x)) + ((p2.x - p1.x) * (py - p2.y))) / denominator;
                    float b1 = (((p2.y - p0.y) * (px - p2.x)) + ((p0.x - p2.x) * (py - p2.y))) / denominator;
                    float b2 = 1f - b0 - b1;
                    if (b0 < epsilon || b1 < epsilon || b2 < epsilon)
                        continue;

                    float objectX = (v0.x * b0) + (v1.x * b1) + (v2.x * b2);
                    float objectY = (v0.z * b0) + (v1.z * b1) + (v2.z * b2);
                    float designU = Mathf.Clamp01((objectX - minX) / spanX);
                    float designV = Mathf.Clamp01((objectY - minY) / spanY);
                    Color sampled = design.GetPixelBilinear(designU, designV);
                    outputPixels[(y * textureSize) + x] = sampled;
                }
            }
        }

        private static Material CreateGuideMatchedRugMaterial(string configAssetPath, string textureAssetPath, RugDefinition rug)
        {
            string templatePath = rug.Recolourable ? StaffRugMaterialTemplatePath : MarketingRugMaterialTemplatePath;
            Material templateMaterial = AssetDatabase.LoadAssetAtPath<Material>(templatePath);
            if (templateMaterial == null)
                throw new FileNotFoundException("The TPM Simple Mod Maker rug material template is missing: " + templatePath);

            Texture2D rugTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(textureAssetPath);
            if (rugTexture == null)
                throw new InvalidOperationException("The generated rug texture could not be loaded: " + textureAssetPath);

            string generatedRoot = configAssetPath.Substring(
                0,
                configAssetPath.LastIndexOf("/ModConfig/", StringComparison.OrdinalIgnoreCase));

            string materialAssetPath = generatedRoot + "/Materials/RugMaterial.mat";
            CreateUnityFolderForAsset(materialAssetPath);

            if (AssetDatabase.LoadAssetAtPath<Material>(materialAssetPath) != null)
                AssetDatabase.DeleteAsset(materialAssetPath);

            if (!AssetDatabase.CopyAsset(templatePath, materialAssetPath))
                throw new IOException("Unity could not create the generated rug material at: " + materialAssetPath);

            AssetDatabase.ImportAsset(materialAssetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialAssetPath);
            if (material == null)
                throw new InvalidOperationException("The generated rug material could not be loaded: " + materialAssetPath);

            // The manually working reference materials use _MainTex with the Two Point/Lit shader.
            material.SetTexture("_MainTex", rugTexture);
            material.SetTextureScale("_MainTex", Vector2.one);
            material.SetTextureOffset("_MainTex", Vector2.zero);

            if (rug.Recolourable)
            {
                Texture2D tintMask = AssetDatabase.LoadAssetAtPath<Texture2D>(StaffRugTintMaskPath);
                if (tintMask == null)
                    throw new FileNotFoundException("The Staff rug tint mask is missing: " + StaffRugTintMaskPath);

                material.SetTexture("_TintMask1", tintMask);
                if (material.HasProperty("_EnableTintMasks"))
                    material.SetFloat("_EnableTintMasks", 1f);
            }
            else
            {
                if (material.HasProperty("_TintMask1"))
                    material.SetTexture("_TintMask1", null);
                if (material.HasProperty("_EnableTintMasks"))
                    material.SetFloat("_EnableTintMasks", 0f);
            }

            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(materialAssetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

            Debug.Log(string.Format(
                "[TPM Simple Mod Maker] Created guide-matched {0} rug material from {1} and assigned {2}.",
                rug.Recolourable ? "Staff / recolourable" : "Marketing / non-recolourable",
                templatePath,
                textureAssetPath));

            return material;
        }

        private static ItemModConfig CreateRugConfig(string configAssetPath, string textureAssetPath, BuildJob job, RugDefinition rug)
        {
            if (rug == null)
                throw new ArgumentNullException("rug");

            ValidateRugReplacementDefinition(rug);

            if (AssetDatabase.LoadAssetAtPath<ItemModConfig>(MuralTemplateConfigPath) == null)
                throw new FileNotFoundException("The official Two Point mural example config is missing: " + MuralTemplateConfigPath);

            if (!AssetDatabase.CopyAsset(MuralTemplateConfigPath, configAssetPath))
                throw new IOException("Unity could not create the rug ItemModConfig at: " + configAssetPath);

            AssetDatabase.ImportAsset(configAssetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

            ItemModConfig config = AssetDatabase.LoadAssetAtPath<ItemModConfig>(configAssetPath);
            if (config == null)
                throw new InvalidOperationException("The generated rug ItemModConfig could not be loaded.");

            InitialiseItemConfig(config, job, rug.BaseArchetypeId, rug.BaseItemName);

            // Unified rug architecture: every rug is a real Item Mod, never a Texture Only Mod.
            config.TextureOnlyMod = false;
            config.HideAllCustomisationVariantMeshes = false;
            config.HideAllRandomVariantMeshes = false;
            config.UseExistingCustomisationVariants = true;
            config.UseExistingRandomVariants = true;

            // Match the manually verified working setup: retain one blank Textures row per LOD,
            // while the actual generated artwork texture lives on the Two Point/Lit material.
            config.Textures = new List<ItemModTextureDef>(rug.MeshNames.Length);
            config.Meshes = new List<ItemModMeshDef>(rug.MeshNames.Length);

            Material rugMaterial = CreateGuideMatchedRugMaterial(configAssetPath, textureAssetPath, rug);

            foreach (string meshName in rug.MeshNames)
            {
                config.Textures.Add(new ItemModTextureDef
                {
                    MeshName = meshName
                });

                config.Meshes.Add(new ItemModMeshDef
                {
                    MeshName = meshName,
                    MeshReference = CreateReplacementMeshReference(rug.ReplacementFbxAssetPath, meshName),
                    MaterialList = new List<Material> { rugMaterial }
                });
            }

            Debug.Log(string.Format(
                "[TPM Simple Mod Maker] Unified rug pipeline: {0}; BaseArchetypeID: {1}; LODs: {2}; Family: {3}; TextureOnlyMod: false.",
                rug.BaseItemName,
                rug.BaseArchetypeId,
                rug.MeshNames.Length,
                rug.Recolourable ? "Staff / recolourable" : "Marketing / non-recolourable"));

            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(configAssetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            return config;
        }

        private static Material CreateGuideMatchedBannerMaterial(string configAssetPath, string textureAssetPath, BannerDefinition banner)
        {
            if (banner == null)
                throw new ArgumentNullException("banner");

            Material templateMaterial = AssetDatabase.LoadAssetAtPath<Material>(banner.MaterialTemplatePath);
            if (templateMaterial == null)
                throw new FileNotFoundException("The Memento Maker banner material template is missing: " + banner.MaterialTemplatePath);

            Texture2D bannerTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(textureAssetPath);
            if (bannerTexture == null)
                throw new InvalidOperationException("The generated banner texture could not be loaded: " + textureAssetPath);

            string generatedRoot = configAssetPath.Substring(
                0,
                configAssetPath.LastIndexOf("/ModConfig/", StringComparison.OrdinalIgnoreCase));

            string materialAssetPath = generatedRoot + "/Materials/BannerArtwork.mat";
            CreateUnityFolderForAsset(materialAssetPath);

            if (AssetDatabase.LoadAssetAtPath<Material>(materialAssetPath) != null)
                AssetDatabase.DeleteAsset(materialAssetPath);

            if (!AssetDatabase.CopyAsset(banner.MaterialTemplatePath, materialAssetPath))
                throw new IOException("Unity could not create the generated banner material at: " + materialAssetPath);

            AssetDatabase.ImportAsset(materialAssetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialAssetPath);
            if (material == null)
                throw new InvalidOperationException("The generated banner material could not be loaded: " + materialAssetPath);

            material.SetTexture("_MainTex", bannerTexture);
            material.SetTextureScale("_MainTex", Vector2.one);
            material.SetTextureOffset("_MainTex", Vector2.zero);
            if (material.HasProperty("_BaseMap"))
                material.SetTexture("_BaseMap", bannerTexture);

            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(materialAssetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            return material;
        }

        private static Material CreateDoubleBannerMaterial(string configAssetPath, string textureAssetPath, DoubleBannerDefinition banner)
        {
            if (banner == null) throw new ArgumentNullException("banner");
            Material templateMaterial = AssetDatabase.LoadAssetAtPath<Material>(banner.MaterialTemplatePath);
            if (templateMaterial == null)
                throw new FileNotFoundException("The Memento Maker Double Banner material template is missing: " + banner.MaterialTemplatePath);

            Texture2D bannerTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(textureAssetPath);
            if (bannerTexture == null)
                throw new InvalidOperationException("The generated Double Banner texture could not be loaded: " + textureAssetPath);

            string generatedRoot = configAssetPath.Substring(0, configAssetPath.LastIndexOf("/ModConfig/", StringComparison.OrdinalIgnoreCase));
            string materialAssetPath = generatedRoot + "/Materials/DoubleBannerArtwork.mat";
            CreateUnityFolderForAsset(materialAssetPath);
            if (AssetDatabase.LoadAssetAtPath<Material>(materialAssetPath) != null)
                AssetDatabase.DeleteAsset(materialAssetPath);
            if (!AssetDatabase.CopyAsset(banner.MaterialTemplatePath, materialAssetPath))
                throw new IOException("Unity could not create the generated Double Banner material at: " + materialAssetPath);

            AssetDatabase.ImportAsset(materialAssetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialAssetPath);
            if (material == null)
                throw new InvalidOperationException("The generated Double Banner material could not be loaded: " + materialAssetPath);

            material.SetTexture("_MainTex", bannerTexture);
            material.SetTextureScale("_MainTex", Vector2.one);
            material.SetTextureOffset("_MainTex", Vector2.zero);
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", bannerTexture);
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(materialAssetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            return material;
        }

        private static ItemModConfig CreateDoubleBannerConfig(string configAssetPath, string textureAssetPath, BuildJob job, DoubleBannerDefinition banner)
        {
            if (banner == null) throw new ArgumentNullException("banner");
            if (AssetDatabase.LoadAssetAtPath<ItemModConfig>(MuralTemplateConfigPath) == null)
                throw new FileNotFoundException("The official Two Point mural example config is missing: " + MuralTemplateConfigPath);
            if (!AssetDatabase.CopyAsset(MuralTemplateConfigPath, configAssetPath))
                throw new IOException("Unity could not create the Double Banner ItemModConfig at: " + configAssetPath);

            AssetDatabase.ImportAsset(configAssetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            ItemModConfig config = AssetDatabase.LoadAssetAtPath<ItemModConfig>(configAssetPath);
            if (config == null)
                throw new InvalidOperationException("The generated Double Banner ItemModConfig could not be loaded.");

            InitialiseItemConfig(config, job, banner.BaseArchetypeId, banner.BaseItemName);
            config.TextureOnlyMod = false;
            config.HideAllCustomisationVariantMeshes = false;
            config.HideAllRandomVariantMeshes = false;
            config.Textures = new List<ItemModTextureDef>();
            config.Meshes = new List<ItemModMeshDef>();

            Material artworkMaterial = CreateDoubleBannerMaterial(configAssetPath, textureAssetPath, banner);
            if (banner.ArtworkMeshNames == null || banner.ReplacementFbxAssetPaths == null ||
                banner.ArtworkMeshNames.Length != banner.ReplacementFbxAssetPaths.Length)
                throw new InvalidDataException("Double Banner replacement mesh definition is incomplete for " + banner.TemplateName + ".");

            for (int i = 0; i < banner.ArtworkMeshNames.Length; i++)
            {
                string meshName = banner.ArtworkMeshNames[i];
                string fbxPath = banner.ReplacementFbxAssetPaths[i];
                config.Textures.Add(new ItemModTextureDef { MeshName = meshName });
                config.Meshes.Add(new ItemModMeshDef
                {
                    MeshName = meshName,
                    MeshReference = CreateReplacementMeshReference(fbxPath, meshName),
                    MaterialList = new List<Material> { artworkMaterial }
                });
            }

            if (banner.HiddenArtworkMeshNames != null)
            {
                foreach (string hiddenMeshName in banner.HiddenArtworkMeshNames)
                {
                    config.Textures.Add(new ItemModTextureDef { MeshName = hiddenMeshName });
                    config.Meshes.Add(new ItemModMeshDef { MeshName = hiddenMeshName, HideMeshInGame = true });
                }
            }

            Debug.Log(string.Format(
                "[Memento Maker] Double Banner pipeline: Theme={0}; Base={1} ({2}); CustomMeshes={3}; GoldStandard={4}; one combined 1024x1024 atlas.",
                banner.TemplateName, banner.BaseItemName, banner.BaseArchetypeId,
                banner.ArtworkMeshNames.Length, banner.Fantasy ? "Fantasy" : "General"));

            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(configAssetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            return config;
        }


        private static ItemModConfig CreateHangingSignConfig(string configAssetPath, string textureAssetPath, BuildJob job, HangingSignDefinition hangingSign)
        {
            if (hangingSign == null) throw new ArgumentNullException("hangingSign");
            if (AssetDatabase.LoadAssetAtPath<ItemModConfig>(hangingSign.ConfigTemplatePath) == null)
                throw new FileNotFoundException("The Hanging Sign config template is missing: " + hangingSign.ConfigTemplatePath);
            if (!AssetDatabase.CopyAsset(hangingSign.ConfigTemplatePath, configAssetPath))
                throw new IOException("Unity could not create the Hanging Sign ItemModConfig at: " + configAssetPath);

            AssetDatabase.ImportAsset(configAssetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            ItemModConfig config = AssetDatabase.LoadAssetAtPath<ItemModConfig>(configAssetPath);
            if (config == null)
                throw new InvalidOperationException("The generated Hanging Sign ItemModConfig could not be loaded.");

            // IMPORTANT: Hanging Sign is scaffolded from the supplied gold-standard
            // ItemModConfig, then normalized into the requested runtime material order:
            //   0 = 00_Suspenders, 1 = Sign, 2 = 00_Frame
            // for both LOD0 and LOD1. Only the Sign slot uses the generated artwork
            // material; the frame and suspenders materials are preserved from the
            // supplied Small/Large examples.
            InitialiseItemConfig(config, job, hangingSign.BaseArchetypeId, hangingSign.BaseItemName);
            config.TextureOnlyMod = false;
            config.HideAllCustomisationVariantMeshes = false;
            config.HideAllRandomVariantMeshes = false;

            Texture2D generatedTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(textureAssetPath);
            if (generatedTexture == null)
                throw new InvalidOperationException("The generated Hanging Sign texture could not be loaded: " + textureAssetPath);

            Material signMaterial = CreateHangingSignMaterial(configAssetPath, textureAssetPath, hangingSign);
            if (signMaterial == null)
                throw new InvalidOperationException("The generated Hanging Sign Sign material could not be loaded.");

            if (config.Meshes == null || config.Meshes.Count != 2)
                throw new InvalidDataException("The Hanging Sign gold-standard config must contain exactly two replacement mesh entries (LOD0 and LOD1).");

            string[] expectedBaseMeshNames =
            {
                "A_Room_General_Rug_Rectangle_V1_LOD0",
                "A_Room_General_Rug_Rectangle_V1_LOD1"
            };

            for (int i = 0; i < config.Meshes.Count; i++)
            {
                ItemModMeshDef mesh = config.Meshes[i];
                if (!string.Equals(mesh.MeshName, expectedBaseMeshNames[i], StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException(
                        "Unexpected Hanging Sign mesh slot order in the gold-standard config. Expected '" +
                        expectedBaseMeshNames[i] + "' at index " + i + " but found '" + (mesh.MeshName ?? "") + "'.");

                if (mesh.MaterialList == null || mesh.MaterialList.Count != 3)
                    throw new InvalidDataException(
                        "Hanging Sign mesh '" + mesh.MeshName + "' must contain exactly three materials so they can be reordered to [00_Suspenders, Sign, 00_Frame].");

                Material frameMaterial = null;
                Material suspendersMaterial = null;
                Material originalSignMaterial = null;
                for (int materialIndex = 0; materialIndex < mesh.MaterialList.Count; materialIndex++)
                {
                    Material material = mesh.MaterialList[materialIndex];
                    string materialName = material == null ? "" : material.name;
                    if (string.Equals(materialName, "00_Frame", StringComparison.OrdinalIgnoreCase))
                        frameMaterial = material;
                    else if (string.Equals(materialName, "00_Suspenders", StringComparison.OrdinalIgnoreCase))
                        suspendersMaterial = material;
                    else if (string.Equals(materialName, "Sign", StringComparison.OrdinalIgnoreCase))
                        originalSignMaterial = material;
                }

                if (frameMaterial == null || suspendersMaterial == null || originalSignMaterial == null)
                {
                    string foundOrder = string.Join(", ", mesh.MaterialList.ConvertAll(delegate(Material m) { return m == null ? "<null>" : m.name; }).ToArray());
                    throw new InvalidDataException(
                        "Hanging Sign gold-standard materials on '" + mesh.MeshName + "' must contain 00_Suspenders, Sign and 00_Frame. Found [" + foundOrder + "].");
                }

                // Normalize the runtime material order requested by the user.
                mesh.MaterialList = new List<Material>
                {
                    suspendersMaterial,
                    signMaterial,
                    frameMaterial
                };

                // Do not manually rewrite MaterialsData here. The official SDK
                // TPCAddressablesBuildScript regenerates MaterialsData from MaterialList
                // in the same order immediately before the Addressables build. Leaving
                // this to the SDK avoids stale serialized material data and keeps the
                // runtime slot order identical to the normalized MaterialList above.

                config.Meshes[i] = mesh;
            }

            Debug.Log(string.Format(
                "[Memento Maker] Hanging Sign pipeline: Size={0}; Base={1} ({2}); runtime material order normalized to [00_Suspenders, Sign, 00_Frame]; generated artwork assigned to Sign.",
                hangingSign.TemplateName, hangingSign.BaseItemName, hangingSign.BaseArchetypeId));

            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(configAssetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            return config;
        }

        private static Material CreateHangingSignMaterial(string configAssetPath, string textureAssetPath, HangingSignDefinition hangingSign)
        {
            Material templateMaterial = AssetDatabase.LoadAssetAtPath<Material>(hangingSign.SignMaterialTemplatePath);
            if (templateMaterial == null)
                throw new FileNotFoundException("The Hanging Sign sign material template is missing: " + hangingSign.SignMaterialTemplatePath);

            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(textureAssetPath);
            if (texture == null)
                throw new InvalidOperationException("The generated Hanging Sign texture could not be loaded: " + textureAssetPath);

            string generatedRoot = configAssetPath.Substring(0, configAssetPath.LastIndexOf("/ModConfig/", StringComparison.OrdinalIgnoreCase));
            string materialAssetPath = generatedRoot + "/Materials/HangingSignSign.mat";
            CreateUnityFolderForAsset(materialAssetPath);
            if (!AssetDatabase.CopyAsset(hangingSign.SignMaterialTemplatePath, materialAssetPath))
                throw new IOException("Unity could not duplicate the Hanging Sign material template into: " + materialAssetPath);

            AssetDatabase.ImportAsset(materialAssetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            Material generatedMaterial = AssetDatabase.LoadAssetAtPath<Material>(materialAssetPath);
            if (generatedMaterial == null)
                throw new InvalidOperationException("The generated Hanging Sign material could not be loaded.");

            // Match the supplied gold-standard Sign.mat exactly. The Two Point/Lit
            // Sign material uses the same texture in _MainTex (albedo) and _EmissionTex.
            // _BaseMap is intentionally left as-authored in the template (null).
            generatedMaterial.name = "Sign";
            generatedMaterial.mainTexture = texture;
            if (generatedMaterial.HasProperty("_MainTex"))
            {
                generatedMaterial.SetTexture("_MainTex", texture);
                generatedMaterial.SetTextureScale("_MainTex", Vector2.one);
                generatedMaterial.SetTextureOffset("_MainTex", Vector2.zero);
            }
            if (generatedMaterial.HasProperty("_EmissionTex"))
            {
                generatedMaterial.SetTexture("_EmissionTex", texture);
                generatedMaterial.SetTextureScale("_EmissionTex", Vector2.one);
                generatedMaterial.SetTextureOffset("_EmissionTex", Vector2.zero);
            }
            EditorUtility.SetDirty(generatedMaterial);
            AssetDatabase.SaveAssets();
            return generatedMaterial;
        }

        private static ItemModConfig CreateWallSignConfig(string configAssetPath, string textureAssetPath, BuildJob job, WallSignDefinition wallSign)
        {
            if (wallSign == null) throw new ArgumentNullException("wallSign");
            if (AssetDatabase.LoadAssetAtPath<ItemModConfig>(wallSign.ConfigTemplatePath) == null)
                throw new FileNotFoundException("The Wall Sign config template is missing: " + wallSign.ConfigTemplatePath);
            if (!AssetDatabase.CopyAsset(wallSign.ConfigTemplatePath, configAssetPath))
                throw new IOException("Unity could not create the Wall Sign ItemModConfig at: " + configAssetPath);

            AssetDatabase.ImportAsset(configAssetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            ItemModConfig config = AssetDatabase.LoadAssetAtPath<ItemModConfig>(configAssetPath);
            if (config == null)
                throw new InvalidOperationException("The generated Wall Sign ItemModConfig could not be loaded.");

            InitialiseItemConfig(config, job, wallSign.BaseArchetypeId, wallSign.BaseItemName);
            config.TextureOnlyMod = false;
            config.HideAllCustomisationVariantMeshes = false;
            config.HideAllRandomVariantMeshes = false;

            Texture2D generatedTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(textureAssetPath);
            if (generatedTexture == null)
                throw new InvalidOperationException("The generated Wall Sign texture could not be loaded: " + textureAssetPath);

            Material signMaterial = CreateWallSignMaterial(configAssetPath, textureAssetPath, wallSign);
            if (signMaterial == null)
                throw new InvalidOperationException("The generated Wall Sign Sign material could not be loaded.");

            if (config.Meshes == null)
                throw new InvalidDataException("The Wall Sign gold-standard config contains no mesh definitions.");

            string[] expectedBaseMeshNames =
            {
                "DecorationGeneralWallClock_LOD0",
                "DecorationGeneralWallClock_LOD1"
            };
            int updatedMeshCount = 0;

            for (int meshIndex = 0; meshIndex < config.Meshes.Count; meshIndex++)
            {
                ItemModMeshDef mesh = config.Meshes[meshIndex];
                bool targetMesh = expectedBaseMeshNames.Any(n => string.Equals(n, mesh.MeshName, StringComparison.OrdinalIgnoreCase));
                if (!targetMesh)
                    continue; // Preserve the gold-standard hidden ArmLong/ArmShort entries unchanged.

                if (mesh.MaterialList == null || mesh.MaterialList.Count != 3)
                    throw new InvalidDataException("Wall Sign mesh '" + mesh.MeshName + "' must contain exactly three materials.");

                Material bracketMaterial = null;
                Material frameMaterial = null;
                Material originalSignMaterial = null;
                foreach (Material material in mesh.MaterialList)
                {
                    string materialName = material == null ? "" : material.name;
                    if (string.Equals(materialName, "Sign", StringComparison.OrdinalIgnoreCase))
                        originalSignMaterial = material;
                    else if (string.Equals(materialName, "Bracket", StringComparison.OrdinalIgnoreCase))
                        bracketMaterial = material;
                    else if (string.Equals(materialName, "Frame", StringComparison.OrdinalIgnoreCase))
                        frameMaterial = material;
                }

                if (originalSignMaterial == null || bracketMaterial == null || frameMaterial == null)
                {
                    string foundOrder = string.Join(", ", mesh.MaterialList.ConvertAll(delegate(Material m) { return m == null ? "<null>" : m.name; }).ToArray());
                    throw new InvalidDataException("Wall Sign materials on '" + mesh.MeshName + "' must contain Sign, Bracket and Frame. Found [" + foundOrder + "].");
                }

                bool smallWallSign = string.Equals(wallSign.TemplateName, "Small Wall Sign", StringComparison.OrdinalIgnoreCase);
                if (smallWallSign)
                {
                    // Small Wall Sign test order requested during beta validation:
                    //   0 = Bracket, 1 = Frame, 2 = Sign
                    // Only Sign is replaced; Bracket and Frame remain the supplied references.
                    mesh.MaterialList = new List<Material>
                    {
                        bracketMaterial,
                        frameMaterial,
                        signMaterial
                    };
                }
                else
                {
                    // Large Wall Sign remains on the existing order:
                    //   0 = Sign, 1 = Bracket, 2 = Frame
                    mesh.MaterialList = new List<Material>
                    {
                        signMaterial,
                        bracketMaterial,
                        frameMaterial
                    };
                }
                config.Meshes[meshIndex] = mesh;
                updatedMeshCount++;
            }

            if (updatedMeshCount != 2)
                throw new InvalidDataException("The Wall Sign gold-standard config must contain both DecorationGeneralWallClock_LOD0 and DecorationGeneralWallClock_LOD1 replacement entries.");

            string wallSignMaterialOrder = string.Equals(wallSign.TemplateName, "Small Wall Sign", StringComparison.OrdinalIgnoreCase)
                ? "[Bracket, Frame, Sign]"
                : "[Sign, Bracket, Frame]";
            Debug.Log(string.Format(
                "[Memento Maker] Wall Sign pipeline: Size={0}; Base={1} ({2}); material order {3}; generated Front/Back artwork assigned only to Sign.",
                wallSign.TemplateName, wallSign.BaseItemName, wallSign.BaseArchetypeId, wallSignMaterialOrder));

            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(configAssetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            return config;
        }

        private static Material CreateWallSignMaterial(string configAssetPath, string textureAssetPath, WallSignDefinition wallSign)
        {
            Material templateMaterial = AssetDatabase.LoadAssetAtPath<Material>(wallSign.SignMaterialTemplatePath);
            if (templateMaterial == null)
                throw new FileNotFoundException("The Wall Sign sign material template is missing: " + wallSign.SignMaterialTemplatePath);

            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(textureAssetPath);
            if (texture == null)
                throw new InvalidOperationException("The generated Wall Sign texture could not be loaded: " + textureAssetPath);

            string generatedRoot = configAssetPath.Substring(0, configAssetPath.LastIndexOf("/ModConfig/", StringComparison.OrdinalIgnoreCase));
            string materialAssetPath = generatedRoot + "/Materials/WallSignSign.mat";
            CreateUnityFolderForAsset(materialAssetPath);
            if (!AssetDatabase.CopyAsset(wallSign.SignMaterialTemplatePath, materialAssetPath))
                throw new IOException("Unity could not duplicate the Wall Sign material template into: " + materialAssetPath);

            AssetDatabase.ImportAsset(materialAssetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            Material generatedMaterial = AssetDatabase.LoadAssetAtPath<Material>(materialAssetPath);
            if (generatedMaterial == null)
                throw new InvalidOperationException("The generated Wall Sign material could not be loaded.");

            generatedMaterial.name = "Sign";
            generatedMaterial.mainTexture = texture;
            if (generatedMaterial.HasProperty("_MainTex"))
            {
                generatedMaterial.SetTexture("_MainTex", texture);
                generatedMaterial.SetTextureScale("_MainTex", Vector2.one);
                generatedMaterial.SetTextureOffset("_MainTex", Vector2.zero);
            }
            if (generatedMaterial.HasProperty("_EmissionTex"))
            {
                generatedMaterial.SetTexture("_EmissionTex", texture);
                generatedMaterial.SetTextureScale("_EmissionTex", Vector2.one);
                generatedMaterial.SetTextureOffset("_EmissionTex", Vector2.zero);
            }
            EditorUtility.SetDirty(generatedMaterial);
            AssetDatabase.SaveAssets();
            return generatedMaterial;
        }

        private static ItemModConfig CreateBannerConfig(string configAssetPath, string textureAssetPath, BuildJob job, BannerDefinition banner)
        {
            if (banner == null)
                throw new ArgumentNullException("banner");

            if (AssetDatabase.LoadAssetAtPath<ItemModConfig>(MuralTemplateConfigPath) == null)
                throw new FileNotFoundException("The official Two Point mural example config is missing: " + MuralTemplateConfigPath);

            if (!AssetDatabase.CopyAsset(MuralTemplateConfigPath, configAssetPath))
                throw new IOException("Unity could not create the banner ItemModConfig at: " + configAssetPath);

            AssetDatabase.ImportAsset(configAssetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            ItemModConfig config = AssetDatabase.LoadAssetAtPath<ItemModConfig>(configAssetPath);
            if (config == null)
                throw new InvalidOperationException("The generated banner ItemModConfig could not be loaded.");

            InitialiseItemConfig(config, job, banner.BaseArchetypeId, banner.BaseItemName);
            config.TextureOnlyMod = false;
            config.HideAllCustomisationVariantMeshes = false;
            config.HideAllRandomVariantMeshes = false;
            config.Textures = new List<ItemModTextureDef>();
            config.Meshes = new List<ItemModMeshDef>();

            Material artworkMaterial = CreateGuideMatchedBannerMaterial(configAssetPath, textureAssetPath, banner);

            config.Textures.Add(new ItemModTextureDef
            {
                MeshName = banner.ArtworkMeshName
            });
            config.Meshes.Add(new ItemModMeshDef
            {
                MeshName = banner.ArtworkMeshName,
                MeshReference = CreateReplacementMeshReference(banner.ReplacementFbxAssetPath, banner.ArtworkMeshName),
                MaterialList = new List<Material> { artworkMaterial }
            });

            if (banner.HiddenArtworkMeshNames != null)
            {
                foreach (string hiddenMeshName in banner.HiddenArtworkMeshNames)
                {
                    config.Textures.Add(new ItemModTextureDef { MeshName = hiddenMeshName });
                    config.Meshes.Add(new ItemModMeshDef
                    {
                        MeshName = hiddenMeshName,
                        HideMeshInGame = true
                    });
                }
            }

            Debug.Log(string.Format(
                "[Memento Maker] Single Banner pipeline: Theme={0}; SDK base={1} ({2}); ArtworkMesh={3}; GoldStandard={4}.",
                banner.TemplateName,
                banner.BaseItemName,
                banner.BaseArchetypeId,
                banner.ArtworkMeshName,
                banner.Fantasy ? "Fantasy" : (banner.DigiverseSpecialLayout ? "Digiverse special" : "General")));

            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(configAssetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            return config;
        }

        private static Material CreatePosterMaterial(string configAssetPath, string textureAssetPath, PosterDefinition poster)
        {
            if (poster == null)
                throw new ArgumentNullException("poster");

            Material templateMaterial = AssetDatabase.LoadAssetAtPath<Material>(poster.MaterialTemplatePath);
            if (templateMaterial == null)
                throw new FileNotFoundException("The Memento Maker poster material template is missing: " + poster.MaterialTemplatePath);

            Texture2D posterTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(textureAssetPath);
            if (posterTexture == null)
                throw new InvalidOperationException("The generated poster texture could not be loaded: " + textureAssetPath);

            string generatedRoot = configAssetPath.Substring(
                0,
                configAssetPath.LastIndexOf("/ModConfig/", StringComparison.OrdinalIgnoreCase));
            string materialAssetPath = generatedRoot + "/Materials/PosterArtwork.mat";
            CreateUnityFolderForAsset(materialAssetPath);

            if (AssetDatabase.LoadAssetAtPath<Material>(materialAssetPath) != null)
                AssetDatabase.DeleteAsset(materialAssetPath);

            if (!AssetDatabase.CopyAsset(poster.MaterialTemplatePath, materialAssetPath))
                throw new IOException("Unity could not create the generated poster material at: " + materialAssetPath);

            AssetDatabase.ImportAsset(materialAssetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialAssetPath);
            if (material == null)
                throw new InvalidOperationException("The generated poster material could not be loaded: " + materialAssetPath);

            material.SetTexture("_MainTex", posterTexture);
            material.SetTextureScale("_MainTex", Vector2.one);
            material.SetTextureOffset("_MainTex", Vector2.zero);
            if (material.HasProperty("_BaseMap"))
                material.SetTexture("_BaseMap", posterTexture);

            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(materialAssetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            return material;
        }

        private static ItemModConfig CreatePosterConfig(string configAssetPath, string textureAssetPath, BuildJob job, PosterDefinition poster)
        {
            if (poster == null)
                throw new ArgumentNullException("poster");

            if (AssetDatabase.LoadAssetAtPath<ItemModConfig>(MuralTemplateConfigPath) == null)
                throw new FileNotFoundException("The official Two Point mural example config is missing: " + MuralTemplateConfigPath);

            if (string.IsNullOrEmpty(poster.FrameFbxAssetPath) || string.IsNullOrEmpty(poster.PosterFbxAssetPath) || string.IsNullOrEmpty(poster.MaterialTemplatePath))
                throw new InvalidDataException("Poster gold-standard asset definition is incomplete for " + poster.TemplateName + ".");

            if (!AssetDatabase.CopyAsset(MuralTemplateConfigPath, configAssetPath))
                throw new IOException("Unity could not create the poster ItemModConfig at: " + configAssetPath);

            AssetDatabase.ImportAsset(configAssetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            ItemModConfig config = AssetDatabase.LoadAssetAtPath<ItemModConfig>(configAssetPath);
            if (config == null)
                throw new InvalidOperationException("The generated poster ItemModConfig could not be loaded.");

            InitialiseItemConfig(config, job, poster.BaseArchetypeId, poster.BaseItemName);
            config.TextureOnlyMod = false;
            config.HideAllCustomisationVariantMeshes = false;
            config.HideAllRandomVariantMeshes = false;
            config.UseExistingCustomisationVariants = true;
            config.UseExistingRandomVariants = true;
            config.Textures = new List<ItemModTextureDef>();
            config.Meshes = new List<ItemModMeshDef>();

            Material posterMaterial = CreatePosterMaterial(configAssetPath, textureAssetPath, poster);

            foreach (string frameMeshName in PosterFrameMeshNames)
            {
                config.Textures.Add(new ItemModTextureDef { MeshName = frameMeshName });
                config.Meshes.Add(new ItemModMeshDef
                {
                    MeshName = frameMeshName,
                    MeshReference = CreateReplacementMeshReference(poster.FrameFbxAssetPath, frameMeshName)
                });
            }

            config.Textures.Add(new ItemModTextureDef { MeshName = PosterTextureMeshName });
            config.Meshes.Add(new ItemModMeshDef
            {
                MeshName = PosterTextureMeshName,
                MeshReference = CreateReplacementMeshReference(poster.PosterFbxAssetPath, PosterTextureMeshName),
                MaterialList = new List<Material> { posterMaterial }
            });

            Debug.Log(string.Format(
                "[Memento Maker] Poster size pipeline: Size={0}; Base={1} ({2}); FrameFBX={3}; PosterFBX={4}; TextureOnlyMod=false.",
                poster.TemplateName, poster.BaseItemName, poster.BaseArchetypeId, poster.FrameFbxAssetPath, poster.PosterFbxAssetPath));

            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(configAssetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            return config;
        }

        private static void InitialiseItemConfig(ItemModConfig config, BuildJob job, long baseArchetypeId, string baseItemName)
        {
            config.Version = ItemModConfigVersion.OnePastLatest - 1;
            config.BaseArchetypeID = baseArchetypeId;
            ApplyItemCustomisation(config, job, baseArchetypeId);
            config.ItemName = job.modName;
            config.ItemDescription = job.description ?? "";
            config.ItemModID = GetOrCreateItemModId(job.itemModId);
            config.ItemCost = job.itemCost;
            config.KudoshCost = job.kudoshCost;
            config.HideAllBaseItemMeshes = false;
            config.UseExistingCustomisationVariants = true;
            config.UseExistingRandomVariants = true;
            config.CurrentItemFilter = baseItemName;
            config.SelectedBySearchFilter = true;

            config.EnvironmentAttributeModifiers = new List<EnvironmentAttributeModSpecData>();
            config.RoomPowerModifiers = new List<RoomModifierPowerModSpecData>();
            config.HasRoomCapacityModifier = false;
            config.RoomCapacityModifier = default(CapacityModSpecData);
            config.CustomisationVariantMeshes = new List<ItemModMeshDef>();
            config.RandomVariantMeshes = new List<ItemModMeshDef>();
            config.EnhancerMaterials = new List<ItemModEnhancerMatDef>();
            config.MaintenanceMaterials = default(ItemModMaintenanceMatDef);
        }

        private static void ApplyItemCustomisation(ItemModConfig config, BuildJob job, long baseArchetypeId)
        {
            if (config == null)
                throw new ArgumentNullException("config");

            string mode = string.IsNullOrWhiteSpace(job.variantMode) ? "Standalone" : job.variantMode.Trim();
            config.ItemCustomisationID = GetOrCreateItemCustomisationId(job.itemCustomisationId);

            if (string.Equals(mode, "BaseGame", StringComparison.OrdinalIgnoreCase))
            {
                config.CustomisationType = ResolveCustomisationType("Base");
                config.CustomisationName = job.modName ?? "";
                long parentBaseArchetypeId = baseArchetypeId;
                long overrideBaseArchetypeId;
                if (!string.IsNullOrWhiteSpace(job.variantParentBaseArchetypeId) &&
                    long.TryParse(job.variantParentBaseArchetypeId, out overrideBaseArchetypeId) &&
                    overrideBaseArchetypeId != 0)
                {
                    parentBaseArchetypeId = overrideBaseArchetypeId;
                }
                config.ParentItemCustomisationID = parentBaseArchetypeId;
                job.kudoshCost = 0;
                return;
            }

            if (string.Equals(mode, "Modded", StringComparison.OrdinalIgnoreCase))
            {
                long parentModId;
                if (!long.TryParse(job.variantParentModId, out parentModId) || parentModId == 0)
                    throw new ArgumentException("variantParentModId must contain the Item Mod ID of a valid parent mod.");
                config.CustomisationType = ResolveCustomisationType("Modded");
                config.CustomisationName = job.modName ?? "";
                config.ParentItemCustomisationID = parentModId;
                job.kudoshCost = 0;
                return;
            }

            config.CustomisationType = ItemModConfig.ItemCustomisationType.None;
            config.CustomisationName = "";
            config.ParentItemCustomisationID = 0;
        }

        private static ItemModConfig.ItemCustomisationType ResolveCustomisationType(string kind)
        {
            string[] names = Enum.GetNames(typeof(ItemModConfig.ItemCustomisationType));
            for (int i = 0; i < names.Length; i++)
            {
                string name = names[i] ?? "";
                if (name.IndexOf(kind, StringComparison.OrdinalIgnoreCase) >= 0)
                    return (ItemModConfig.ItemCustomisationType)Enum.Parse(typeof(ItemModConfig.ItemCustomisationType), name);
            }

            // The official inspector orders these as None, Base Game Item Customisation,
            // Modded Item Customisation. Retain that as a compatibility fallback in case
            // the SDK enum names are changed while preserving their serialized values.
            return string.Equals(kind, "Base", StringComparison.OrdinalIgnoreCase)
                ? (ItemModConfig.ItemCustomisationType)1
                : (ItemModConfig.ItemCustomisationType)2;
        }

        private static long GetOrCreateItemCustomisationId(string value)
        {
            long id;
            if (!string.IsNullOrWhiteSpace(value) && long.TryParse(value, out id) && id != 0)
                return id;

            id = ModItemIDGenerator.GenerateUniqueID();
            if (id == 0)
                throw new InvalidOperationException("The SDK generated an invalid Item Customisation ID of 0.");
            return id;
        }

        private static int FindTextureEntry(List<ItemModTextureDef> textures, string meshName)
        {
            for (int i = 0; i < textures.Count; i++)
            {
                if (textures[i].MeshName == meshName)
                    return i;
            }
            return -1;
        }

        private static long GetOrCreateItemModId(string requestedId)
        {
            if (!string.IsNullOrWhiteSpace(requestedId))
            {
                long parsed;
                if (!long.TryParse(requestedId, out parsed) || parsed <= 0)
                    throw new ArgumentException("itemModId must be a positive 64-bit integer when supplied.");
                return parsed;
            }

            return ModItemIDGenerator.GenerateUniqueID();
        }

        private static Texture2D ImportAndConfigureTexture(string assetPath, string templateName)
        {
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
                throw new InvalidOperationException("Unity did not import the selected image as a texture: " + assetPath);

            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.mipmapEnabled = true;
            importer.streamingMipmaps = true;
            importer.streamingMipmapsPriority = 5;
            importer.isReadable = false;
            RugDefinition importedRug = IsRugTemplate(templateName) ? GetRugDefinition(templateName) : null;
            // Rug replacement meshes place some side UVs directly against the texture
            // boundary. Clamp prevents filtering from wrapping across to the opposite edge.
            // The desktop app now supplies the artwork directly with no artificial padding/bleed.
            importer.wrapMode = (IsPosterTemplate(templateName) || importedRug != null || IsBannerTemplate(templateName) || IsDoubleBannerTemplate(templateName) || IsHangingSignTemplate(templateName) || IsWallSignTemplate(templateName)) ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Bilinear;
            importer.anisoLevel = 2;
            importer.alphaIsTransparency = false;
            importer.textureCompression = TextureImporterCompression.Compressed;
            int requiredMaxSize = (templateName == TemplateMural || templateName == TemplateWallpaper) ? 2048 : 1024;
            importer.maxTextureSize = requiredMaxSize;

            TextureImporterPlatformSettings defaultSettings = importer.GetDefaultPlatformTextureSettings();
            defaultSettings.maxTextureSize = requiredMaxSize;
            defaultSettings.textureCompression = TextureImporterCompression.Compressed;
            defaultSettings.compressionQuality = 50;
            importer.SetPlatformTextureSettings(defaultSettings);

            importer.SaveAndReimport();

            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            if (texture == null)
                throw new InvalidOperationException("The imported texture could not be loaded: " + assetPath);

            if (templateName == TemplateWallpaper)
            {
                Debug.Log(string.Format("[Memento Maker] Imported Wallpaper room-visual texture {0} ({1}x{2}). Wrap mode: Repeat.", assetPath, texture.width, texture.height));
            }
            else if (templateName == TemplateMural)
            {
                Debug.Log(string.Format("[TPM Simple Mod Maker] Imported mural image {0} ({1}x{2}). The official mural example is 2048x512 (4:1); other aspect ratios may stretch in-game.", assetPath, texture.width, texture.height));
            }
            else if (IsRugTemplate(templateName))
            {
                Debug.Log(string.Format("[TPM Simple Mod Maker] Imported rug texture {0} ({1}x{2}) for {3}. Pipeline: replacement mesh + Two Point/Lit material.", assetPath, texture.width, texture.height, templateName));
            }
            else if (IsDoubleBannerTemplate(templateName))
            {
                Debug.Log(string.Format("[Memento Maker] Imported Double Banner 1024x1024 combined artwork texture {0} ({1}x{2}) for {3}.", assetPath, texture.width, texture.height, templateName));
            }
            else if (IsHangingSignTemplate(templateName))
            {
                Debug.Log(string.Format("[Memento Maker] Imported Hanging Sign 1024x1024 combined artwork texture {0} ({1}x{2}) for {3}. Sign material only is replaced.", assetPath, texture.width, texture.height, templateName));
            }
            else if (IsWallSignTemplate(templateName))
            {
                Debug.Log(string.Format("[Memento Maker] Imported Wall Sign 1024x1024 combined artwork texture {0} ({1}x{2}) for {3}. Sign material only is replaced.", assetPath, texture.width, texture.height, templateName));
            }
            else if (IsBannerTemplate(templateName))
            {
                Debug.Log(string.Format("[Memento Maker] Imported Single Banner artwork texture {0} ({1}x{2}) for {3}.", assetPath, texture.width, texture.height, templateName));
            }
            else if (IsPosterTemplate(templateName))
            {
                Debug.Log(string.Format("[Memento Maker] Imported {3} artwork texture {0} ({1}x{2}). The supplied replacement Poster.fbx receives the generated material and the matching Frame.fbx supplies the poster frame geometry.", assetPath, texture.width, texture.height, templateName));
            }
            return texture;
        }

        private static void BuildAddressablesForFamily(IList<string> configAssetPaths, IList<string> packedAssetPaths)
        {
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
                throw new InvalidOperationException("AddressableAssetSettings could not be loaded.");

            AddressableAssetGroup modConfigs = settings.FindGroup("ModConfigs");
            AddressableAssetGroup packedAssets = settings.FindGroup("Packed Assets");
            if (modConfigs == null || packedAssets == null)
                throw new InvalidOperationException("Expected Two Point Addressables groups 'ModConfigs' and 'Packed Assets' were not found.");
            if (!(settings.ActivePlayerDataBuilder is TPCAddressablesBuildScript))
                throw new InvalidOperationException("The active Addressables Player Data Builder is not 'Two Point Modding Build Script'.");

            string moddingProfileId = settings.profileSettings.GetProfileId("TPC Modding Profile");
            if (string.IsNullOrEmpty(moddingProfileId))
                throw new InvalidOperationException("The 'TPC Modding Profile' Addressables profile was not found.");

            string originalProfileId = settings.activeProfileId;
            List<EntrySnapshot> originalModConfigEntries = SnapshotEntries(modConfigs);
            List<EntrySnapshot> originalPackedEntries = SnapshotEntries(packedAssets);
            try
            {
                settings.activeProfileId = moddingProfileId;
                ClearEntries(modConfigs);
                ClearEntries(packedAssets);

                HashSet<string> seenGuids = new HashSet<string>(StringComparer.Ordinal);
                if (configAssetPaths != null)
                {
                    foreach (string configPath in configAssetPaths)
                    {
                        if (string.IsNullOrWhiteSpace(configPath))
                            continue;
                        string guid = AssetDatabase.AssetPathToGUID(configPath);
                        if (string.IsNullOrEmpty(guid))
                            throw new InvalidOperationException("Unity did not assign a GUID to combined ModConfig: " + configPath);
                        if (!seenGuids.Add(guid))
                            continue;
                        AddressableAssetEntry entry = settings.CreateOrMoveEntry(guid, modConfigs, false, true);
                        entry.address = configPath;
                    }
                }

                seenGuids.Clear();
                if (packedAssetPaths != null)
                {
                    foreach (string assetPath in packedAssetPaths)
                    {
                        if (string.IsNullOrWhiteSpace(assetPath))
                            continue;
                        string guid = AssetDatabase.AssetPathToGUID(assetPath);
                        if (string.IsNullOrEmpty(guid))
                            throw new InvalidOperationException("Unity did not assign a GUID to combined packed asset: " + assetPath);
                        if (!seenGuids.Add(guid))
                            continue;
                        AddressableAssetEntry entry = settings.CreateOrMoveEntry(guid, packedAssets, false, true);
                        entry.address = assetPath;
                    }
                }

                EditorUtility.SetDirty(modConfigs);
                EditorUtility.SetDirty(packedAssets);
                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssets();

                AddressablesPlayerBuildResult result;
                AddressableAssetSettings.BuildPlayerContent(out result);
                if (result == null)
                    throw new InvalidOperationException("Addressables returned no build result for the combined package.");
                if (!string.IsNullOrEmpty(result.Error))
                    throw new InvalidOperationException("Two Point combined-package Addressables build failed: " + result.Error);
            }
            finally
            {
                ClearEntries(modConfigs);
                ClearEntries(packedAssets);
                RestoreEntries(settings, modConfigs, originalModConfigEntries);
                RestoreEntries(settings, packedAssets, originalPackedEntries);
                settings.activeProfileId = originalProfileId;
                EditorUtility.SetDirty(modConfigs);
                EditorUtility.SetDirty(packedAssets);
                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssets();
            }
        }

        private static void BuildAddressablesForOnlyThisMod(string configAssetPath, string textureAssetPath, params string[] additionalAssetPaths)
        {
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
                throw new InvalidOperationException("AddressableAssetSettings could not be loaded.");

            AddressableAssetGroup modConfigs = settings.FindGroup("ModConfigs");
            AddressableAssetGroup packedAssets = settings.FindGroup("Packed Assets");
            if (modConfigs == null || packedAssets == null)
                throw new InvalidOperationException("Expected Two Point Addressables groups 'ModConfigs' and 'Packed Assets' were not found.");

            if (!(settings.ActivePlayerDataBuilder is TPCAddressablesBuildScript))
                throw new InvalidOperationException("The active Addressables Player Data Builder is not 'Two Point Modding Build Script'. Open Addressables Groups and select the official Two Point builder, then try again.");

            string moddingProfileId = settings.profileSettings.GetProfileId("TPC Modding Profile");
            if (string.IsNullOrEmpty(moddingProfileId))
                throw new InvalidOperationException("The 'TPC Modding Profile' Addressables profile was not found.");

            string originalProfileId = settings.activeProfileId;
            List<EntrySnapshot> originalModConfigEntries = SnapshotEntries(modConfigs);
            List<EntrySnapshot> originalPackedEntries = SnapshotEntries(packedAssets);

            try
            {
                settings.activeProfileId = moddingProfileId;
                ClearEntries(modConfigs);
                ClearEntries(packedAssets);

                string configGuid = AssetDatabase.AssetPathToGUID(configAssetPath);
                string textureGuid = AssetDatabase.AssetPathToGUID(textureAssetPath);

                AddressableAssetEntry configEntry = settings.CreateOrMoveEntry(configGuid, modConfigs, false, true);
                configEntry.address = configAssetPath;

                AddressableAssetEntry textureEntry = settings.CreateOrMoveEntry(textureGuid, packedAssets, false, true);
                textureEntry.address = textureAssetPath;

                if (additionalAssetPaths != null)
                {
                    foreach (string additionalPath in additionalAssetPaths)
                    {
                        if (string.IsNullOrWhiteSpace(additionalPath))
                            continue;

                        string guid = AssetDatabase.AssetPathToGUID(additionalPath);
                        if (string.IsNullOrEmpty(guid))
                            throw new InvalidOperationException("Unity did not assign a GUID to addressable asset: " + additionalPath);

                        AddressableAssetEntry extraEntry = settings.CreateOrMoveEntry(guid, packedAssets, false, true);
                        extraEntry.address = additionalPath;
                    }
                }

                EditorUtility.SetDirty(modConfigs);
                EditorUtility.SetDirty(packedAssets);
                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssets();

                AddressablesPlayerBuildResult result;
                AddressableAssetSettings.BuildPlayerContent(out result);

                if (result == null)
                    throw new InvalidOperationException("Addressables returned no build result.");

                if (!string.IsNullOrEmpty(result.Error))
                    throw new InvalidOperationException("Two Point Addressables build failed: " + result.Error);
            }
            finally
            {
                ClearEntries(modConfigs);
                ClearEntries(packedAssets);
                RestoreEntries(settings, modConfigs, originalModConfigEntries);
                RestoreEntries(settings, packedAssets, originalPackedEntries);
                settings.activeProfileId = originalProfileId;

                EditorUtility.SetDirty(modConfigs);
                EditorUtility.SetDirty(packedAssets);
                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssets();
            }
        }

        private static List<EntrySnapshot> SnapshotEntries(AddressableAssetGroup group)
        {
            List<EntrySnapshot> snapshots = new List<EntrySnapshot>();
            foreach (AddressableAssetEntry entry in group.entries)
            {
                snapshots.Add(new EntrySnapshot
                {
                    Guid = entry.guid,
                    Address = entry.address,
                    ReadOnly = entry.ReadOnly,
                    Labels = entry.labels == null ? new string[0] : entry.labels.ToArray()
                });
            }
            return snapshots;
        }

        private static void ClearEntries(AddressableAssetGroup group)
        {
            foreach (AddressableAssetEntry entry in group.entries.ToList())
                group.RemoveAssetEntry(entry, false);
        }

        private static void RestoreEntries(AddressableAssetSettings settings, AddressableAssetGroup group, List<EntrySnapshot> snapshots)
        {
            foreach (EntrySnapshot snapshot in snapshots)
            {
                AddressableAssetEntry entry = settings.CreateOrMoveEntry(snapshot.Guid, group, snapshot.ReadOnly, false);
                entry.address = snapshot.Address;
                if (snapshot.Labels != null)
                {
                    foreach (string label in snapshot.Labels)
                        entry.SetLabel(label, true, false, false);
                }
            }
        }

        private static void CopyFileIntoProject(string sourcePath, string assetPath)
        {
            string absoluteDestination = AssetPathToAbsolutePath(assetPath);
            Directory.CreateDirectory(Path.GetDirectoryName(absoluteDestination));
            File.Copy(sourcePath, absoluteDestination, true);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        private static void CreateUnityFolderForAsset(string assetPath)
        {
            string absolutePath = AssetPathToAbsolutePath(assetPath);
            string directory = Path.GetDirectoryName(absolutePath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        private static string AssetPathToAbsolutePath(string assetPath)
        {
            if (!assetPath.StartsWith("Assets/", StringComparison.Ordinal))
                throw new ArgumentException("Expected an Assets/... path: " + assetPath);

            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string relative = assetPath.Replace('/', Path.DirectorySeparatorChar);
            return Path.Combine(projectRoot, relative);
        }

        private static string ResolveExternalPath(string value, string baseDirectory)
        {
            if (Path.IsPathRooted(value))
                return Path.GetFullPath(value);
            return Path.GetFullPath(Path.Combine(baseDirectory, value));
        }

        private static string MakeSafeName(string value)
        {
            string safe = SanitiseAssetComponent(value, 0);
            return string.IsNullOrEmpty(safe) ? "GeneratedMod" : safe;
        }

        private static string BuildItemConfigFileName(string itemName, string moddersName)
        {
            string item = SanitiseAssetComponent(itemName, 15);
            string modder = SanitiseAssetComponent(moddersName, 0);
            if (string.IsNullOrEmpty(item))
                item = "Item";
            if (string.IsNullOrEmpty(modder))
                modder = "Modder";
            return item + "_" + modder + ".asset";
        }

        private static string SanitiseAssetComponent(string value, int maxCharacters)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "";

            StringBuilder cleaned = new StringBuilder();
            bool previousWasSpace = false;
            foreach (char c in value.Trim())
            {
                if (char.IsLetterOrDigit(c))
                {
                    cleaned.Append(c);
                    previousWasSpace = false;
                }
                else if (char.IsWhiteSpace(c))
                {
                    if (cleaned.Length > 0 && !previousWasSpace)
                    {
                        cleaned.Append(' ');
                        previousWasSpace = true;
                    }
                }
                // Punctuation, symbols and other special characters are deliberately ignored.
            }

            string result = cleaned.ToString().Trim();
            if (maxCharacters > 0 && result.Length > maxCharacters)
                result = result.Substring(0, maxCharacters).Trim();
            result = result.Replace(' ', '_');
            while (result.Contains("__"))
                result = result.Replace("__", "_");
            return result.Trim('_');
        }

        private static void CopyDirectory(string sourceDirectory, string destinationDirectory)
        {
            DirectoryInfo source = new DirectoryInfo(sourceDirectory);
            if (!source.Exists)
                throw new DirectoryNotFoundException(sourceDirectory);

            Directory.CreateDirectory(destinationDirectory);

            foreach (FileInfo file in source.GetFiles())
                file.CopyTo(Path.Combine(destinationDirectory, file.Name), true);

            foreach (DirectoryInfo directory in source.GetDirectories())
                CopyDirectory(directory.FullName, Path.Combine(destinationDirectory, directory.Name));
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

        private static void WriteResult(string jobPath, BuildResultFile result)
        {
            string resultPath = jobPath + ".result.json";
            File.WriteAllText(resultPath, JsonUtility.ToJson(result, true));
        }
    }
}
