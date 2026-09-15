using System.Collections.Generic;
using System.Diagnostics;
using TPC;
using TPS.Core.Rendering;
using TPS.Game;
using UnityEditor;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;
using UnityEngine.AddressableAssets;
using Object = UnityEngine.Object;

namespace  TPS.Game
{
    public static class AssetUtils
    {
        public static T FindAssetByType<T>(string[] searchInFolders = null) where T : Object
        {
            var assets = new List<T>();
            var guids = AssetDatabase.FindAssets($"t:{typeof(T)}", searchInFolders);

            foreach (var guid in guids)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
                if (asset != null)
                {
                    return asset;
                }
            }

            return null;
        }
    }
}



[CreateAssetMenu(fileName = "TPCAddressablesBuildScript.asset", menuName = "TPS/Build/AddressablesBuildScript")]
public class TPCAddressablesBuildScript : BuildScriptPackedMode
{
    public static BuildTarget CurrentBuildTarget;
    public static BuildTargetGroup CurrentBuildTargetGroup;

    private struct TPCPlatformBuildTarget
    {
        public TPCPlatformBuildTarget(string name, BuildTarget buildTarget, BuildTargetGroup buildTargetGroup)
        {
            PlatformName = name;
            BuildTarget = buildTarget;
            BuildTargetGroup = buildTargetGroup;
        }
        
        public string PlatformName;
        public BuildTarget BuildTarget;
        public BuildTargetGroup BuildTargetGroup;
    }
    
    private struct ModMeshMaterialsCache
    {
        public string MeshName;
        public List<Material> Materials;
    }
    
    private class ItemModMaterialsCache
    {
        public ItemModMaterialsCache()
        {
            MeshesCache = new List<ModMeshMaterialsCache>();
            CustomisationVariantMeshesCache = new List<ModMeshMaterialsCache>();
            RandomVariantMeshesCache = new List<ModMeshMaterialsCache>();
            MeshPrefabMaterialsCache = new List<ModMeshMaterialsCache>();
        }
        
        public List<ModMeshMaterialsCache> MeshesCache;
        public List<ModMeshMaterialsCache> CustomisationVariantMeshesCache;
        public List<ModMeshMaterialsCache> RandomVariantMeshesCache;

        public List<ModMeshMaterialsCache> MeshPrefabMaterialsCache;
    }

    private enum MeshType
    {
        Mesh = 0,
        CustomisationMesh = 1,
        RandomMesh = 2
    }

    private List<TPCPlatformBuildTarget> PlatformBuildTargets = new List<TPCPlatformBuildTarget>()
    {
        new TPCPlatformBuildTarget("PC", BuildTarget.StandaloneWindows64, BuildTargetGroup.Standalone), // These mods should work on Mac and Linux too
        new TPCPlatformBuildTarget( "Xbox Series X", BuildTarget.GameCoreXboxSeries, BuildTargetGroup.GameCoreXboxSeries),
        new TPCPlatformBuildTarget( "Xbox One", BuildTarget.GameCoreXboxOne, BuildTargetGroup.GameCoreXboxOne),
        new TPCPlatformBuildTarget( "Switch", BuildTarget.Switch, BuildTargetGroup.Switch),
        new TPCPlatformBuildTarget( "PS4", BuildTarget.PS4, BuildTargetGroup.PS4),
        new TPCPlatformBuildTarget( "PS5", BuildTarget.PS5, BuildTargetGroup.PS5)
    };
    
    private static List<string> ExampleModAssetIDs = new List<string>()
    {
        // Alien plant mod guids
        "6fb6c03065ca4874b91aa4ba6a7669ba",
        "817524aac633e5b4d8dbd1344674e76d",
        "29b4225a948787848894b313bdbbe066",
        "7913e62d1f7b3424a8a65d9e59edf57a",
        "8a795d759015ec74595d1acc23d5c5a4",
        "637761e8d64c91e499f4656df7241675",
        "9d9d203c9e07faf4f9e1dad2124aab3a",
        "5b9d400dc17592346a5410e10bf44f6d",
        "434c549568a5c4b4b9b621c0a41e1e10",
        "312a4187efb07e04d93eadc3c3b761a5",
        // Wall mod guids
        "7001a167a3099794c8f158c0313e0987",
        "3227f4e2b550d6e4a821d2cbeb3a1b6a",
        "c45a327fe0690e54ca17b781d4c3f0fd",
        // Particle mod guids
        "92fa7a33ef6a32e4e8de770d3467754b",
        "b0c3ba5eaa26d6a4a842709135368146",
        "d1e2489a8f2bb684e88713c308e905fc",
        // Mural mod guids
        "5bd23e2b589b0774cb86b14d61c5a076",
        "aebab1da4a35ba34287ff0fe2648ae4b"
    };

