using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using SpaceCraft;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace AdvancedCompass;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
public sealed class Plugin : BaseUnityPlugin
{
    public const string PluginGuid = "local.theplanetcrafter.advancedcompass";
    public const string PluginName = "Advanced Compass";
    public const string PluginVersion = "1.1.0";

    private static ManualLogSource Log;
    private static ConfigEntry<float> ScanRange;
    private static ConfigEntry<string> RequiredChipGroupId;
    private static ConfigEntry<float> MarkerSize;
    private static ConfigEntry<bool> ShowAllVeins;
    private static ConfigEntry<bool> ShowContainers;
    private static ConfigEntry<string> ContainerExtraGroupIds;
    private static ConfigEntry<bool> UseContainerIcons;
    private static ConfigEntry<bool> RequireInfoRocket;
    private static ConfigEntry<string> ContainerRocketGroupId;
    private static ConfigEntry<bool> RequireBlueprintUnlock;
    private static ConfigEntry<float> UpdateInterval;

    private static Plugin Instance;
    private static Harmony _harmony;

    private void Awake()
    {
        Instance = this;
        Log = Logger;

        ScanRange = Config.Bind("General", "ScanRange", 100f,
            "Max distance to show points on compass.");
        MarkerSize = Config.Bind("General", "MarkerSize", 48f,
            "Icon size multiplier (48 = default).");
        ShowAllVeins = Config.Bind("General", "ShowAllVeins", false,
            "Show all points without requiring a chip.");
        RequiredChipGroupId = Config.Bind("General", "RequiredChipGroupId", "HudCompassT2",
            "Group ID of the equipment chip that enables scanning.");

        ShowContainers = Config.Bind("Containers", "ShowContainers", true,
            "Show world loot containers on the compass.");
        ContainerExtraGroupIds = Config.Bind("Containers", "ContainerExtraGroupIds", "canister",
            "Comma-separated list of group IDs to show as containers (even if inventory size = 0).");
        UseContainerIcons = Config.Bind("Containers", "UseContainerIcons", true,
            "Show each container's group icon (true) or a question mark (false).");
        ContainerRocketGroupId = Config.Bind("Containers", "ContainerRocketGroupId", "RocketMap2",
            "Group ID of the rocket required to show container markers. Options: RocketMap3 (GPS Satellite T3), RocketMap2 (GPS Satellite T2).");

        RequireBlueprintUnlock = Config.Bind("Crafting", "RequireBlueprintUnlock", true,
            "When enabled: HudCompassT2 recipe is hidden until unlocked via blueprint chip (tier 2). When disabled: recipe is always visible at CraftStationT2.");
        RequireInfoRocket = Config.Bind("General", "RequireInfoRocket", true,
            "When enabled: tier 1+2 veins need MapInfoRocket, fish/harvester/toxic need MapInfoRocket T2, containers need GPS Satellite T3. When disabled: everything visible.");
        UpdateInterval = Config.Bind("General", "UpdateInterval", 0.5f,
            "How often to scan for nearby objects (seconds).");

        _harmony = Harmony.CreateAndPatchAll(typeof(Plugin), PluginGuid);
        Logger.LogInfo($"{PluginName} {PluginVersion} loaded");
    }

    private static void DebugLog(string message)
    {
        Log.LogInfo(message);
    }

    // ─── Custom item injection ─────────────────────────────────────────────

    private const string ChipGroupId = "HudCompassT2";

    [HarmonyPostfix]
    [HarmonyPatch(typeof(StaticDataHandler), "LoadStaticData")]
    private static void StaticDataHandler_LoadStaticData_Post()
    {
        if (GroupsHandler.GetGroupViaId(ChipGroupId) != null)
            return;

        try
        {
            InjectCompassProChip();
            DebugLog($"Injected craftable item '{ChipGroupId}' into groups");
        }
        catch (Exception ex)
        {
            DebugLog($"Failed to inject '{ChipGroupId}': {ex.Message}");
        }
    }

