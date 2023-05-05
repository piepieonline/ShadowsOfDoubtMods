using System.IO;
using UnityEngine;
using UnityEditor;
using System;
using System.Reflection;

public class ScriptableObjectMenus
{
    static void SaveAsset(ScriptableObject scriptableObject)
    {
        Type projectWindowUtilType = typeof(ProjectWindowUtil);
        MethodInfo getActiveFolderPath = projectWindowUtilType.GetMethod("GetActiveFolderPath", BindingFlags.Static | BindingFlags.NonPublic);
        object obj = getActiveFolderPath.Invoke(null, new object[0]);
        string pathToCurrentFolder = obj.ToString();

        AssetDatabase.CreateAsset(scriptableObject, $"{pathToCurrentFolder}/New {scriptableObject.GetType().Name}.asset");
        AssetDatabase.SaveAssets();

        EditorUtility.FocusProjectWindow();

        Selection.activeObject = scriptableObject;
    }

    [MenuItem("Assets/Create/ShadowsOfDoubt/AddressPreset")]
    static void CreateAsset_AddressPreset() { SaveAsset(ScriptableObject.CreateInstance<AddressPreset>()); }
    [MenuItem("Assets/Create/ShadowsOfDoubt/AIActionPreset")]
    static void CreateAsset_AIActionPreset() { SaveAsset(ScriptableObject.CreateInstance<AIActionPreset>()); }
    [MenuItem("Assets/Create/ShadowsOfDoubt/AIGoalPreset")]
    static void CreateAsset_AIGoalPreset() { SaveAsset(ScriptableObject.CreateInstance<AIGoalPreset>()); }
    [MenuItem("Assets/Create/ShadowsOfDoubt/AmbientZone")]
    static void CreateAsset_AmbientZone() { SaveAsset(ScriptableObject.CreateInstance<AmbientZone>()); }
    [MenuItem("Assets/Create/ShadowsOfDoubt/ArtPreset")]
    static void CreateAsset_ArtPreset() { SaveAsset(ScriptableObject.CreateInstance<ArtPreset>()); }
    [MenuItem("Assets/Create/ShadowsOfDoubt/AudioEvent")]
    static void CreateAsset_AudioEvent() { SaveAsset(ScriptableObject.CreateInstance<AudioEvent>()); }
    [MenuItem("Assets/Create/ShadowsOfDoubt/BookPreset")]
    static void CreateAsset_BookPreset() { SaveAsset(ScriptableObject.CreateInstance<BookPreset>()); }
    [MenuItem("Assets/Create/ShadowsOfDoubt/BroadcastPreset")]
    static void CreateAsset_BroadcastPreset() { SaveAsset(ScriptableObject.CreateInstance<BroadcastPreset>()); }
    [MenuItem("Assets/Create/ShadowsOfDoubt/BroadcastSchedule")]
    static void CreateAsset_BroadcastSchedule() { SaveAsset(ScriptableObject.CreateInstance<BroadcastSchedule>()); }
    [MenuItem("Assets/Create/ShadowsOfDoubt/BuildingPreset")]
    static void CreateAsset_BuildingPreset() { SaveAsset(ScriptableObject.CreateInstance<BuildingPreset>()); }
    [MenuItem("Assets/Create/ShadowsOfDoubt/ChapterPreset")]
    static void CreateAsset_ChapterPreset() { SaveAsset(ScriptableObject.CreateInstance<ChapterPreset>()); }
    [MenuItem("Assets/Create/ShadowsOfDoubt/CharacterTrait")]
    static void CreateAsset_CharacterTrait() { SaveAsset(ScriptableObject.CreateInstance<CharacterTrait>()); }
    [MenuItem("Assets/Create/ShadowsOfDoubt/ClothesPreset")]
    static void CreateAsset_ClothesPreset() { SaveAsset(ScriptableObject.CreateInstance<ClothesPreset>()); }
    [MenuItem("Assets/Create/ShadowsOfDoubt/CloudSaveData")]
    static void CreateAsset_CloudSaveData() { SaveAsset(ScriptableObject.CreateInstance<CloudSaveData>()); }
    [MenuItem("Assets/Create/ShadowsOfDoubt/ColourPalettePreset")]
    static void CreateAsset_ColourPalettePreset() { SaveAsset(ScriptableObject.CreateInstance<ColourPalettePreset>()); }
    [MenuItem("Assets/Create/ShadowsOfDoubt/ColourSchemePreset")]
    static void CreateAsset_ColourSchemePreset() { SaveAsset(ScriptableObject.CreateInstance<ColourSchemePreset>()); }
    [MenuItem("Assets/Create/ShadowsOfDoubt/CompanyOpenHoursPreset")]
    static void CreateAsset_CompanyOpenHoursPreset() { SaveAsset(ScriptableObject.CreateInstance<CompanyOpenHoursPreset>()); }
    [MenuItem("Assets/Create/ShadowsOfDoubt/CompanyPreset")]
    static void CreateAsset_CompanyPreset() { SaveAsset(ScriptableObject.CreateInstance<CompanyPreset>()); }
    [MenuItem("Assets/Create/ShadowsOfDoubt/CompanyStructurePreset")]
    static void CreateAsset_CompanyStructurePreset() { SaveAsset(ScriptableObject.CreateInstance<CompanyStructurePreset>()); }
    [MenuItem("Assets/Create/ShadowsOfDoubt/CriminalPreset")]
    static void CreateAsset_CriminalPreset() { SaveAsset(ScriptableObject.CreateInstance<CriminalPreset>()); }
    [MenuItem("Assets/Create/ShadowsOfDoubt/CruncherAppPreset")]
    static void CreateAsset_CruncherAppPreset() { SaveAsset(ScriptableObject.CreateInstance<CruncherAppPreset>()); }
    [MenuItem("Assets/Create/ShadowsOfDoubt/CutScenePreset")]
    static void CreateAsset_CutScenePreset() { SaveAsset(ScriptableObject.CreateInstance<CutScenePreset>()); }
    [MenuItem("Assets/Create/ShadowsOfDoubt/DDSScope")]
    static void CreateAsset_DDSScope() { SaveAsset(ScriptableObject.CreateInstance<DDSScope>()); }
    [MenuItem("Assets/Create/ShadowsOfDoubt/DesignStylePreset")]
    static void CreateAsset_DesignStylePreset() { SaveAsset(ScriptableObject.CreateInstance<DesignStylePreset>()); }
    [MenuItem("Assets/Create/ShadowsOfDoubt/DialogPreset")]
    static void CreateAsset_DialogPreset() { SaveAsset(ScriptableObject.CreateInstance<DialogPreset>()); }
    [MenuItem("Assets/Create/ShadowsOfDoubt/DistrictPreset")]
    static void CreateAsset_DistrictPreset() { SaveAsset(ScriptableObject.CreateInstance<DistrictPreset>()); }
    [MenuItem("Assets/Create/ShadowsOfDoubt/DoorMovementPreset")]
    static void CreateAsset_DoorMovementPreset() { SaveAsset(ScriptableObject.CreateInstance<DoorMovementPreset>()); }
    [MenuItem("Assets/Create/ShadowsOfDoubt/DoorPairPreset")]
    static void CreateAsset_DoorPairPreset() { SaveAsset(ScriptableObject.CreateInstance<DoorPairPreset>()); }
    [MenuItem("Assets/Create/ShadowsOfDoubt/DoorPreset")]
    static void CreateAsset_DoorPreset() { SaveAsset(ScriptableObject.CreateInstance<DoorPreset>()); }
    [MenuItem("Assets/Create/ShadowsOfDoubt/EffectPreset")]
    static void CreateAsset_EffectPreset() { SaveAsset(ScriptableObject.CreateInstance<EffectPreset>()); }
    [MenuItem("Assets/Create/ShadowsOfDoubt/ElevatorPreset")]
    static void CreateAsset_ElevatorPreset() { SaveAsset(ScriptableObject.CreateInstance<ElevatorPreset>()); }
    [MenuItem("Assets/Create/ShadowsOfDoubt/EvidencePreset")]
    static void CreateAsset_EvidencePreset() { SaveAsset(ScriptableObject.CreateInstance<EvidencePreset>()); }
    [MenuItem("Assets/Create/ShadowsOfDoubt/FactPreset")]
    static void CreateAsset_FactPreset() { SaveAsset(ScriptableObject.CreateInstance<FactPreset>()); }
    [MenuItem("Assets/Create/ShadowsOfDoubt/FirstPersonItem")]
    static void CreateAsset_FirstPersonItem() { SaveAsset(ScriptableObject.CreateInstance<FirstPersonItem>()); }
    [MenuItem("Assets/Create/ShadowsOfDoubt/FogPreset")]
    static void CreateAsset_FogPreset() { SaveAsset(ScriptableObject.CreateInstance<FogPreset>()); }
    [MenuItem("Assets/Create/ShadowsOfDoubt/FurnitureClass")]
    static void CreateAsset_FurnitureClass() { SaveAsset(ScriptableObject.CreateInstance<FurnitureClass>()); }
    [MenuItem("Assets/Create/ShadowsOfDoubt/FurnitureCluster")]
    static void CreateAsset_FurnitureCluster() { SaveAsset(ScriptableObject.CreateInstance<FurnitureCluster>()); }
    [MenuItem("Assets/Create/ShadowsOfDoubt/FurniturePreset")]
    static void CreateAsset_FurniturePreset() { SaveAsset(ScriptableObject.CreateInstance<FurniturePreset>()); }
    [MenuItem("Assets/Create/ShadowsOfDoubt/GroupPreset")]
    static void CreateAsset_GroupPreset() { SaveAsset(ScriptableObject.CreateInstance<GroupPreset>()); }
    [MenuItem("Assets/Create/ShadowsOfDoubt/HandwritingPreset")]
    static void CreateAsset_HandwritingPreset() { SaveAsset(ScriptableObject.CreateInstance<HandwritingPreset>()); }
    [MenuItem("Assets/Create/ShadowsOfDoubt/HelpContentPage")]
    static void CreateAsset_HelpContentPage() { SaveAsset(ScriptableObject.CreateInstance<HelpContentPage>()); }
    [MenuItem("Assets/Create/ShadowsOfDoubt/InteractableActionsPreset")]
    static void CreateAsset_InteractableActionsPreset() { SaveAsset(ScriptableObject.CreateInstance<InteractableActionsPreset>()); }
    [MenuItem("Assets/Create/ShadowsOfDoubt/InteractablePreset")]
    static void CreateAsset_InteractablePreset() { SaveAsset(ScriptableObject.CreateInstance<InteractablePreset>()); }
    [MenuItem("Assets/Create/ShadowsOfDoubt/ItemHolstered")]
    static void CreateAsset_ItemHolstered() { SaveAsset(ScriptableObject.CreateInstance<ItemHolstered>()); }
    [MenuItem("Assets/Create/ShadowsOfDoubt/JobPreset")]
    static void CreateAsset_JobPreset() { SaveAsset(ScriptableObject.CreateInstance<JobPreset>()); }
    [MenuItem("Assets/Create/ShadowsOfDoubt/LayoutConfiguration")]
    static void CreateAsset_LayoutConfiguration() { SaveAsset(ScriptableObject.CreateInstance<LayoutConfiguration>()); }
    [MenuItem("Assets/Create/ShadowsOfDoubt/LightingPreset")]
    static void CreateAsset_LightingPreset() { SaveAsset(ScriptableObject.CreateInstance<LightingPreset>()); }
    [MenuItem("Assets/Create/ShadowsOfDoubt/MatchPreset")]
    static void CreateAsset_MatchPreset() { SaveAsset(ScriptableObject.CreateInstance<MatchPreset>()); }
    [MenuItem("Assets/Create/ShadowsOfDoubt/MaterialGroupPreset")]
    static void CreateAsset_MaterialGroupPreset() { SaveAsset(ScriptableObject.CreateInstance<MaterialGroupPreset>()); }
    [MenuItem("Assets/Create/ShadowsOfDoubt/MenuPreset")]
    static void CreateAsset_MenuPreset() { SaveAsset(ScriptableObject.CreateInstance<MenuPreset>()); }
    [MenuItem("Assets/Create/ShadowsOfDoubt/MotivePreset")]
    static void CreateAsset_MotivePreset() { SaveAsset(ScriptableObject.CreateInstance<MotivePreset>()); }
    [MenuItem("Assets/Create/ShadowsOfDoubt/MurderMO")]
    static void CreateAsset_MurderMO() { SaveAsset(ScriptableObject.CreateInstance<MurderMO>()); }
    [MenuItem("Assets/Create/ShadowsOfDoubt/MurderPreset")]
    static void CreateAsset_MurderPreset() { SaveAsset(ScriptableObject.CreateInstance<MurderPreset>()); }
    [MenuItem("Assets/Create/ShadowsOfDoubt/MurderWeaponPreset")]
    static void CreateAsset_MurderWeaponPreset() { SaveAsset(ScriptableObject.CreateInstance<MurderWeaponPreset>()); }
    [MenuItem("Assets/Create/ShadowsOfDoubt/MurderWeaponsPool")]
    static void CreateAsset_MurderWeaponsPool() { SaveAsset(ScriptableObject.CreateInstance<MurderWeaponsPool>()); }
    [MenuItem("Assets/Create/ShadowsOfDoubt/MusicCue")]
    static void CreateAsset_MusicCue() { SaveAsset(ScriptableObject.CreateInstance<MusicCue>()); }
    [MenuItem("Assets/Create/ShadowsOfDoubt/NeonSignCharacters")]
    static void CreateAsset_NeonSignCharacters() { SaveAsset(ScriptableObject.CreateInstance<NeonSignCharacters>()); }
    [MenuItem("Assets/Create/ShadowsOfDoubt/NewspaperArticle")]
    static void CreateAsset_NewspaperArticle() { SaveAsset(ScriptableObject.CreateInstance<NewspaperArticle>()); }
    [MenuItem("Assets/Create/ShadowsOfDoubt/OccupationPreset")]
    static void CreateAsset_OccupationPreset() { SaveAsset(ScriptableObject.CreateInstance<OccupationPreset>()); }
    [MenuItem("Assets/Create/ShadowsOfDoubt/ParticleEffect")]
    static void CreateAsset_ParticleEffect() { SaveAsset(ScriptableObject.CreateInstance<ParticleEffect>()); }
    [MenuItem("Assets/Create/ShadowsOfDoubt/PhysicsProfile")]
    static void CreateAsset_PhysicsProfile() { SaveAsset(ScriptableObject.CreateInstance<PhysicsProfile>()); }
    [MenuItem("Assets/Create/ShadowsOfDoubt/PlayerTransitionPreset")]
    static void CreateAsset_PlayerTransitionPreset() { SaveAsset(ScriptableObject.CreateInstance<PlayerTransitionPreset>()); }
    [MenuItem("Assets/Create/ShadowsOfDoubt/ResidencePreset")]
    static void CreateAsset_ResidencePreset() { SaveAsset(ScriptableObject.CreateInstance<ResidencePreset>()); }
    [MenuItem("Assets/Create/ShadowsOfDoubt/RetailItemPreset")]
    static void CreateAsset_RetailItemPreset() { SaveAsset(ScriptableObject.CreateInstance<RetailItemPreset>()); }
    [MenuItem("Assets/Create/ShadowsOfDoubt/RevengeObjective")]
    static void CreateAsset_RevengeObjective() { SaveAsset(ScriptableObject.CreateInstance<RevengeObjective>()); }
    [MenuItem("Assets/Create/ShadowsOfDoubt/RoomClassPreset")]
    static void CreateAsset_RoomClassPreset() { SaveAsset(ScriptableObject.CreateInstance<RoomClassPreset>()); }
    [MenuItem("Assets/Create/ShadowsOfDoubt/RoomConfiguration")]
    static void CreateAsset_RoomConfiguration() { SaveAsset(ScriptableObject.CreateInstance<RoomConfiguration>()); }
    [MenuItem("Assets/Create/ShadowsOfDoubt/RoomLightingPreset")]
    static void CreateAsset_RoomLightingPreset() { SaveAsset(ScriptableObject.CreateInstance<RoomLightingPreset>()); }
    [MenuItem("Assets/Create/ShadowsOfDoubt/RoomTypeFilter")]
    static void CreateAsset_RoomTypeFilter() { SaveAsset(ScriptableObject.CreateInstance<RoomTypeFilter>()); }
    [MenuItem("Assets/Create/ShadowsOfDoubt/RoomTypePreset")]
    static void CreateAsset_RoomTypePreset() { SaveAsset(ScriptableObject.CreateInstance<RoomTypePreset>()); }
    [MenuItem("Assets/Create/ShadowsOfDoubt/ScriptableObjectIDSystem")]
    static void CreateAsset_ScriptableObjectIDSystem() { SaveAsset(ScriptableObject.CreateInstance<ScriptableObjectIDSystem>()); }
    [MenuItem("Assets/Create/ShadowsOfDoubt/SideMissionHandInPreset")]
    static void CreateAsset_SideMissionHandInPreset() { SaveAsset(ScriptableObject.CreateInstance<SideMissionHandInPreset>()); }
    [MenuItem("Assets/Create/ShadowsOfDoubt/SideMissionIntroPreset")]
    static void CreateAsset_SideMissionIntroPreset() { SaveAsset(ScriptableObject.CreateInstance<SideMissionIntroPreset>()); }
    [MenuItem("Assets/Create/ShadowsOfDoubt/SpatterPatternPreset")]
    static void CreateAsset_SpatterPatternPreset() { SaveAsset(ScriptableObject.CreateInstance<SpatterPatternPreset>()); }
    [MenuItem("Assets/Create/ShadowsOfDoubt/StairwellPreset")]
    static void CreateAsset_StairwellPreset() { SaveAsset(ScriptableObject.CreateInstance<StairwellPreset>()); }
    [MenuItem("Assets/Create/ShadowsOfDoubt/StatusPreset")]
    static void CreateAsset_StatusPreset() { SaveAsset(ScriptableObject.CreateInstance<StatusPreset>()); }
    [MenuItem("Assets/Create/ShadowsOfDoubt/StreetTilePreset")]
    static void CreateAsset_StreetTilePreset() { SaveAsset(ScriptableObject.CreateInstance<StreetTilePreset>()); }
    [MenuItem("Assets/Create/ShadowsOfDoubt/SubObjectClassPreset")]
    static void CreateAsset_SubObjectClassPreset() { SaveAsset(ScriptableObject.CreateInstance<SubObjectClassPreset>()); }
    [MenuItem("Assets/Create/ShadowsOfDoubt/SyncDiskPreset")]
    static void CreateAsset_SyncDiskPreset() { SaveAsset(ScriptableObject.CreateInstance<SyncDiskPreset>()); }
    [MenuItem("Assets/Create/ShadowsOfDoubt/WallFrontageClass")]
    static void CreateAsset_WallFrontageClass() { SaveAsset(ScriptableObject.CreateInstance<WallFrontageClass>()); }
    [MenuItem("Assets/Create/ShadowsOfDoubt/WallFrontagePreset")]
    static void CreateAsset_WallFrontagePreset() { SaveAsset(ScriptableObject.CreateInstance<WallFrontagePreset>()); }
    [MenuItem("Assets/Create/ShadowsOfDoubt/WindowStylePreset")]
    static void CreateAsset_WindowStylePreset() { SaveAsset(ScriptableObject.CreateInstance<WindowStylePreset>()); }
    [MenuItem("Assets/Create/ShadowsOfDoubt/WindowTabPreset")]
    static void CreateAsset_WindowTabPreset() { SaveAsset(ScriptableObject.CreateInstance<WindowTabPreset>()); }
}