    private Dictionary<long, ItemModMaterialsCache> ItemMaterialsCache;
    private Dictionary<long, Material> RoomVisualMaterialsCache;
    private Dictionary<long, Material> MaintenanceMaterialsCache;
    private Dictionary<long, List<ItemModEnhancerMatDef>> EnhancerMaterialsCache;
    private Dictionary<Renderer, string[]> RenderersMaterialsCache;
    private List<TPCBuildConfigEntry> _buildPreferences = new List<TPCBuildConfigEntry>()
    {
        new TPCBuildConfigEntry(){ BuildTarget =  BuildTarget.StandaloneWindows64, ExportEnabled = true},
        new TPCBuildConfigEntry(){ BuildTarget =  BuildTarget.GameCoreXboxOne, ExportEnabled = false},
        new TPCBuildConfigEntry(){ BuildTarget =  BuildTarget.GameCoreXboxSeries, ExportEnabled = false},
        new TPCBuildConfigEntry(){ BuildTarget =  BuildTarget.PS4, ExportEnabled = false},
        new TPCBuildConfigEntry(){ BuildTarget =  BuildTarget.PS5, ExportEnabled = false},
        new TPCBuildConfigEntry(){ BuildTarget =  BuildTarget.Switch, ExportEnabled = false}
    };
    
    public override string Name
    {
        get { return "Two Point Modding Build Script"; }
    }

    private bool IsEnabledInBuildConfig(BuildTarget buildTarget)
    {
        foreach (var preferences in _buildPreferences)
        {
            if (preferences.BuildTarget == buildTarget)
            {
                return preferences.ExportEnabled;
            }
        }

        return true;
    }