    private static void InjectCompassProChip()
    {
        var dataItem = ScriptableObject.CreateInstance<GroupDataItem>();
        dataItem.id = ChipGroupId;

        foreach (var item in GroupsHandler.GetGroupsItem())
        {
            if (item.GetEquipableType() == DataConfig.EquipableType.CompassHUD)
            {
                dataItem.icon = item.GetImage();
                dataItem.associatedGameObject = item.GetAssociatedGameObject();
                break;
            }
        }

        dataItem.equipableType = DataConfig.EquipableType.CompassHUD;
        dataItem.usableType = DataConfig.UsableType.Null;
        dataItem.craftableInList = new List<DataConfig.CraftableIn>
            { DataConfig.CraftableIn.CraftStationT2 };
        dataItem.itemCategory = DataConfig.ItemCategory.Equipment;
        dataItem.itemSubCategory = DataConfig.ItemSubCategory.Null;
        dataItem.value = 1;
        dataItem.unlockingWorldUnit = RequireBlueprintUnlock.Value
            ? DataConfig.WorldUnitType.Null
            : DataConfig.WorldUnitType.Terraformation;
        dataItem.unlockingValue = 0f;
        dataItem.terraformStageUnlock = null;
        dataItem.unlockInPlanets = new List<PlanetData>();
        dataItem.planetUsageType = DataConfig.GroupPlanetUsageType.CanBeUsedOnAllPlanets;
        dataItem.hideInCrafter = false;
        dataItem.tradeCategory = DataConfig.TradeCategory.Null;
        dataItem.tradeValue = 0;
        dataItem.inventorySize = 0;
        dataItem.secondaryInventoriesSize = null;
        dataItem.logisticInterplanetaryType = DataConfig.LogisticInterplanetaryType.Disabled;
        dataItem.lootRecipeOnDeconstruct = false;
        dataItem.chanceToSpawn = 0f;
        dataItem.cantBeDestroyed = false;
        dataItem.cantBeRecycled = false;
        dataItem.craftedInWorld = false;
        dataItem.canBePickedUpFromWorldByDrones = false;
        dataItem.displayInLogisticType = DataConfig.LogisticDisplayType.Display;
        dataItem.effectOnPlayer = null;
        dataItem.growableGroup = null;
        dataItem.unlocksGroup = null;

        var ingredientIds = new[] { "HudCompass", "Alloy", "Alloy", "Alloy", "Aluminium", "Aluminium" };
        var ingredientItems = new List<GroupDataItem>();
        foreach (var id in ingredientIds)
        {
            if (GroupsHandler.GetGroupViaId(id) != null)
            {
                var ing = ScriptableObject.CreateInstance<GroupDataItem>();
                ing.id = id;
                ingredientItems.Add(ing);
            }
        }

        var groupItem = new GroupItem(dataItem);
        if (RequireBlueprintUnlock.Value)
        {
            groupItem.GetUnlockingInfos().SetIsUnlockedViaBlueprint(true);
        }
        if (ingredientItems.Count > 0)
        {
            groupItem.SetRecipe(new Recipe(ingredientItems));
        }

        var allGroupsField = typeof(GroupsHandler).GetField("_allGroups",
            BindingFlags.Static | BindingFlags.NonPublic);
        var allGroupsByIdField = typeof(GroupsHandler).GetField("_allGroupsById",
            BindingFlags.Static | BindingFlags.NonPublic);
        var allGroupsByHashField = typeof(GroupsHandler).GetField("_allGroupsByHash",
            BindingFlags.Static | BindingFlags.NonPublic);
        var groupsItemField = typeof(GroupsHandler).GetField("_groupsItem",
            BindingFlags.Static | BindingFlags.NonPublic);

        var allGroups = allGroupsField?.GetValue(null) as List<Group>;
        var allGroupsById = allGroupsByIdField?.GetValue(null) as Dictionary<string, Group>;
        var allGroupsByHash = allGroupsByHashField?.GetValue(null) as Dictionary<int, Group>;
        var groupsItem = groupsItemField?.GetValue(null) as List<GroupItem>;

        int compassGroupIndex = FindGroupIndex<Group>(allGroups, "HudCompass");
        if (allGroups != null)
        {
            if (compassGroupIndex >= 0)
                allGroups.Insert(compassGroupIndex + 1, groupItem);
            else
                allGroups.Add(groupItem);
        }
        if (allGroupsById != null) allGroupsById[groupItem.GetId()] = groupItem;
        if (allGroupsByHash != null) allGroupsByHash[groupItem.stableHashCode] = groupItem;

        int compassItemIndex = FindGroupIndex<GroupItem>(groupsItem, "HudCompass");
        if (groupsItem != null)
        {
            if (compassItemIndex >= 0)
                groupsItem.Insert(compassItemIndex + 1, groupItem);
            else
                groupsItem.Add(groupItem);
        }

        InjectLocalization();
    }