    protected override TResult BuildDataImplementation<TResult>(AddressablesDataBuilderInput builderInput)
    {
        // Important!  Need to use our alternative serialized file writer class
        WriteSerializedFileClass = typeof(TPCWriteSerializedFiles);
        
        TResult result = default(TResult);

        var timer = new Stopwatch();
        timer.Start();

        ItemMaterialsCache = new Dictionary<long, ItemModMaterialsCache>();
        RoomVisualMaterialsCache = new Dictionary<long, Material>();
        MaintenanceMaterialsCache = new Dictionary<long, Material>();
        EnhancerMaterialsCache = new Dictionary<long, List<ItemModEnhancerMatDef>>();
        
        _buildPreferences = new List<TPCBuildConfigEntry>()
        {
            new TPCBuildConfigEntry(){ BuildTarget =  BuildTarget.StandaloneWindows64, ExportEnabled = true},
            new TPCBuildConfigEntry(){ BuildTarget =  BuildTarget.GameCoreXboxOne, ExportEnabled = false},
            new TPCBuildConfigEntry(){ BuildTarget =  BuildTarget.GameCoreXboxSeries, ExportEnabled = false},
            new TPCBuildConfigEntry(){ BuildTarget =  BuildTarget.PS4, ExportEnabled = false},
            new TPCBuildConfigEntry(){ BuildTarget =  BuildTarget.PS5, ExportEnabled = false},
            new TPCBuildConfigEntry(){ BuildTarget =  BuildTarget.Switch, ExportEnabled = false}
        };
        
        // TPM Simple Mod Maker: command-line builds cannot interact with modal Editor windows.
        // In batch mode the default build preferences above already select PC only, which is
        // exactly what the automated builder requires. Interactive Unity builds retain the
        // original Two Point platform-selection popup.
        if (!Application.isBatchMode)
        {
            TPCBuildConfigPopup window = CreateInstance<TPCBuildConfigPopup>();
            window.BuildPreferences = _buildPreferences;
            window.position = new Rect(Screen.width / 2, Screen.height / 2, 200, 300);
            window.ShowModal();
        }
        
        // Run the build process for each platform target (if enabled in config)
        foreach (var platform in PlatformBuildTargets)
        {
            if (IsEnabledInBuildConfig(platform.BuildTarget))
            {
                
                RenderersMaterialsCache = new Dictionary<Renderer, string[]>();
                CurrentBuildTarget = platform.BuildTarget;
                CurrentBuildTargetGroup = platform.BuildTargetGroup;

                InitializeBuildContext(builderInput, out AddressableAssetsBuildContext aaContext);

                // Force correct bundle schema settings
                
                foreach (var group in aaContext.Settings.groups)
                {
                    foreach (var schema in group.Schemas)
                    {
                        if (schema is BundledAssetGroupSchema bundleSchema)
                        {
                            // TODO: If/when Switch/PS4/PS5 consoles are working, figure out if this is necessary.  I'm concerned that
                            // this bundle naming strategy could result in conflicts between mods that share the same group names.
                            bundleSchema.BundleNaming = BundledAssetGroupSchema.BundleNamingStyle.NoHash;
                            
                            // Need to disable CRC checking due to a Unity bug where it fails randomly
                            bundleSchema.UseAssetBundleCrc = false;
                            bundleSchema.UseAssetBundleCrcForCachedBundles = false;
                        }
                    }
                }

                // The primary purpose of the our custom processing of groups is to strip materials and replace
                // them with our own custom structure (for cross-platform support)
                var errorString = ProcessAllGroups(aaContext);
                
                TResult createAssetBuildResult = default(TResult);
                if (!string.IsNullOrEmpty(errorString))
                    createAssetBuildResult = AddressableAssetBuildResult.CreateResult<TResult>(null, 0, errorString);

                if (createAssetBuildResult == null)
                {
                    var prefixGuid = GUID.Generate().ToString();
                    aaContext.Settings.ShaderBundleCustomNaming = prefixGuid;
                    aaContext.Settings.MonoScriptBundleCustomNaming = prefixGuid;

                    var profileID = aaContext.Settings.profileSettings.GetProfileId("TPC Modding Profile");
                    aaContext.Settings.profileSettings.SetValue(profileID, "Local.BuildPath",
                        "[UnityEngine.Application.dataPath]/Exported~/" + platform.PlatformName);

                    result = DoBuild<TResult>(builderInput, aaContext);

                    aaContext.Settings.profileSettings.SetValue(profileID, "Local.BuildPath",
                        "[UnityEngine.Application.dataPath]/Exported~");
                }
                
                // Restore all the materials that were stripped out for this build
                for (int index = 0; index < aaContext.Settings.groups.Count; index++)
                {
                    RestoreAssetDataForGroup(aaContext.Settings.groups[index]);
                }
                
                AssetDatabase.SaveAssets();
                
            }
        }

        if (result != null)
            result.Duration = timer.Elapsed.TotalSeconds;

        return result;
    }
    