    private static int FindGroupIndex<T>(List<T> list, string id) where T : Group
    {
        if (list == null)
            return -1;
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i]?.GetId() == id)
                return i;
        }
        return -1;
    }

    // ─── Blueprint tier registration (postfix on private GetUnlockableGroup for tier2) ─

    [HarmonyPostfix]
    [HarmonyPatch(typeof(UnlockingHandler), "GetUnlockableGroup", new Type[] { typeof(List<GroupData>) })]
    private static void UnlockingHandler_GetUnlockableGroupTier_Post(
        ref Group __result, List<GroupData> referenceList, UnlockingHandler __instance)
    {
        if (__result != null)
            return;
        if (!RequireBlueprintUnlock.Value)
            return;

        if (referenceList == __instance.unlockingData?.tier2GroupToUnlock)
        {
            var group = GroupsHandler.GetGroupViaId(ChipGroupId);
            if (group != null && UnlockedGroupsHandler.Instance != null
                && !UnlockedGroupsHandler.Instance.IsGloballyUnlocked(group))
            {
                __result = group;
            }
        }
    }

    private static void InjectLocalization()
    {
        try
        {
            var locDictField = typeof(Localization).GetField("localizationDictionary",
                BindingFlags.Static | BindingFlags.NonPublic);
            if (locDictField?.GetValue(null) is Dictionary<string, Dictionary<string, string>> dict)
            {
                string nameKey = "GROUP_NAME_" + ChipGroupId;
                SetLocalizedString(dict, "english", nameKey, "Microchip - Compass T2");
                SetLocalizedString(dict, "russian", nameKey, "Микрочип - Компасс Т2");

                string descKey = "GROUP_DESC_" + ChipGroupId;
                SetLocalizedString(dict, "english", descKey, "Adds a compass to your screen\nDisplays additional information");
                SetLocalizedString(dict, "russian", descKey, "Добавляет компас на ваш экран\nОтображает дополнительную информацию");
            }
        }
        catch { }
    }

    private static void SetLocalizedString(
        Dictionary<string, Dictionary<string, string>> dict,
        string language, string key, string text)
    {
        if (dict.TryGetValue(language, out var langDict))
            langDict[key] = text;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(PlayerEquipment), "InitEquipment")]
    private static void PlayerEquipment_InitEquipment_Post(PlayerEquipment __instance)
    {
        if (GroupsHandler.GetGroupViaId(ChipGroupId) is GroupItem chip)
        {
            __instance.GetInventory().AddAuthorizedGroup(chip);
        }
    }

    // ─── Equipment chip detection ──────────────────────────────────────────

    private static readonly Dictionary<int, float> ChipCache = new();
    private static float _lastChipCheck;

    private static bool HasScannerChip()
    {
        if (ShowAllVeins.Value)
            return true;

        float now = Time.time;
        if (now - _lastChipCheck < 3f && ChipCache.Count > 0)
            return ChipCache.Values.FirstOrDefault() > 0f;

        _lastChipCheck = now;
        ChipCache.Clear();

        try
        {
            var players = Managers.GetManager<PlayersManager>();
            var active = players?.GetActivePlayerController();
            var equip = active?.GetPlayerEquipment()?.GetInventory();
            if (equip == null)
                return false;

            foreach (WorldObject wo in equip.GetInsideWorldObjects())
            {
                if (wo?.GetGroup()?.GetId() == RequiredChipGroupId.Value)
                {
                    ChipCache[wo.GetId()] = Time.time;
                    return true;
                }
            }
        }
        catch { }

        return false;
    }

    // ─── Compass marker management ─────────────────────────────────────────

    private class VeinMarker
    {
        public GameObject go;
        public Image image;
        public MachineGenerationGroupVein vein;
    }

    private class ContainerMarker
    {
        public GameObject go;
        public Image image;
        public TextMeshProUGUI questionMark;
        public int woId;
    }

    private static readonly List<MachineGenerationGroupVein> CachedVeins = new();
    private static float _lastVeinScan;
    private static readonly Dictionary<MachineGenerationGroupVein, VeinMarker> ActiveVeinMarkers = new();

    private static readonly List<WorldObject> CachedContainers = new();
    private static float _lastContainerScan;
    private static readonly Dictionary<int, ContainerMarker> ActiveContainerMarkers = new();

    private static Sprite _blueprintChipSprite;
    private static bool _blueprintChipSearched;

    private static RectTransform _compassRect;
    private static float _compassWidth;

    private static readonly HashSet<string> ExtraContainerIds = new(StringComparer.OrdinalIgnoreCase);
    private static float _lastExtraIdsRefresh;

    private static void RefreshExtraContainerIds()
    {
        float now = Time.time;
        if (now - _lastExtraIdsRefresh < 10f)
            return;
        _lastExtraIdsRefresh = now;

        ExtraContainerIds.Clear();
        foreach (var id in ContainerExtraGroupIds.Value.Split(','))
        {
            var trimmed = id.Trim();
            if (!string.IsNullOrEmpty(trimmed))
                ExtraContainerIds.Add(trimmed);
        }
    }

    private static bool IsVeinAllowedByProgression(DataConfig.OreVeinIdentifer type)
    {
        if (!RequireInfoRocket.Value)
            return true;

        string requiredGroup = type switch
        {
            DataConfig.OreVeinIdentifer.tier1 or DataConfig.OreVeinIdentifer.tier2 => "RocketInformations1",
            DataConfig.OreVeinIdentifer.fish1 or DataConfig.OreVeinIdentifer.harvester or DataConfig.OreVeinIdentifer.toxic1 => "RocketInformations2",
            _ => null
        };

        if (string.IsNullOrEmpty(requiredGroup))
            return true;

        return CheckRocketPlaced(requiredGroup);
    }

    private static bool IsContainerAllowedByProgression()
    {
        if (!RequireInfoRocket.Value)
            return true;

        return CheckRocketPlaced(ContainerRocketGroupId.Value);
    }

    private static readonly Dictionary<string, (bool result, float time)> RocketCache = new();
    private const float RocketCacheTtl = 3f;

    private static bool CheckRocketPlaced(string rocketGroupId)
    {
        float now = Time.time;
        if (RocketCache.TryGetValue(rocketGroupId, out var entry) && now - entry.time < RocketCacheTtl)
            return entry.result;

        try
        {
            var group = GroupsHandler.GetGroupViaId(rocketGroupId);
            if (group == null)
            {
                RocketCache[rocketGroupId] = (false, now);
                return false;
            }
            bool placed = WorldObjectsHandler.Instance.HasObjectInWorldObjects(group, false);
            RocketCache[rocketGroupId] = (placed, now);
            return placed;
        }
        catch
        {
            RocketCache[rocketGroupId] = (false, now);
            return false;
        }
    }

    private static void RefreshVeinCache()
    {
        float now = Time.time;
        if (now - _lastVeinScan < UpdateInterval.Value)
            return;
        _lastVeinScan = now;

        CachedVeins.Clear();
        var found = UnityEngine.Object.FindObjectsByType<MachineGenerationGroupVein>(FindObjectsSortMode.None);
        if (found != null && found.Length > 0)
            CachedVeins.AddRange(found);
    }

    private static void RefreshContainerCache()
    {
        float now = Time.time;
        if (now - _lastContainerScan < UpdateInterval.Value)
            return;
        _lastContainerScan = now;
        RefreshExtraContainerIds();

        CachedContainers.Clear();
        try
        {
            var handler = WorldObjectsHandler.Instance;
            if (handler == null || !handler.GetHasLoadedData())
                return;

            var allWo = handler.GetAllWorldObjects();
            foreach (var kvp in allWo)
            {
                var wo = kvp.Value;
                if (wo == null) continue;

                var group = wo.GetGroup();
                if (group == null) continue;

                if (!wo.GetIsPlaced()) continue;

                string gid = group.GetId();
                bool isExtra = ExtraContainerIds.Contains(gid);
                if (isExtra)
                {
                    CachedContainers.Add(wo);
                    continue;
                }

                if (!WorldObjectsIdHandler.IsWorldObjectFromScene(wo.GetId())) continue;

                bool hasInventory = group.GetInventorySize() > 0;
                bool hasSceneLoot = !hasInventory && wo.GetGameObject() != null
                    && wo.GetGameObject().GetComponent<InventoryFromScene>() != null;

                if (hasInventory || hasSceneLoot)
                {
                    CachedContainers.Add(wo);
                }
            }
        }
        catch { }
    }

    private static void EnsureCompassRefs(CanvasCompass compass)
    {
        if (_compassRect != null)
            return;

        _compassRect = compass.compass.rectTransform;
        _compassWidth = _compassRect.rect.width;
    }

    private static void ClearMarkers()
    {
        foreach (var m in ActiveVeinMarkers.Values)
            UnityEngine.Object.Destroy(m.go);
        ActiveVeinMarkers.Clear();

        foreach (var m in ActiveContainerMarkers.Values)
            UnityEngine.Object.Destroy(m.go);
        ActiveContainerMarkers.Clear();
    }

    private static Image CreateMarkerIcon(Sprite sprite)
    {
        var go = new GameObject("Marker");
        go.transform.SetParent(_compassRect, false);

        var image = go.AddComponent<Image>();
        image.sprite = sprite;
        image.raycastTarget = false;
        image.preserveAspect = true;
        image.color = Color.white;

        var rt = go.GetComponent<RectTransform>();
        float compassHeight = _compassRect.rect.height;
        float markerSize = Mathf.Max(compassHeight * 1.5f, 40f) * (MarkerSize.Value / 48f);
        rt.sizeDelta = new Vector2(markerSize, markerSize);
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);

        return image;
    }

    private static Image CreateFallbackMarker()
    {
        var go = new GameObject("Marker");
        go.transform.SetParent(_compassRect, false);

        var image = go.AddComponent<Image>();
        image.raycastTarget = false;
        image.color = Color.yellow;

        var rt = go.GetComponent<RectTransform>();
        float compassHeight = _compassRect.rect.height;
        float markerSize = Mathf.Max(compassHeight * 1.5f, 40f) * (MarkerSize.Value / 48f);
        rt.sizeDelta = new Vector2(markerSize, markerSize);
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);

        return image;
    }

    private static void PositionMarker(RectTransform rt, Vector3 playerPos, Vector3 targetPos, float cameraAngleY)
    {
        Vector3 toTarget = targetPos - playerPos;
        toTarget.y = 0f;

        if (toTarget.sqrMagnitude < 0.01f)
            return;

        float angleToTarget = Mathf.Atan2(toTarget.x, toTarget.z) * Mathf.Rad2Deg;
        float angleDiff = Mathf.DeltaAngle(cameraAngleY, angleToTarget);
        float fraction = angleDiff / 360f;

        float xPos = fraction * _compassWidth;
        rt.anchoredPosition = new Vector2(xPos, 0f);
    }

    private static Sprite GetBlueprintChipSprite()
    {
        if (_blueprintChipSprite != null)
            return _blueprintChipSprite;

        if (_blueprintChipSearched)
            return null;

        _blueprintChipSearched = true;
        try
        {
            var group = GroupsHandler.GetGroupViaId("BlueprintT1");
            if (group != null)
                _blueprintChipSprite = group.GetImage();
        }
        catch { }

        return _blueprintChipSprite;
    }

    // ─── Harmony patches ───────────────────────────────────────────────────

    [HarmonyPostfix]
    [HarmonyPatch(typeof(CanvasCompass), "Update")]
    private static void CanvasCompass_Update_Post(CanvasCompass __instance)
    {
        if (!__instance.enabled || !__instance.compass.gameObject.activeInHierarchy)
        {
            ClearMarkers();
            return;
        }

        if (!HasScannerChip())
        {
            ClearMarkers();
            return;
        }

        EnsureCompassRefs(__instance);

        try
        {
            var camera = Camera.main;
            if (camera == null)
            {
                ClearMarkers();
                return;
            }

            Vector3 playerPos = camera.transform.position;
            float cameraAngleY = camera.transform.eulerAngles.y;
            float rangeSq = ScanRange.Value * ScanRange.Value;

            UpdateVeinMarkers(playerPos, cameraAngleY, rangeSq);
            if (ShowContainers.Value)
                UpdateContainerMarkers(playerPos, cameraAngleY, rangeSq);
        }
        catch (Exception ex)
        {
            DebugLog($"Compass scan error: {ex.Message}");
        }
    }

    private static void UpdateVeinMarkers(Vector3 playerPos, float cameraAngleY, float rangeSq)
    {
        RefreshVeinCache();

        if (CachedVeins.Count == 0)
        {
            foreach (var m in ActiveVeinMarkers.Values)
                m.go.SetActive(false);
            return;
        }

        var keep = new HashSet<MachineGenerationGroupVein>();

        foreach (var vein in CachedVeins)
        {
            if (vein == null) continue;
            if (!IsVeinAllowedByProgression(vein.oreVeinIdentifer)) continue;

            Vector3 veinPos = vein.transform.position;
            Vector3 diff = veinPos - playerPos;
            diff.y = 0f;

            if (diff.sqrMagnitude > rangeSq || diff.sqrMagnitude < 0.01f)
                continue;

            keep.Add(vein);

            if (!ActiveVeinMarkers.TryGetValue(vein, out var marker))
            {
                var sprite = vein.groups != null && vein.groups.Count > 0
                    ? vein.groups[0].icon
                    : null;
                if (sprite == null) continue;

                var image = CreateMarkerIcon(sprite);
                marker = new VeinMarker { go = image.gameObject, image = image, vein = vein };
                ActiveVeinMarkers[vein] = marker;
            }

            marker.go.SetActive(true);
            PositionMarker(marker.image.rectTransform, playerPos, veinPos, cameraAngleY);
        }

        foreach (var kvp in ActiveVeinMarkers)
        {
            if (!keep.Contains(kvp.Key))
                kvp.Value.go.SetActive(false);
        }
    }

    private static void UpdateContainerMarkers(Vector3 playerPos, float cameraAngleY, float rangeSq)
    {
        if (!IsContainerAllowedByProgression())
        {
            foreach (var m in ActiveContainerMarkers.Values)
                m.go.SetActive(false);
            return;
        }

        RefreshContainerCache();

        if (CachedContainers.Count == 0)
        {
            foreach (var m in ActiveContainerMarkers.Values)
                m.go.SetActive(false);
            return;
        }

        var keep = new HashSet<int>();

        foreach (var wo in CachedContainers)
        {
            if (wo == null || !wo.GetIsPlaced())
                continue;

            Vector3 containerPos = wo.GetPosition();
            Vector3 diff = containerPos - playerPos;
            diff.y = 0f;

            float distSq = diff.sqrMagnitude;
            if (distSq > rangeSq || distSq < 0.01f)
                continue;

            int woId = wo.GetId();
            keep.Add(woId);

            if (!ActiveContainerMarkers.TryGetValue(woId, out var marker))
            {
                if (UseContainerIcons.Value)
                {
                    var sprite = wo.GetGroup()?.GetImage();
                    if (sprite == null)
                        sprite = GetBlueprintChipSprite();

                    if (sprite != null)
                    {
                        var image = CreateMarkerIcon(sprite);
                        marker = new ContainerMarker { go = image.gameObject, image = image, woId = woId };
                    }
                    else
                    {
                        var image = CreateFallbackMarker();
                        marker = new ContainerMarker { go = image.gameObject, image = image, woId = woId };
                    }
                }
                else
                {
                    var textGo = new GameObject("MarkerQ");
                    textGo.transform.SetParent(_compassRect, false);

                    var tmp = textGo.AddComponent<TextMeshProUGUI>();
                    tmp.text = "?";
                    tmp.fontSize = MarkerSize.Value * 1.2f;
                    tmp.alignment = TextAlignmentOptions.Center;
                    tmp.color = Color.white;
                    tmp.raycastTarget = false;

                    var rt = textGo.GetComponent<RectTransform>();
                    float compassHeight = _compassRect.rect.height;
                    float markerSize = Mathf.Max(compassHeight * 1.5f, 40f) * (MarkerSize.Value / 48f);
                    rt.sizeDelta = new Vector2(markerSize, markerSize);
                    rt.anchorMin = new Vector2(0.5f, 0.5f);
                    rt.anchorMax = new Vector2(0.5f, 0.5f);
                    rt.pivot = new Vector2(0.5f, 0.5f);

                    marker = new ContainerMarker { go = textGo, questionMark = tmp, woId = woId };
                }

                ActiveContainerMarkers[woId] = marker;
            }

            marker.go.SetActive(true);
            PositionMarker(marker.go.GetComponent<RectTransform>(), playerPos, containerPos, cameraAngleY);
        }

        foreach (var kvp in ActiveContainerMarkers)
        {
            if (!keep.Contains(kvp.Key))
                kvp.Value.go.SetActive(false);
        }
    }
}

public sealed class ConfigurationManagerAttributes
{
    public bool? Browsable;
}