    protected override string ProcessGroup(AddressableAssetGroup assetGroup, AddressableAssetsBuildContext aaContext)
    {
        bool detectedNonTwoPointShader = false;
        List<string> nonTwoPointShaders = new List<string>();
        var oldModConfigStates = new List<ItemModConfig>();
        var assetNames = new List<string>();
        foreach (var entry in assetGroup.entries)
        {
            if (ExampleModAssetIDs.Contains(entry.guid))
            {
                assetNames.Add(entry.AssetPath);
            }

            // Strip and cache materials from ItemModConfigs and RoomVisualModConfigs (relatively straightforward)
            if (entry.MainAsset is RoomVisualModConfig roomVisualModConfig)
            {
                if (roomVisualModConfig.Material != null)
                {
                    Material cachedMaterial = null;
                    roomVisualModConfig.MaterialData = new TPSMaterialData();
                    CacheMaterial(ref roomVisualModConfig.Material, ref cachedMaterial, ref roomVisualModConfig.MaterialData);

                    RoomVisualMaterialsCache[roomVisualModConfig.RoomVisualModID] = cachedMaterial;
                    EditorUtility.SetDirty(entry.MainAsset);
                }
            }
            else if (entry.MainAsset is ItemModConfig itemModConfig)
            {
                var matCache = new ItemModMaterialsCache();
                
                CacheModMeshMaterials(ref itemModConfig.Meshes, ref matCache, MeshType.Mesh);
                CacheModMeshMaterials(ref itemModConfig.CustomisationVariantMeshes, ref matCache, MeshType.CustomisationMesh);
                CacheModMeshMaterials(ref itemModConfig.RandomVariantMeshes, ref matCache, MeshType.RandomMesh);
                
                ItemMaterialsCache[itemModConfig.ItemModID] = matCache;

                if (itemModConfig.MaintenanceMaterials.MaterialBroken != null)
                {
                    var maintenanceMaterialsStruct = itemModConfig.MaintenanceMaterials;
                    Material cachedMaterial = null;

                    maintenanceMaterialsStruct.MaterialDataBroken = new TPSMaterialData();
                    CacheMaterial(ref maintenanceMaterialsStruct.MaterialBroken, ref cachedMaterial, ref maintenanceMaterialsStruct.MaterialDataBroken);
                    MaintenanceMaterialsCache[itemModConfig.ItemModID] = cachedMaterial;

                    itemModConfig.MaintenanceMaterials = maintenanceMaterialsStruct;
                }

                if (itemModConfig.EnhancerMaterials != null)
                {
                    EnhancerMaterialsCache[itemModConfig.ItemModID] = itemModConfig.EnhancerMaterials;
                    var newEnhancerMatsList = new List<ItemModEnhancerMatDef>();
                    foreach (var enhancerMat in itemModConfig.EnhancerMaterials)
                    {
                        var enhancerMaterialsStruct = enhancerMat;
                        Material cachedMaterial = null;
                        if (enhancerMat.Material != null)
                        {
                            CacheMaterial(ref enhancerMaterialsStruct.Material, ref cachedMaterial, ref enhancerMaterialsStruct.MaterialData);
                            
                            newEnhancerMatsList.Add(enhancerMaterialsStruct);
                        }
                    }

                    itemModConfig.EnhancerMaterials = newEnhancerMatsList;
                }
                
                EditorUtility.SetDirty(entry.MainAsset);
            }
            else if( entry.MainAsset is GameObject gameObject)
            {
                // All other objects still need materials stripped, but it's a bit trickier.
                // Here we grab the path of the original material asset and cache that to restore later
                // This is because mesh renderers don't store direct references to the original material assets
                bool isPrefab = gameObject.hideFlags == HideFlags.None;
                
                if (!isPrefab)
                {
                    var renderer = gameObject.GetComponentsInChildren<Renderer>();
                    
                    for (int i = 0; i < renderer.Length; i++)
                    {
                        RenderersMaterialsCache[renderer[i]] = new string[renderer[i].sharedMaterials.Length];

                        for (int j = 0; j < renderer[i].sharedMaterials.Length; j++)
                        {
                            var currentMat = renderer[i].sharedMaterials[j];
                            if (currentMat != null)
                            {
                                RenderersMaterialsCache[renderer[i]][j] = AssetDatabase.GetAssetPath(currentMat);
                            }
                        }

                        renderer[i].sharedMaterial = null;
                        renderer[i].sharedMaterials = new Material[1];
                    }
                }
                else
                {
                    var renderer = gameObject.GetComponentsInChildren<Renderer>();
                    
                    for (int i = 0; i < renderer.Length; i++)
                    {
                        RenderersMaterialsCache[renderer[i]] = new string[renderer[i].sharedMaterials.Length];
                        
                        // MaterialContainer is a special component that we add to prefabs to store all details of
                        // a material.  We can then reconstruct the material at run-time in-game from this data.
                        var matComp = renderer[i].gameObject.GetComponent<MaterialContainer>();

                        if (matComp == null)
                        {
                            matComp = renderer[i].gameObject.AddComponent<MaterialContainer>();
                        }
                        
                        matComp.MaterialData = new List<TPSMaterialData>();
                        
                        for (int j = 0; j < renderer[i].sharedMaterials.Length; j++)
                        {
                            var currentMat = renderer[i].sharedMaterials[j];
                            if (currentMat != null)
                            {
                                if (!TPSMaterialHelper.CreateMaterialDataFromMaterial(currentMat, out var matData))
                                {
                                    nonTwoPointShaders.Add(currentMat.shader.name);
                                    detectedNonTwoPointShader = true;
                                }

                                matComp.MaterialData.Add(matData);
                                
                                RenderersMaterialsCache[renderer[i]][j] = AssetDatabase.GetAssetPath(currentMat);
                            }
                        }
                        
                        renderer[i].sharedMaterial = null;
                        renderer[i].sharedMaterials = new Material[1];
                    }
                }
            }
        }

        if (detectedNonTwoPointShader)
        {
            var message = "We have detected the use of a non-Two Point shader in a prefab (possibly in a particle effect?).  Note that this mod may function incorrectly on non-Windows platforms.  Shaders detected:";
            foreach (var shaderName in nonTwoPointShaders)
            {
                message += ("\n" + shaderName);
            }
            
            EditorUtility.DisplayDialog($"WARNING: Non-Two Point Shader detected - {assetGroup.name}",
                message,
                "Okay");
        }

        if (assetNames.Count > 0)
        {
            var message =
                "We have detected one or more assets using the example mod Asset IDs, " +
                "you shouldn't include these in your mod as they may cause conflicts with other mods.  " +
                "Please create a separate copy of the following assets (or remove them) before uploading your mod to mod.io:\n\n";

            foreach (var entry in assetNames)
            {
                message += $"{entry}\n";
            }
            
            EditorUtility.DisplayDialog($"WARNING: Example Mod Assets Detected in Group - {assetGroup.name}",
                message,
                "Okay");
            
        }

        return base.ProcessGroup(assetGroup, aaContext);
        
    }

    private void RestoreCachedMaterials(ref List<Material> materialsList, List<Material> materialsCacheList)
    {
        if (materialsList != null)
        {
            foreach (var entry in materialsCacheList)
            {
                materialsList.Add(entry);
            }
        }
    }
    
    private void RestoreAssetDataForGroup(AddressableAssetGroup assetGroup)
    {
        foreach (var entry in assetGroup.entries)
        {
            if (entry.MainAsset is RoomVisualModConfig roomVisualModConfig)
            {
                if (RoomVisualMaterialsCache.ContainsKey(roomVisualModConfig.RoomVisualModID))
                {
                    roomVisualModConfig.Material = RoomVisualMaterialsCache[roomVisualModConfig.RoomVisualModID];
                    EditorUtility.SetDirty(entry.MainAsset);
                }
            }
            else if (entry.MainAsset is ItemModConfig itemModConfig)
            {
                if (ItemMaterialsCache.ContainsKey(itemModConfig.ItemModID))
                {
                    for (int i = 0; i < itemModConfig.Meshes.Count; i++)
                    {
                        var itemMeshData = itemModConfig.Meshes[i];
                        RestoreCachedMaterials(ref itemMeshData.MaterialList,
                            ItemMaterialsCache[itemModConfig.ItemModID].MeshesCache[i].Materials);
                        itemModConfig.Meshes[i] = itemMeshData;
                    }

                    for (int i = 0; i < itemModConfig.CustomisationVariantMeshes.Count; i++)
                    {
                        var itemMeshData = itemModConfig.CustomisationVariantMeshes[i];
                        RestoreCachedMaterials(ref itemMeshData.MaterialList,
                            ItemMaterialsCache[itemModConfig.ItemModID].CustomisationVariantMeshesCache[i].Materials);
                        itemModConfig.CustomisationVariantMeshes[i] = itemMeshData;
                    }

                    for (int i = 0; i < itemModConfig.RandomVariantMeshes.Count; i++)
                    {
                        var itemMeshData = itemModConfig.RandomVariantMeshes[i];
                        RestoreCachedMaterials(ref itemMeshData.MaterialList,
                            ItemMaterialsCache[itemModConfig.ItemModID].RandomVariantMeshesCache[i].Materials);
                        itemModConfig.RandomVariantMeshes[i] = itemMeshData;
                    }

                    // Restore maintenance materials in asset
                    var maintenanceMaterialsStruct = itemModConfig.MaintenanceMaterials;

                    if (MaintenanceMaterialsCache.ContainsKey(itemModConfig.ItemModID))
                    {
                        maintenanceMaterialsStruct.MaterialBroken = MaintenanceMaterialsCache[itemModConfig.ItemModID];
                    }
                    
                    itemModConfig.MaintenanceMaterials = maintenanceMaterialsStruct;

                    // Restore enhancer item materials in asset
                    if (EnhancerMaterialsCache.ContainsKey(itemModConfig.ItemModID))
                    {
                        itemModConfig.EnhancerMaterials = EnhancerMaterialsCache[itemModConfig.ItemModID];
                    }
                    
                    EditorUtility.SetDirty(entry.MainAsset);
                }
            }
            else if (entry.MainAsset is GameObject gameObject)
            {
                var renderers = gameObject.GetComponentsInChildren<Renderer>();

                for (int i = 0; i < renderers.Length; i++)
                {
                    // Check if this mesh renderer has materials in our cache
                    if (RenderersMaterialsCache.ContainsKey(renderers[i]))
                    {
                        var newMaterials = new Material[RenderersMaterialsCache[renderers[i]].Length];

                        // Load all cached materials stored for this mesh renderer and assign them back
                        for (int j = 0; j < newMaterials.Length; j++)
                        {
                            var restoredMat =
                                AssetDatabase.LoadAssetAtPath<Material>(RenderersMaterialsCache[renderers[i]][j]);
                            if (restoredMat != null)
                            {
                                newMaterials[j] = restoredMat;
                            }
                        }
                        
                        renderers[i].sharedMaterials = newMaterials;
                        
                        EditorUtility.SetDirty(entry.MainAsset);
                    }
                }
            }
        }
    }

    private void CacheMaterials(ref List<Material> materialsList, ref List<Material> materialsCacheList, ref List<TPSMaterialData> matDataList)
    {
        matDataList.Clear();
        
        foreach (var mat in materialsList)
        {
            materialsCacheList.Add(mat);

            TPSMaterialData matData = null;

            TPSMaterialHelper.CreateMaterialDataFromMaterial(mat, out matData);
            
            matDataList.Add(matData);
        }
                        
        materialsList.Clear();
    }
    
    private void CacheMaterial(ref Material material, ref Material cachedMaterial, ref TPSMaterialData materialData)
    {
        cachedMaterial = material;

        TPSMaterialHelper.CreateMaterialDataFromMaterial(material, out materialData);
       
        material = null;
    }

    private void CacheModMeshMaterials(ref List<ItemModMeshDef> modMeshDefList, ref ItemModMaterialsCache matCache, MeshType meshType)
    {
        for (int i = 0; i < modMeshDefList.Count; i++)
        {
            var modMeshDef = modMeshDefList[i];
            
            List<Material> meshMaterials = new List<Material>();
            if (modMeshDef.MaterialList != null && modMeshDef.MaterialList.Count > 0)
            {
                CacheMaterials(ref modMeshDef.MaterialList, ref meshMaterials, ref modMeshDef.MaterialsData);
            }

            switch (meshType)
            {
                case MeshType.Mesh:
                    matCache.MeshesCache.Add( new ModMeshMaterialsCache(){ MeshName = modMeshDef.MeshName, Materials = meshMaterials});
                    break;
                case MeshType.CustomisationMesh:
                    matCache.CustomisationVariantMeshesCache.Add( new ModMeshMaterialsCache(){ MeshName = modMeshDef.MeshName, Materials = meshMaterials});
                    break;
                case MeshType.RandomMesh:
                    matCache.RandomVariantMeshesCache.Add( new ModMeshMaterialsCache(){ MeshName = modMeshDef.MeshName, Materials = meshMaterials});
                    break;
            }
            
            // Copy our new struct back into the list (because we can't modify structs in-place in lists)
            modMeshDefList[i] = modMeshDef;
        }
        
    }
}
