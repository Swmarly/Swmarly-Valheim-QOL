using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace SwmarlyValheimQOL;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
public sealed class Plugin : BaseUnityPlugin
{
    public const string PluginGuid = "Swmarly.ValheimQOL";
    public const string PluginName = "Swmarly Valheim QOL";
    public const string PluginVersion = "1.0.3";
    internal static Plugin Instance;
    internal static readonly Harmony Harmony = new(PluginGuid);

    internal static ConfigEntry<bool> FloatItems;
    internal static ConfigEntry<bool> EquipmentInWater;
    internal static ConfigEntry<bool> EquipWhileRunning;
    internal static ConfigEntry<bool> SleepSkip;
    internal static ConfigEntry<bool> CurrencyPocket;
    internal static ConfigEntry<bool> SwimImprovements;
    internal static ConfigEntry<bool> Diving;
    internal static ConfigEntry<bool> SneakSpeed;
    internal static ConfigEntry<bool> SitRegeneration;
    internal static ConfigEntry<bool> FleeOnSight;
    internal static ConfigEntry<bool> NoRainDamage;
    internal static ConfigEntry<bool> NoStaminaCosts;
    internal static ConfigEntry<bool> MultiUserChests;
    internal static ConfigEntry<bool> SpeedyPaths;
    internal static ConfigEntry<bool> NoStaminaOnPaths;
    internal static ConfigEntry<bool> NoAfkRaids;
    internal static ConfigEntry<bool> EternalFires;
    internal static ConfigEntry<bool> AutoReplantTrees;
    internal static ConfigEntry<bool> AutoRepairAtWorkbench;

    internal static ConfigEntry<float> FloatForce;
    internal static ConfigEntry<float> FloatDamping;
    internal static ConfigEntry<float> MaxSwimSpeedMultiplier;
    internal static ConfigEntry<float> SwimIdleStaminaPerSecond;
    internal static ConfigEntry<float> SitHealPerSecond;
    internal static ConfigEntry<float> SneakSpeedMultiplier;
    internal static ConfigEntry<float> DiveSpeed;
    internal static ConfigEntry<float> DiveStaminaPerSecond;
    internal static ConfigEntry<bool> SwimSprint;
    internal static ConfigEntry<KeyboardShortcut> DiveKey;
    internal static ConfigEntry<KeyboardShortcut> SurfaceKey;
    internal static ConfigEntry<string> FleeMobNames;
    internal static ConfigEntry<int> StaminaCostMode;
    internal static ConfigEntry<float> PathSensorInterval;
    internal static ConfigEntry<float> DirtPathSpeed;
    internal static ConfigEntry<float> StonePathSpeed;
    internal static ConfigEntry<float> CultivatedSpeed;
    internal static ConfigEntry<float> WoodPathSpeed;
    internal static ConfigEntry<float> StoneStructureSpeed;
    internal static ConfigEntry<float> DirtPathStamina;
    internal static ConfigEntry<float> StonePathStamina;
    internal static ConfigEntry<float> CultivatedStamina;
    internal static ConfigEntry<float> StructurePathStamina;
    internal static ConfigEntry<float> AfkMinutes;
    internal static ConfigEntry<float> AfkMovementThreshold;
    internal static ConfigEntry<float> AfkProtectionRadius;
    internal static ConfigEntry<bool> BlockForcedRaids;
    internal static ConfigEntry<string> EternalFirePrefabs;
    internal static ConfigEntry<string> TreeReplantMappings;
    internal static ConfigEntry<float> TreeReplantDelaySeconds;

    internal static ConfigEntry<int> SleepPercent;
    internal static ConfigEntry<int> SleepPlayersNeeded;
    internal static ConfigEntry<int> SleepWarningSeconds;
    internal static ConfigEntry<int> SleepVoteTimeoutSeconds;
    internal static ConfigEntry<int> SleepCooldownSeconds;
    internal static ConfigEntry<bool> SleepAutoAccept;

    internal const string CoinKey = "SwmarlyValheimQOL_Coins";
    internal const string LegacyCoinKey = "CoinPocket_CoinCount";
    internal const string CoinMigrationKey = "SwmarlyValheimQOL_CoinMigrationComplete";
    internal const string CoinConsumedKey = "SwmarlyValheimQOL_CoinDropConsumed";
    internal const string CoinPrefab = "Coins";
    internal const string CoinToken = "$item_coins";
    internal const string PocketUiName = "SwmarlyValheimQOL_CurrencyPocket";
    internal const string LegacyPocketUiName = "CoinPocketUI";
    internal const string PocketButtonRowName = "SwmarlyValheimQOL_CoinButtons";
    internal static int LastPocketValue;
    internal static GameObject PocketUi;
    internal static Button PocketExtractButton;
    internal static Button PocketDepositButton;
    internal static TextMeshProUGUI PocketText;
    internal static Coroutine PocketUiRepositionCoroutine;

    internal static readonly HashSet<long> SleepYes = new();
    internal static readonly HashSet<long> SleepNo = new();
    internal static readonly HashSet<long> SleepPopupSent = new();
    internal static DateTime SleepVoteStarted = DateTime.MinValue;
    internal static DateTime LastSleepCompleted = DateTime.MinValue;
    internal static int SleepInBed;
    internal static int SleepYesCount;
    internal static int SleepNoCount;
    internal static int SleepWaiting;
    internal static int SleepTotal;
    internal static bool SleepVoteActive;
    internal static bool SleepPopupOpen;
    internal static string LastSleepDisplay;
    internal static DateTime SleepWarningStarted = DateTime.MinValue;
    internal static int SleepLastWarning = -1;

    private void Awake()
    {
        Instance = this;
        BindConfig();
        // Persist the file before Harmony touches any client-only UI methods.
        // This guarantees a config is created on a headless dedicated server
        // even when another server plugin changes the available UI surface.
        Config.Save();
        ApplyHarmonyPatches();
        Logger.LogInfo($"{PluginName} {PluginVersion} loaded for Valheim 1.0 in process '{Process.GetCurrentProcess().ProcessName}'.");
    }

    private void ApplyHarmonyPatches()
    {
        int patchedTypes = 0;
        foreach (Type type in AccessTools.GetTypesFromAssembly(typeof(Plugin).Assembly))
        {
            if (type.GetCustomAttributes(typeof(HarmonyPatch), false).Length == 0) continue;

            try
            {
                Harmony.CreateClassProcessor(type).Patch();
                patchedTypes++;
            }
            catch (Exception exception)
            {
                // One stale target must not prevent unrelated QOL features
                // from registering. The exact patch class and exception stay
                // visible in LogOutput.log for version-specific follow-up.
                Logger.LogError($"Harmony patch class '{type.FullName}' failed: {exception}");
            }
        }

        Logger.LogInfo($"Harmony registered {patchedTypes} QOL patch classes.");
    }

    internal static void LogWarning(string message)
    {
        Instance?.Logger.LogWarning(message);
    }

    private void BindConfig()
    {
        FloatItems = Config.Bind("Features", "Float dropped items", true, "Make dropped items use Valheim's native floating physics in water.");
        EquipmentInWater = Config.Bind("Features", "Use equipment while swimming", true, "Allow equipment and tools to be equipped while swimming.");
        EquipWhileRunning = Config.Bind("Features", "Equip hotbar items while running", true, "Allow hotbar equipment changes while running.");
        SleepSkip = Config.Bind("Features", "Sleep skip voting", true, "Allow a configurable percentage of players to skip the night.");
        CurrencyPocket = Config.Bind("Features", "Currency pocket", true, "Automatically store picked-up coins in a pocket shown in the inventory.");
        SwimImprovements = Config.Bind("Features", "Swimming improvements", true, "Scale swimming speed and add optional idle stamina regeneration.");
        Diving = Config.Bind("Features", "Diving", true, "Allow controlled diving and surfacing while swimming.");
        SneakSpeed = Config.Bind("Features", "Sneak speed scaling", true, "Scale sneak speed with the Sneak skill.");
        SitRegeneration = Config.Bind("Features", "Regenerate while sitting", true, "Regenerate a small amount of health while sitting.");
        FleeOnSight = Config.Bind("Features", "Trash mobs flee on sight", true, "Make configured low-tier mobs flee instead of attacking on sight.");
        NoRainDamage = Config.Bind("Features", "No rain damage", true, "Prevent uncovered structures from taking rain wear while preserving support wear.");
        NoStaminaCosts = Config.Bind("Features", "No stamina costs", true, "Remove stamina costs from building tools by default.");
        MultiUserChests = Config.Bind("Features", "Allow multiple users in chests", true, "Remove the vanilla chest-in-use block so multiple players can open a chest together.");
        SpeedyPaths = Config.Bind("Features", "Speedy paths", true, "Apply SpeedyPaths-style movement bonuses to paths and common building surfaces.");
        NoStaminaOnPaths = Config.Bind("Features", "No stamina on paths", true, "Remove running stamina drain while standing on a dirt or stone path.");
        NoAfkRaids = Config.Bind("Features", "No AFK raids", true, "Prevent random raids while all connected players have been stationary for the configured AFK period.");
        EternalFires = Config.Bind("Features", "Eternal fires and lights", true, "Keep configured fireplaces and lights at full fuel without refueling.");
        AutoReplantTrees = Config.Bind("Features", "Automatically replant trees", true, "Plant the matching sapling after a supported tree stump is destroyed.");
        AutoRepairAtWorkbench = Config.Bind("Features", "Auto repair at workbenches", true, "Repair every item the opened crafting station can repair.");

        FloatForce = Config.Bind("Floating items", "Buoyancy force", 0.5f, new ConfigDescription("Native Floating force applied below the surface.", new AcceptableValueRange<float>(0.05f, 3f)));
        FloatDamping = Config.Bind("Floating items", "Damping", 0.05f, new ConfigDescription("Velocity damping while an item is floating.", new AcceptableValueRange<float>(0f, 0.5f)));
        EternalFirePrefabs = Config.Bind("Eternal fires and lights", "Prefab names", "fire_pit,fire_pit_iron,fire_pit_hildir,bonfire,hearth,piece_walltorch,piece_groundtorch,piece_groundtorch_wood,piece_groundtorch_green,piece_groundtorch_blue,piece_brazierfloor01,piece_brazierfloor02,piece_brazierceiling01,piece_jackoturnip", "Comma-separated Fireplace prefab names that should never run out of fuel. Add compatible modded Fireplace prefab names here.");

        TreeReplantMappings = Config.Bind("Automatic tree replanting", "Stump to sapling mappings", "Beech_Stub=Beech_Sapling,Beech1_Stub=Beech_Sapling,FirTree_Stub=FirTree_Sapling,Pinetree_01_Stub=PineTree_Sapling,BirchStub=Birch_Sapling,OakStub=Oak_Sapling", "Comma-separated stump=sapling mappings. Only mapped stumps are replanted; this prevents the wrong tree type from being created.");
        TreeReplantDelaySeconds = Config.Bind("Automatic tree replanting", "Replant delay seconds", 2.5f, new ConfigDescription("Server-side delay before the matching sapling is spawned.", new AcceptableValueRange<float>(0f, 60f)));

        MaxSwimSpeedMultiplier = Config.Bind("Swimming", "Maximum swim speed multiplier", 1.5f, new ConfigDescription("Multiplier at 100 Swim skill.", new AcceptableValueRange<float>(0.5f, 3f)));
        SwimIdleStaminaPerSecond = Config.Bind("Swimming", "Idle stamina regeneration per second", 2f, new ConfigDescription("Stamina restored while swimming nearly still.", new AcceptableValueRange<float>(0f, 20f)));
        SwimSprint = Config.Bind("Swimming", "Allow swim sprint", true, "Allow sprint input to increase swimming speed.");
        DiveSpeed = Config.Bind("Swimming", "Dive speed", 3f, new ConfigDescription("Vertical dive/surface speed in metres per second.", new AcceptableValueRange<float>(0.5f, 10f)));
        DiveStaminaPerSecond = Config.Bind("Swimming", "Dive stamina drain per second", 2f, new ConfigDescription("Stamina consumed while actively diving.", new AcceptableValueRange<float>(0f, 20f)));
        DiveKey = Config.Bind("Swimming", "Dive key", new KeyboardShortcut(KeyCode.LeftControl), "Hold this key while swimming to dive.");
        SurfaceKey = Config.Bind("Swimming", "Surface key", new KeyboardShortcut(KeyCode.Space), "Hold this key while swimming to surface.");

        SneakSpeedMultiplier = Config.Bind("Sneak", "Maximum sneak speed multiplier", 1.5f, new ConfigDescription("Multiplier at 100 Sneak skill.", new AcceptableValueRange<float>(0.5f, 3f)));
        SitHealPerSecond = Config.Bind("Sitting regeneration", "Health per second", 1f, new ConfigDescription("Health restored per second while sitting.", new AcceptableValueRange<float>(0f, 20f)));
        FleeMobNames = Config.Bind("Flee on sight", "Mob name fragments", "Greyling,Neck,Greydwarf", "Comma-separated prefab/name fragments that should flee on sight.");
        StaminaCostMode = Config.Bind("No stamina costs", "Mode", 1, new ConfigDescription("0 = disabled, 1 = hammer/hoe/cultivator, 2 = all stamina actions.", new AcceptableValueRange<int>(0, 2)));
        PathSensorInterval = Config.Bind("Speedy paths", "Ground sensor interval", 0.25f, new ConfigDescription("Seconds between ground-material checks for the local player.", new AcceptableValueRange<float>(0.05f, 2f)));
        DirtPathSpeed = Config.Bind("Speedy paths", "Dirt path speed", 1.15f, new ConfigDescription("Movement multiplier on dirt paths.", new AcceptableValueRange<float>(0.1f, 3f)));
        StonePathSpeed = Config.Bind("Speedy paths", "Stone path speed", 1.4f, new ConfigDescription("Movement multiplier on stone paths.", new AcceptableValueRange<float>(0.1f, 3f)));
        CultivatedSpeed = Config.Bind("Speedy paths", "Cultivated ground speed", 1f, new ConfigDescription("Movement multiplier on cultivated ground.", new AcceptableValueRange<float>(0.1f, 3f)));
        WoodPathSpeed = Config.Bind("Speedy paths", "Wood structure speed", 1.15f, new ConfigDescription("Movement multiplier on wood structures.", new AcceptableValueRange<float>(0.1f, 3f)));
        StoneStructureSpeed = Config.Bind("Speedy paths", "Stone structure speed", 1.4f, new ConfigDescription("Movement multiplier on stone/iron/marble structures.", new AcceptableValueRange<float>(0.1f, 3f)));
        // These are the SpeedyPaths defaults. The separate NoStaminaOnPaths
        // feature overrides them to zero when enabled; retaining the native
        // reference values here makes disabling that toggle actually restore
        // normal path stamina instead of leaving it free forever.
        DirtPathStamina = Config.Bind("Speedy paths", "Dirt path stamina multiplier", 0.8f, new ConfigDescription("Running stamina multiplier on dirt paths. 0 means no stamina usage.", new AcceptableValueRange<float>(0f, 2f)));
        StonePathStamina = Config.Bind("Speedy paths", "Stone path stamina multiplier", 0.7f, new ConfigDescription("Running stamina multiplier on stone paths. 0 means no stamina usage.", new AcceptableValueRange<float>(0f, 2f)));
        CultivatedStamina = Config.Bind("Speedy paths", "Cultivated ground stamina multiplier", 1f, new ConfigDescription("Running stamina multiplier on cultivated ground.", new AcceptableValueRange<float>(0f, 2f)));
        StructurePathStamina = Config.Bind("Speedy paths", "Structure stamina multiplier", 0.8f, new ConfigDescription("Running stamina multiplier on supported structures. 0 means no stamina usage.", new AcceptableValueRange<float>(0f, 2f)));
        // NoAFKRaids' current reference default is three minutes. Keep that
        // behavior instead of making a newly-created config silently wait ten
        // minutes before protecting an idle player.
        AfkMinutes = Config.Bind("No AFK raids", "AFK minutes", 3f, new ConfigDescription("Minutes without meaningful movement before a player is treated as AFK.", new AcceptableValueRange<float>(0.1f, 240f)));
        AfkMovementThreshold = Config.Bind("No AFK raids", "Movement threshold", 0.1f, new ConfigDescription("Minimum movement in metres that resets AFK detection.", new AcceptableValueRange<float>(0.01f, 5f)));
        AfkProtectionRadius = Config.Bind("No AFK raids", "Protection radius", 200f, new ConfigDescription("Radius used when locating a player near a random event. Set to 0 to protect the whole world.", new AcceptableValueRange<float>(0f, 1000f)));
        BlockForcedRaids = Config.Bind("No AFK raids", "Block forced raids", false, "Also block explicitly forced random events while players are AFK.");

        SleepPercent = Config.Bind("Sleep skip", "Required yes percentage", 50, new ConfigDescription("Percentage of active players required to skip the night.", new AcceptableValueRange<int>(1, 100)));
        SleepPlayersNeeded = Config.Bind("Sleep skip", "Players in bed to start vote", 2, new ConfigDescription("Minimum number of players in bed before a vote starts. Solo play bypasses this.", new AcceptableValueRange<int>(1, 100)));
        SleepWarningSeconds = Config.Bind("Sleep skip", "Warning seconds", 15, new ConfigDescription("Delay before the vote popup appears.", new AcceptableValueRange<int>(0, 60)));
        SleepVoteTimeoutSeconds = Config.Bind("Sleep skip", "Vote timeout seconds", 45, new ConfigDescription("After this time, players who have not voted abstain. 0 disables the timeout.", new AcceptableValueRange<int>(0, 300)));
        SleepCooldownSeconds = Config.Bind("Sleep skip", "Cooldown seconds", 0, new ConfigDescription("Cooldown between completed or failed votes.", new AcceptableValueRange<int>(0, 600)));
        SleepAutoAccept = Config.Bind("Sleep skip", "Automatically accept votes", false, "Automatically vote yes on the client when a popup arrives.");
    }

    internal static bool IsFeatureEnabled(ConfigEntry<bool> setting)
    {
        return setting != null && setting.Value;
    }

    internal static bool IsLocalPlayer(Player player)
    {
        return player != null && Player.m_localPlayer == player;
    }

    internal static int GetPocketCoins(Player player)
    {
        if (player == null || player.m_customData == null) return 0;

        bool migrated = player.m_customData.ContainsKey(CoinMigrationKey);
        if (!migrated)
        {
            int current = player.m_customData.TryGetValue(CoinKey, out string currentValue) && int.TryParse(currentValue, out int parsedCurrent)
                ? Math.Max(0, parsedCurrent)
                : 0;
            int legacy = player.m_customData.TryGetValue(LegacyCoinKey, out string legacyValue) && int.TryParse(legacyValue, out int parsedLegacy)
                ? Math.Max(0, parsedLegacy)
                : 0;

            // CurrencyPocket used the legacy key. Prefer an already-created
            // combined-mod balance when both keys exist: both plugins can see
            // the same pickup, so adding the two values is exactly how a
            // duplicated balance is created. If this is a first migration,
            // the legacy value is used instead.
            player.m_customData[CoinKey] = (current > 0 ? current : legacy).ToString();
            player.m_customData[CoinMigrationKey] = "1";
        }

        player.m_customData.Remove(LegacyCoinKey);
        return player.m_customData.TryGetValue(CoinKey, out string value) && int.TryParse(value, out int coins)
            ? Math.Max(0, coins)
            : 0;
    }

    internal static void SetPocketCoins(Player player, int coins)
    {
        if (player == null || player.m_customData == null) return;
        player.m_customData[CoinKey] = Math.Max(0, coins).ToString();
        player.m_customData[CoinMigrationKey] = "1";
        player.m_customData.Remove(LegacyCoinKey);
        UpdatePocketUi();
    }

    internal static string SleepVoteBody()
    {
        return $"{SleepInBed + SleepYesCount} yes / {SleepTotal} players\n\n" +
               $"In bed: {SleepInBed}\nYes: {SleepYesCount}\nNo: {SleepNoCount}\nWaiting: {SleepWaiting}\n\n" +
               $"Need {SleepPercent.Value}% to skip the night.";
    }

    internal static void UpdatePocketUi()
    {
        if (PocketText == null || Player.m_localPlayer == null) return;
        int coins = GetPocketCoins(Player.m_localPlayer);
        PocketText.text = coins.ToString();
        LastPocketValue = coins;
    }

    internal static void ExtractPocketCoins()
    {
        Player player = Player.m_localPlayer;
        if (player == null || ObjectDB.instance == null) return;
        int coins = GetPocketCoins(player);
        if (coins <= 0) return;
        GameObject prefab = ObjectDB.instance.GetItemPrefab(CoinPrefab);
        if (prefab == null) return;
        if (!player.GetInventory().CanAddItem(prefab, coins))
        {
            player.Message(MessageHud.MessageType.Center, "$inventory_full");
            return;
        }
        Inventory inventory = player.GetInventory();
        int before = inventory.CountItems(CoinToken);
        inventory.AddItem(prefab, coins);
        int added = Mathf.Max(0, inventory.CountItems(CoinToken) - before);
        if (added > 0) SetPocketCoins(player, Mathf.Max(0, coins - added));
    }

    internal static void DepositInventoryCoins()
    {
        Player player = Player.m_localPlayer;
        if (player == null) return;
        Inventory inventory = player.GetInventory();
        int coins = inventory.CountItems(CoinToken);
        if (coins <= 0) return;

        int before = inventory.CountItems(CoinToken);
        inventory.RemoveItem(CoinToken, coins);
        int deposited = Mathf.Max(0, before - inventory.CountItems(CoinToken));
        if (deposited > 0) SetPocketCoins(player, GetPocketCoins(player) + deposited);
    }

    internal static bool DepositDraggedCoins()
    {
        Player player = Player.m_localPlayer;
        InventoryGui gui = InventoryGui.m_instance;
        if (player == null || gui == null || gui.m_dragItem == null || gui.m_dragInventory == null || gui.m_dragAmount <= 0) return false;
        if (gui.m_dragItem.m_shared == null || gui.m_dragItem.m_shared.m_name != CoinToken) return false;

        int amount = Mathf.Min(gui.m_dragAmount, gui.m_dragItem.m_stack);
        if (amount <= 0) return false;
        int before = gui.m_dragItem.m_stack;
        gui.m_dragInventory.RemoveItem(gui.m_dragItem, amount);
        int removed = Mathf.Clamp(before - gui.m_dragItem.m_stack, 0, amount);
        if (removed <= 0) return false;
        SetPocketCoins(player, GetPocketCoins(player) + removed);
        gui.SetupDragItem(null, null, 1);
        return true;
    }

    internal static void CreatePocketUi(InventoryGui gui)
    {
        if (!IsFeatureEnabled(CurrencyPocket) || gui == null) return;
        // CurrencyPocket's working implementation uses InventoryGui.m_player
        // as the coordinate space. Expanded-inventory mods can move Armor and
        // Weight into child panels, so use the player panel when available and
        // only fall back to the GUI object on layouts that do not expose it.
        Transform inventoryRoot = GetPocketLayoutRoot(gui);
        if (inventoryRoot == null) return;
        Transform armor = FindDescendant(inventoryRoot, "Armor");
        Transform weight = FindDescendant(inventoryRoot, "Weight");
        if (armor == null) armor = FindDescendant(gui.transform, "Armor");
        if (weight == null) weight = FindDescendant(gui.transform, "Weight");
        if (armor == null && weight == null)
        {
            // Do not make the feature disappear when an inventory-layout mod
            // renames or removes the stock cards. Create a small self-contained
            // target under the live player panel as a visible fallback; it is
            // also a valid drag target for depositing inventory coins.
            CreateFallbackPocketUi(gui, inventoryRoot);
            return;
        }
        Transform source = armor ?? weight;

        // A GUI can survive a world/player transition and old versions of the
        // combined mod could also leave two cloned panels behind. Adopt one
        // panel and remove every duplicate before doing any layout work.
        GameObject existing = null;
        // Repositioning may intentionally reparent the clone next to Armor
        // inside an inventory-layout mod's canvas. Walk the whole player UI
        // hierarchy so old nested copies are adopted/removed too.
        Transform[] descendants = inventoryRoot.GetComponentsInChildren<Transform>(true);
        for (int i = descendants.Length - 1; i >= 0; --i)
        {
            Transform child = descendants[i];
            if (child == inventoryRoot) continue;
            if (child.name == PocketUiName)
            {
                if (existing == null) existing = child.gameObject;
                else Object.Destroy(child.gameObject);
            }
            else if (child.name == LegacyPocketUiName)
            {
                // The standalone CurrencyPocket plugin uses a different
                // custom-data key and a second panel. Remove that visual copy;
                // its balance is migrated by GetPocketCoins above.
                Object.Destroy(child.gameObject);
            }
        }

        if (PocketUi == null || !PocketUi || !PocketUi.transform.IsChildOf(inventoryRoot))
            PocketUi = existing;
        if (PocketUi == null)
        {
            PocketUi = Object.Instantiate(source.gameObject, inventoryRoot);
            PocketUi.name = PocketUiName;
        }
        else if (PocketUi.transform.parent != inventoryRoot)
        {
            // Reparent the surviving clone to the same panel used by the
            // stock inventory. Keeping it under an Armor sub-panel was the
            // source of the previous center-screen/off-screen placements.
            PocketUi.transform.SetParent(inventoryRoot, false);
        }
        PocketUi.SetActive(true);

        CurrencyPocketDropTarget[] targets = PocketUi.GetComponents<CurrencyPocketDropTarget>();
        if (targets.Length == 0) PocketUi.AddComponent<CurrencyPocketDropTarget>();
        for (int i = 1; i < targets.Length; ++i) Object.Destroy(targets[i]);

        Transform text = Utils.FindChild(PocketUi.transform, "ac_text");
        PocketText = text == null ? null : text.GetComponent<TextMeshProUGUI>();
        if (PocketText != null) PocketText.text = GetPocketCoins(Player.m_localPlayer).ToString();

        EnsurePocketButtons(gui);
        RepositionPocketUi(inventoryRoot, armor, weight);
        Graphic pocketGraphic = PocketUi.GetComponent<Graphic>();
        if (pocketGraphic != null) pocketGraphic.raycastTarget = true;
        CanvasGroup pocketCanvas = PocketUi.GetComponent<CanvasGroup>();
        if (pocketCanvas != null) pocketCanvas.blocksRaycasts = true;
        SetPocketIcon();
    }

    private static void CreateFallbackPocketUi(InventoryGui gui, Transform inventoryRoot)
    {
        if (PocketUi == null || !PocketUi || !PocketUi.transform.IsChildOf(inventoryRoot))
        {
            Transform existing = FindDescendant(inventoryRoot, PocketUiName);
            PocketUi = existing != null ? existing.gameObject : null;
        }

        if (PocketUi == null)
        {
            PocketUi = new GameObject(PocketUiName, typeof(RectTransform), typeof(Image), typeof(CurrencyPocketDropTarget));
            PocketUi.transform.SetParent(inventoryRoot, false);
            Image background = PocketUi.GetComponent<Image>();
            background.color = new Color(0.08f, 0.06f, 0.05f, 0.9f);

            GameObject textObject = new("ac_text", typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(PocketUi.transform, false);
            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(6f, 6f);
            textRect.offsetMax = new Vector2(-6f, -6f);
            TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
            text.alignment = TextAlignmentOptions.Center;
            text.fontSize = 20f;
            text.color = Color.yellow;
            text.enableWordWrapping = false;
        }

        PocketUi.SetActive(true);
        CurrencyPocketDropTarget[] targets = PocketUi.GetComponents<CurrencyPocketDropTarget>();
        if (targets.Length == 0) PocketUi.AddComponent<CurrencyPocketDropTarget>();
        PocketText = Utils.FindChild(PocketUi.transform, "ac_text")?.GetComponent<TextMeshProUGUI>();
        if (PocketText != null) PocketText.text = GetPocketCoins(Player.m_localPlayer).ToString();
        EnsurePocketButtons(gui);
        PositionFallbackPocketUi(gui, inventoryRoot);
        UpdatePocketUi();
        LogPocketUiWarning(gui, "Armor/Weight anchors were not present; using the visible fallback pocket target.");
    }

    private static void PositionFallbackPocketUi(InventoryGui gui, Transform inventoryRoot)
    {
        RectTransform pocketRect = PocketUi?.GetComponent<RectTransform>();
        if (pocketRect == null) return;
        if (pocketRect.parent != inventoryRoot) pocketRect.SetParent(inventoryRoot, false);
        pocketRect.anchorMin = new Vector2(0.5f, 0.5f);
        pocketRect.anchorMax = new Vector2(0.5f, 0.5f);
        pocketRect.pivot = new Vector2(0.5f, 0.5f);
        pocketRect.sizeDelta = new Vector2(110f, 58f);

        RectTransform gridRect = gui?.m_playerGrid?.GetComponent<RectTransform>();
        Vector2 position = gridRect != null
            ? gridRect.anchoredPosition + new Vector2(gridRect.rect.width * 0.5f + 70f, 0f)
            : Vector2.zero;
        pocketRect.anchoredPosition = position;
        SetPocketUiNativeLayer(gui, inventoryRoot, null, null);
    }

    internal static Transform GetPocketLayoutRoot(InventoryGui gui)
    {
        if (gui == null) return null;
        if (gui.m_player != null) return gui.m_player.transform;
        if (gui.m_playerGrid != null)
            return gui.m_playerGrid.transform.parent ?? gui.m_playerGrid.transform;
        return gui.transform;
    }

    private static Transform FindDescendant(Transform root, string name)
    {
        if (root == null) return null;
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child == root) continue;
            string childName = NormalizeUiName(child.name);
            if (childName.Equals(name, StringComparison.OrdinalIgnoreCase)) return child;
        }
        // Some inventory-layout mods clone these anchors and append a suffix.
        // Accept that suffix only after the exact-name pass, so a similarly
        // named unrelated control cannot win over the stock anchor.
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child == root) continue;
            if (NormalizeUiName(child.name).StartsWith(name, StringComparison.OrdinalIgnoreCase)) return child;
        }
        return null;
    }

    private static string NormalizeUiName(string name)
    {
        return (name ?? string.Empty).Replace("(Clone)", string.Empty).Trim();
    }

    private static Transform GetImmediateChildUnder(Transform target, Transform parent)
    {
        if (target == null || parent == null) return null;
        Transform current = target;
        while (current != null && current.parent != parent)
            current = current.parent;
        return current != null && current.parent == parent ? current : null;
    }

    private static void SetPocketUiNativeLayer(InventoryGui gui, Transform inventoryRoot, Transform armor, Transform weight)
    {
        RectTransform pocketRect = PocketUi == null ? null : PocketUi.GetComponent<RectTransform>();
        if (pocketRect == null || inventoryRoot == null || pocketRect.parent != inventoryRoot) return;

        // The stock Armor and Weight cards are siblings in InventoryGui's
        // player panel. Use their actual sibling layer instead of putting the
        // pocket at the top of the hierarchy. SetSiblingIndex inserts before
        // the reference, so the inventory's native card can still render in
        // front of the pocket at shared edges.
        int nativeLayer = int.MaxValue;
        Transform armorRoot = GetImmediateChildUnder(armor, inventoryRoot);
        Transform weightRoot = GetImmediateChildUnder(weight, inventoryRoot);
        if (armorRoot != null) nativeLayer = Mathf.Min(nativeLayer, armorRoot.GetSiblingIndex());
        if (weightRoot != null) nativeLayer = Mathf.Min(nativeLayer, weightRoot.GetSiblingIndex());

        // If an inventory-layout mod nested the stat cards somewhere else,
        // use the player grid's root sibling as the same visual layer. This
        // keeps the fallback deterministic without changing the pocket's
        // already-correct position.
        if (nativeLayer == int.MaxValue && gui?.m_playerGrid != null)
        {
            Transform gridRoot = GetImmediateChildUnder(gui.m_playerGrid.transform, inventoryRoot);
            if (gridRoot != null) nativeLayer = gridRoot.GetSiblingIndex();
        }

        if (nativeLayer != int.MaxValue)
            pocketRect.SetSiblingIndex(Mathf.Max(0, nativeLayer));
        else
            pocketRect.SetAsFirstSibling();
    }

    private static readonly HashSet<int> PocketUiWarnings = new();

    private static void LogPocketUiWarning(InventoryGui gui, string message)
    {
        if (gui == null || !PocketUiWarnings.Add(gui.GetInstanceID())) return;
        LogWarning($"Currency pocket UI: {message}");
    }

    private static void EnsurePocketButtons(InventoryGui gui)
    {
        if (PocketUi == null || gui == null || gui.m_takeAllButton == null) return;

        Transform rowTransform = PocketUi.transform.Find(PocketButtonRowName);
        if (rowTransform == null)
        {
            GameObject row = new(PocketButtonRowName, typeof(RectTransform));
            rowTransform = row.transform;
            rowTransform.SetParent(PocketUi.transform, false);
        }

        RectTransform rowRect = rowTransform as RectTransform;
        if (rowRect == null) return;
        rowRect.anchorMin = new Vector2(0.5f, 0f);
        rowRect.anchorMax = new Vector2(0.5f, 0f);
        rowRect.pivot = new Vector2(0.5f, 0.5f);
        rowRect.sizeDelta = new Vector2(72f, 22f);
        rowRect.anchoredPosition = new Vector2(0f, 12f);
        rowRect.localRotation = Quaternion.identity;
        rowRect.localScale = Vector3.one;

        // Remove stale buttons left by older 1.0.3 builds and keep only one
        // instance of each action. A cloned Armor card can survive inventory
        // transitions, so relying on the static Button references alone leaves
        // visually overlapping copies behind.
        PocketExtractButton = null;
        PocketDepositButton = null;
        foreach (Button button in PocketUi.GetComponentsInChildren<Button>(true))
        {
            if (button.name == "SwmarlyValheimQOL_ExtractCoins" && PocketExtractButton == null)
            {
                PocketExtractButton = button;
            }
            else if (button.name == "SwmarlyValheimQOL_DepositCoins" && PocketDepositButton == null)
            {
                PocketDepositButton = button;
            }
            else
            {
                // Armor/Weight cards do not contain buttons. Any other button
                // here is an orphan from a previous pocket layout.
                Object.Destroy(button.gameObject);
            }
        }
        if (PocketExtractButton == null)
        {
            PocketExtractButton = Object.Instantiate(gui.m_takeAllButton, rowTransform);
            PocketExtractButton.name = "SwmarlyValheimQOL_ExtractCoins";
        }
        if (PocketDepositButton == null)
        {
            PocketDepositButton = Object.Instantiate(gui.m_takeAllButton, rowTransform);
            PocketDepositButton.name = "SwmarlyValheimQOL_DepositCoins";
        }

        ConfigurePocketButton(PocketExtractButton, "↑", new Vector2(18f, 0f), ExtractPocketCoins);
        ConfigurePocketButton(PocketDepositButton, "↓", new Vector2(-18f, 0f), DepositInventoryCoins);
        rowTransform.SetAsLastSibling();
    }

    private static Button FindPocketButton(string name)
    {
        if (PocketUi == null) return null;
        foreach (Button button in PocketUi.GetComponentsInChildren<Button>(true))
        {
            if (button.name == name) return button;
        }
        return null;
    }

    private static void ConfigurePocketButton(Button button, string label, Vector2 position, UnityEngine.Events.UnityAction action)
    {
        if (button == null) return;
        button.transform.SetParent(PocketUi.transform.Find(PocketButtonRowName), false);
        button.transform.localScale = Vector3.one;
        button.transform.localRotation = Quaternion.identity;

        RectTransform rect = button.GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(28f, 18f);
            rect.anchoredPosition = position;
            rect.localPosition = new Vector3(position.x, position.y, 0f);
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one;
        }

        LayoutElement layout = button.GetComponent<LayoutElement>();
        if (layout != null) layout.ignoreLayout = true;
        TextMeshProUGUI text = button.GetComponentInChildren<TextMeshProUGUI>(true);
        if (text != null) text.text = label;
        button.interactable = true;
        button.onClick = new Button.ButtonClickedEvent();
        button.onClick.AddListener(action);
    }

    internal static void SchedulePocketUiReposition(InventoryGui gui)
    {
        if (Instance == null || gui == null) return;
        if (PocketUiRepositionCoroutine != null) Instance.StopCoroutine(PocketUiRepositionCoroutine);
        PocketUiRepositionCoroutine = Instance.StartCoroutine(RepositionPocketUiAfterLayout(gui));
    }

    private static IEnumerator RepositionPocketUiAfterLayout(InventoryGui gui)
    {
        // Inventory mods commonly move Armor/Weight from their own Show
        // postfixes. Wait until those postfixes have run before positioning our
        // clone, then repeat the calculation for a few frames so layout groups
        // and expanded-inventory mods cannot move the pocket back afterwards.
        for (int frame = 0; frame < 6; ++frame)
        {
            yield return null;
            yield return new WaitForEndOfFrame();
            if (gui == null) continue;
            Transform root = GetPocketLayoutRoot(gui);
            if (root == null) continue;
            Transform armor = FindDescendant(root, "Armor");
            Transform weight = FindDescendant(root, "Weight");
            if (armor != null || weight != null)
            {
                RepositionPocketUi(root, armor, weight);
                SetPocketIcon();
                UpdatePocketUi();
            }
        }
        PocketUiRepositionCoroutine = null;
    }

    private static void RepositionPocketUi(Transform inventoryRoot, Transform armor, Transform weight)
    {
        RectTransform pocketRect = PocketUi == null ? null : PocketUi.GetComponent<RectTransform>();
        RectTransform anchorRect = (armor ?? weight)?.GetComponent<RectTransform>();
        if (pocketRect == null || anchorRect == null) return;
        RectTransform armorRect = armor == null ? anchorRect : armor.GetComponent<RectTransform>();

        RectTransform weightRect = weight == null ? null : weight.GetComponent<RectTransform>();

        // Keep the clone in InventoryGui.m_player's coordinate space, exactly
        // like CurrencyPocket. Armor and Weight may be nested by an inventory
        // layout mod, so compute their midpoint in world space and convert it
        // once into the chosen root. This avoids applying a nested parent's
        // anchor offset a second time (the old reason for center-screen UI).
        if (pocketRect.parent != inventoryRoot)
            pocketRect.SetParent(inventoryRoot, false);
        pocketRect.anchorMin = new Vector2(0.5f, 0.5f);
        pocketRect.anchorMax = new Vector2(0.5f, 0.5f);
        pocketRect.pivot = new Vector2(0.5f, 0.5f);

        Vector3 targetWorld;
        if (armorRect != null && weightRect != null)
            targetWorld = (armorRect.position + weightRect.position) * 0.5f;
        else
            targetWorld = anchorRect.position + new Vector3(0f, -anchorRect.rect.height - 8f, 0f);
        Vector3 targetLocal = inventoryRoot.InverseTransformPoint(targetWorld);
        pocketRect.localPosition = new Vector3(targetLocal.x, targetLocal.y, pocketRect.localPosition.z);

        SetPocketUiNativeLayer(InventoryGui.m_instance, inventoryRoot, armor, weight);
    }

    private static void SetPocketIcon()
    {
        if (PocketUi == null || ObjectDB.instance == null) return;
        Transform icon = Utils.FindChild(PocketUi.transform, "armor_icon");
        GameObject coins = ObjectDB.instance.GetItemPrefab(CoinPrefab);
        if (icon == null || coins == null) return;
        Image image = icon.GetComponent<Image>();
        ItemDrop drop = coins.GetComponent<ItemDrop>();
        if (image != null && drop != null) image.sprite = drop.m_itemData.GetIcon();
    }
}

[HarmonyPatch(typeof(ItemDrop), "Awake")]
internal static class FloatingItemsPatch
{
    private static void Postfix(ItemDrop __instance)
    {
        EnsureFloating(__instance);
    }

    internal static void EnsureFloating(ItemDrop itemDrop)
    {
        if (!Plugin.IsFeatureEnabled(Plugin.FloatItems) || itemDrop == null) return;
        GameObject go = itemDrop.gameObject;
        Rigidbody body = go.GetComponent<Rigidbody>() ?? go.GetComponentInChildren<Rigidbody>();
        if (body == null) return;
        if (go.GetComponent<ZNetView>() == null && go.GetComponentInChildren<ZNetView>() == null) return;

        // Floating reads the Rigidbody from its own GameObject. Most drops
        // keep both components on the root, but a few network prefabs place
        // the Rigidbody on a child; attaching Floating to the root silently
        // does nothing for those items.
        GameObject host = body.gameObject;
        Floating floating = host.GetComponent<Floating>() ?? host.AddComponent<Floating>();
        floating.m_force = Plugin.FloatForce.Value;
        floating.m_damping = Plugin.FloatDamping.Value;
    }
}

[HarmonyPatch(typeof(ItemDrop), "Start")]
internal static class FloatingItemsStartPatch
{
    private static void Postfix(ItemDrop __instance)
    {
        // Some network-spawned drops receive their ZNetView after Awake.
        FloatingItemsPatch.EnsureFloating(__instance);
    }
}

internal static class EternalFireState
{
    private static string CachedPrefabConfig;
    private static readonly HashSet<string> ConfiguredPrefabs = new(StringComparer.OrdinalIgnoreCase);

    internal static bool IsConfigured(string rawName)
    {
        if (!Plugin.IsFeatureEnabled(Plugin.EternalFires)) return false;

        string config = Plugin.EternalFirePrefabs?.Value ?? string.Empty;
        if (!string.Equals(config, CachedPrefabConfig, StringComparison.Ordinal))
        {
            CachedPrefabConfig = config;
            ConfiguredPrefabs.Clear();
            foreach (string entry in config.Split(','))
            {
                string name = NormalizeName(entry);
                if (!string.IsNullOrEmpty(name)) ConfiguredPrefabs.Add(name);
            }
        }

        return ConfiguredPrefabs.Contains(NormalizeName(rawName));
    }

    internal static void KeepFuel(Fireplace fireplace, ZNetView nview)
    {
        if (fireplace == null || nview == null || !IsConfigured(fireplace.name) || !nview.IsValid() || !nview.IsOwner()) return;
        ZDO zdo = nview.GetZDO();
        if (zdo == null || fireplace.m_maxFuel <= 0f) return;

        // Only write when the synchronized value has actually fallen. This
        // keeps eternal lights cheap even in bases with many fireplaces and
        // avoids broadcasting the same ZDO value every frame.
        if (zdo.GetFloat("fuel", 0f) < fireplace.m_maxFuel - 0.01f)
            zdo.Set("fuel", fireplace.m_maxFuel);
    }

    internal static void ForceSetFuel(Fireplace fireplace, ref float fuel)
    {
        if (fireplace != null && IsConfigured(fireplace.name) && fireplace.m_maxFuel > 0f)
            fuel = fireplace.m_maxFuel;
    }

    private static string NormalizeName(string name)
    {
        return (name ?? string.Empty).Replace("(Clone)", string.Empty).Trim();
    }
}

// Fireplace owns Valheim's native fuel value for campfires, hearths, braziers,
// torches, and the other light pieces. Updating the ZDO on its owner makes the
// result authoritative in multiplayer; clients receive the normal synchronized
// fuel state and do not need a second custom network protocol.
[HarmonyPatch(typeof(Fireplace), nameof(Fireplace.UpdateFireplace))]
internal static class EternalFireUpdatePatch
{
    private static void Prefix(Fireplace __instance, ZNetView ___m_nview)
    {
        EternalFireState.KeepFuel(__instance, ___m_nview);
    }
}

[HarmonyPatch(typeof(Fireplace), nameof(Fireplace.SetFuel))]
internal static class EternalFireSetFuelPatch
{
    private static void Prefix(Fireplace __instance, ref float fuel)
    {
        EternalFireState.ForceSetFuel(__instance, ref fuel);
    }
}

internal static class AutoReplantState
{
    private const string ReplantScheduledKey = "SwmarlyValheimQOL_ReplantScheduled";
    private static readonly HashSet<int> PendingInstances = new();
    private static readonly HashSet<string> WarnedNames = new(StringComparer.OrdinalIgnoreCase);

    internal static void OnStumpDestroyed(Destructible destructible)
    {
        if (!Plugin.IsFeatureEnabled(Plugin.AutoReplantTrees) || destructible == null ||
            ZNet.instance == null || ZNetScene.instance == null)
            return;

        // Unity appends "(Clone)" to the live object name. Utils.GetPrefabName
        // is Valheim's own network-prefab normalization and is the reliable
        // value for tree stumps created by ZNetScene, so try both it and the
        // component name for compatibility with tree mods.
        string objectName = NormalizeName(destructible.name);
        string prefabName = NormalizeName(Utils.GetPrefabName(destructible.gameObject));
        if (!TryGetSaplingPrefab(new[] { prefabName, objectName }, out string saplingName))
        {
            if (WarnedNames.Add(prefabName) && !string.IsNullOrEmpty(prefabName))
                Plugin.LogWarning($"Automatic tree replanting saw stump '{prefabName}' but no mapping matched it. Add '{prefabName}=<sapling prefab>' to the config if it is a compatible tree.");
            return;
        }

        // The tree ZDO has one owner. Only that owner schedules the replacement
        // so a dedicated server plus several clients cannot plant duplicates.
        ZNetView nview = destructible.GetComponent<ZNetView>() ?? destructible.GetComponentInParent<ZNetView>();
        if (nview != null && !nview.IsOwner()) return;

        GameObject sapling = ZNetScene.instance.GetPrefab(saplingName);
        if (sapling == null)
        {
            if (WarnedNames.Add(saplingName))
                Plugin.LogWarning($"Automatic tree replanting could not find sapling prefab '{saplingName}' for stump '{prefabName}'. Check the spelling in the mapping config.");
            return;
        }

        ZDO zdo = nview?.GetZDO();
        if (zdo != null)
        {
            if (zdo.GetBool(ReplantScheduledKey)) return;
            zdo.Set(ReplantScheduledKey, true);
        }

        if (!PendingInstances.Add(destructible.GetInstanceID())) return;
        Vector3 position = destructible.transform.position;
        Quaternion rotation = Quaternion.Euler(0f, destructible.transform.eulerAngles.y, 0f);
        Plugin.Instance.StartCoroutine(SpawnSaplingAfterDelay(saplingName, position, rotation, destructible.GetInstanceID()));
    }

    private static bool TryGetSaplingPrefab(IEnumerable<string> stumpNames, out string saplingName)
    {
        string[] normalizedStumps = stumpNames
            .Select(NormalizeName)
            .Where(name => !string.IsNullOrEmpty(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        saplingName = null;
        string mappings = Plugin.TreeReplantMappings?.Value ?? string.Empty;
        foreach (string entry in mappings.Split(','))
        {
            string[] pair = entry.Split(new[] { '=', ':' }, 2);
            if (pair.Length != 2) continue;
            string stump = NormalizeName(pair[0]);
            string sapling = NormalizeName(pair[1]);
            if (stump.Length == 0 || sapling.Length == 0) continue;
            if (normalizedStumps.Any(normalizedStump =>
                    normalizedStump.Equals(stump, StringComparison.OrdinalIgnoreCase) ||
                    normalizedStump.StartsWith(stump, StringComparison.OrdinalIgnoreCase)))
            {
                saplingName = sapling;
                return true;
            }
        }

        return false;
    }

    private static IEnumerator SpawnSaplingAfterDelay(string prefabName, Vector3 position, Quaternion rotation, int instanceId)
    {
        yield return new WaitForSeconds(Mathf.Max(0f, Plugin.TreeReplantDelaySeconds.Value));
        PendingInstances.Remove(instanceId);

        if (ZNet.instance == null || ZNetScene.instance == null) yield break;
        GameObject prefab = ZNetScene.instance.GetPrefab(prefabName);
        if (prefab == null) yield break;

        // SpawnObject is Valheim's routed spawn path. It is invoked by the
        // stump owner (the dedicated server in a dedicated-server world, or
        // the host in a listen-server world), and the resulting ZDO is then
        // replicated to every client.
        ZNetScene.instance.SpawnObject(position, rotation, prefab);
    }

    private static string NormalizeName(string name)
    {
        return (name ?? string.Empty).Replace("(Clone)", string.Empty).Trim();
    }
}

[HarmonyPatch(typeof(Destructible), nameof(Destructible.Destroy))]
internal static class AutoReplantTreePatch
{
    private static void Prefix(Destructible __instance)
    {
        AutoReplantState.OnStumpDestroyed(__instance);
    }
}

// Valheim's native HaveRepairableItems/RepairOneItem pair already knows how
// to repair weapons, tools, armor, bows, shields, and modded items that use
// the normal ItemDrop durability fields. Running the same loop used by the
// established AutoRepair/ValheimPlus implementations from UpdateRepair keeps
// the crafting-station level checks and all station types intact.
[HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.UpdateRepair))]
internal static class AutoRepairAtWorkbenchPatch
{
    private static void Prefix(InventoryGui __instance)
    {
        if (!Plugin.IsFeatureEnabled(Plugin.AutoRepairAtWorkbench) || __instance == null || Player.m_localPlayer == null) return;

        CraftingStation station = Player.m_localPlayer.GetCurrentCraftingStation();
        if (station == null) return;

        int repaired = 0;
        // The inventory cannot contain anywhere near this many distinct
        // repairable entries in vanilla. The cap is a defensive guard against
        // a broken third-party item whose repair state never changes.
        while (repaired < 1024 && __instance.HaveRepairableItems())
        {
            __instance.RepairOneItem();
            repaired++;
        }

        if (repaired > 0)
            station.m_repairItemDoneEffects.Create(station.transform.position, Quaternion.identity, null, 1f);
    }
}

[HarmonyPatch(typeof(Player), "CheckRun")]
internal static class EquipWhileRunningPatch
{
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        if (!Plugin.IsFeatureEnabled(Plugin.EquipWhileRunning)) return instructions;

        List<CodeInstruction> code = instructions.ToList();
        int removedQueueClears = 0;

        for (int i = 1; i < code.Count; ++i)
        {
            if (code[i].opcode != System.Reflection.Emit.OpCodes.Call && code[i].opcode != System.Reflection.Emit.OpCodes.Callvirt)
                continue;

            MethodInfo called = code[i].operand as MethodInfo;
            if (called == null || called.Name != "ClearActionQueue") continue;

            // Valheim 1.0 queues weapons/tools whose equip duration is not
            // instant. Player.CheckRun clears that queue whenever sprinting,
            // so the hotbar selection flashes and then disappears. Remove the
            // receiver load and only that queue-clear call; preserve stamina
            // and sprint calculation.
            code[i - 1].opcode = System.Reflection.Emit.OpCodes.Nop;
            code[i].opcode = System.Reflection.Emit.OpCodes.Nop;
            removedQueueClears++;
        }

        if (removedQueueClears == 0)
            Plugin.LogWarning("Equip hotbar items while running: Player.CheckRun did not contain the expected ClearActionQueue call.");
        return code;
    }
}

// Character.UpdateWalking checks InMinorActionSlowdown before it checks the
// running flag. The equip animation is tagged "minoraction", so removing the
// CheckRun queue clear makes the item switch happen but still forces the
// player's speed down to walk speed for the duration of that animation. Keep
// the native animation and sprint calculation, but do not let that tag cancel
// sprinting while the local player is actively holding Run.
[HarmonyPatch(typeof(Player), nameof(Player.InMinorActionSlowdown))]
internal static class EquipWhileRunningSlowdownPatch
{
    private static bool Prefix(Player __instance, ref bool __result)
    {
        if (!Plugin.IsFeatureEnabled(Plugin.EquipWhileRunning) ||
            !Plugin.IsLocalPlayer(__instance) ||
            (!ZInput.GetButton("Run") && !ZInput.GetButton("JoyRun")))
            return true;

        __result = false;
        return false;
    }
}

// The reference UseEquipmentInWater implementation does not rewrite the
// equipment methods. Valheim's equipment code reaches IsSwimming through
// several internal paths, and a call-site transpiler misses those paths on
// some 1.0 builds. Instead, intercept only IsSwimming calls whose stack is
// currently inside EquipItem or UpdateEquipment. Movement, stamina, drowning
// and animation callers still receive the native swimming result.
[HarmonyPatch(typeof(Character), nameof(Character.IsSwimming))]
internal static class EquipmentInWaterSwimmingPatch
{
    private static bool Prefix(Character __instance, float ___m_swimTimer, ref bool __result)
    {
        if (!Plugin.IsFeatureEnabled(Plugin.EquipmentInWater) || __instance == null ||
            !__instance.IsPlayer() || ___m_swimTimer >= 0.5f)
            return true;

        StackTrace stack = new();
        for (int i = 2; i < stack.FrameCount && i < 10; i++)
        {
            string methodName = stack.GetFrame(i)?.GetMethod()?.Name ?? string.Empty;
            if (methodName == "EquipItem" || methodName == "UpdateEquipment")
            {
                __result = false;
                return false;
            }
        }

        return true;
    }
}

// These empty compatibility patches are intentional. The reference mod uses
// them to keep Harmony's equipment patch chain compatible with other mods;
// target the Valheim 1.0 EquipItem overload explicitly so it cannot become a
// second ambiguous Harmony target.
[HarmonyPatch(typeof(Humanoid), nameof(Humanoid.EquipItem), new[] { typeof(ItemDrop.ItemData), typeof(bool) })]
internal static class EquipmentInWaterEquipCompatibilityPatch
{
    private static void Prefix() { }
}

[HarmonyPatch(typeof(Humanoid), nameof(Humanoid.UpdateEquipment))]
internal static class EquipmentInWaterUpdateCompatibilityPatch
{
    private static void Prefix() { }
}

[HarmonyPatch(typeof(Player), "UseStamina")]
internal static class NoStaminaCostsPatch
{
    private static void Prefix(Player __instance, ref float v)
    {
        if (!Plugin.IsFeatureEnabled(Plugin.NoStaminaCosts) || Plugin.StaminaCostMode.Value <= 0) return;
        // Negative values are stamina regeneration. Never turn those into
        // zero, otherwise the all-actions mode also disables regeneration.
        if (v <= 0f) return;
        if (Plugin.StaminaCostMode.Value >= 2)
        {
            v = 0f;
            return;
        }
        ItemDrop.ItemData right = __instance.GetRightItem();
        ItemDrop.ItemData left = __instance.GetLeftItem();
        string rightName = right?.m_shared?.m_name ?? string.Empty;
        string leftName = left?.m_shared?.m_name ?? string.Empty;
        if (rightName is "$item_hammer" or "$item_hoe" or "$item_cultivator" || leftName is "$item_hammer" or "$item_hoe" or "$item_cultivator") v = 0f;
    }
}

internal enum QolGroundType
{
    Untamed,
    DirtPath,
    StonePath,
    Cultivated,
    WoodStructure,
    StoneStructure
}

internal static class SpeedyPathsState
{
    private static readonly FieldInfo PaintMaskField = AccessTools.Field(typeof(Heightmap), "m_paintMask");
    private static readonly FieldInfo LastGroundPointField = AccessTools.Field(typeof(Character), "m_lastGroundPoint");
    private static readonly MethodInfo WorldToVertexMethod = AccessTools.Method(typeof(Heightmap), "WorldToVertex");
    private static readonly object[] WorldToVertexArgs = { Vector3.zero, 0, 0 };
    private static readonly int PieceLayer = LayerMask.NameToLayer("piece");

    private static float sensorTimer;
    private static QolGroundType cachedGroundType;
    internal static float ActiveSpeedMultiplier { get; private set; } = 1f;
    internal static float ActiveStaminaMultiplier { get; private set; } = 1f;

    internal static void Update(Player player)
    {
        bool pathFeatureActive = Plugin.IsFeatureEnabled(Plugin.SpeedyPaths) || Plugin.IsFeatureEnabled(Plugin.NoStaminaOnPaths);
        if (!pathFeatureActive || !Plugin.IsLocalPlayer(player) || player.IsDead())
        {
            ActiveSpeedMultiplier = 1f;
            ActiveStaminaMultiplier = 1f;
            return;
        }

        sensorTimer -= Time.fixedDeltaTime;
        if (sensorTimer <= 0f)
        {
            sensorTimer = Mathf.Max(0.05f, Plugin.PathSensorInterval.Value);
            cachedGroundType = DetectGround(player);
        }

        if (player.IsSwimming() || player.InInterior())
        {
            ActiveSpeedMultiplier = 1f;
            ActiveStaminaMultiplier = 1f;
            return;
        }

        ActiveSpeedMultiplier = GetSpeedMultiplier(cachedGroundType);
        ActiveStaminaMultiplier = GetStaminaMultiplier(cachedGroundType);
    }

    private static float GetSpeedMultiplier(QolGroundType ground)
    {
        return ground switch
        {
            QolGroundType.DirtPath => Mathf.Max(0.1f, Plugin.DirtPathSpeed.Value),
            QolGroundType.StonePath => Mathf.Max(0.1f, Plugin.StonePathSpeed.Value),
            QolGroundType.Cultivated => Mathf.Max(0.1f, Plugin.CultivatedSpeed.Value),
            QolGroundType.WoodStructure => Mathf.Max(0.1f, Plugin.WoodPathSpeed.Value),
            QolGroundType.StoneStructure => Mathf.Max(0.1f, Plugin.StoneStructureSpeed.Value),
            _ => 1f
        };
    }

    private static float GetStaminaMultiplier(QolGroundType ground)
    {
        if (ground is QolGroundType.DirtPath or QolGroundType.StonePath)
            return Plugin.IsFeatureEnabled(Plugin.NoStaminaOnPaths) ? 0f :
                (ground == QolGroundType.DirtPath ? Mathf.Max(0f, Plugin.DirtPathStamina.Value) : Mathf.Max(0f, Plugin.StonePathStamina.Value));
        if (ground == QolGroundType.Cultivated)
            return Mathf.Max(0f, Plugin.CultivatedStamina.Value);
        if (ground is QolGroundType.WoodStructure or QolGroundType.StoneStructure)
            return Mathf.Max(0f, Plugin.StructurePathStamina.Value);
        return 1f;
    }

    private static QolGroundType DetectGround(Player player)
    {
        try
        {
            Collider ground = player.GetLastGroundCollider();
            if (ground == null) return QolGroundType.Untamed;

            if (ground.gameObject.layer == PieceLayer)
            {
                WearNTear wear = ground.GetComponentInParent<WearNTear>();
                if (wear != null)
                {
                    return wear.m_materialType switch
                    {
                        WearNTear.MaterialType.Wood or WearNTear.MaterialType.HardWood => QolGroundType.WoodStructure,
                        WearNTear.MaterialType.Stone or WearNTear.MaterialType.Iron or WearNTear.MaterialType.Marble or WearNTear.MaterialType.Ashstone => QolGroundType.StoneStructure,
                        _ => QolGroundType.Untamed
                    };
                }
            }

            Heightmap heightmap = ground.GetComponent<Heightmap>() ?? ground.GetComponentInParent<Heightmap>();
            if (heightmap == null || PaintMaskField == null || WorldToVertexMethod == null)
                return QolGroundType.Untamed;

            // Do not reflect an instance field with a null target. On terrain
            // colliders that are not Heightmap-owned this used to throw every
            // sensor tick, so paths silently fell back to Untamed.
            Texture2D paintMask = PaintMaskField.GetValue(heightmap) as Texture2D;
            object rawLastPoint = LastGroundPointField?.GetValue(player);
            if (paintMask == null || !paintMask.isReadable || rawLastPoint is not Vector3 lastPoint)
                return QolGroundType.Untamed;

            WorldToVertexArgs[0] = lastPoint;
            WorldToVertexArgs[1] = 0;
            WorldToVertexArgs[2] = 0;
            WorldToVertexMethod.Invoke(heightmap, WorldToVertexArgs);
            int centerX = Convert.ToInt32(WorldToVertexArgs[1]);
            int centerY = Convert.ToInt32(WorldToVertexArgs[2]);
            int radius = 1;
            int minX = Mathf.Clamp(centerX - radius, 0, paintMask.width - 1);
            int minY = Mathf.Clamp(centerY - radius, 0, paintMask.height - 1);
            int width = Mathf.Min(radius * 2 + 1, paintMask.width - minX);
            int height = Mathf.Min(radius * 2 + 1, paintMask.height - minY);
            if (width <= 0 || height <= 0) return QolGroundType.Untamed;

            Color average = Color.black;
            Color[] samples = paintMask.GetPixels(minX, minY, width, height, 0);
            foreach (Color sample in samples) average += sample;
            average /= Mathf.Max(1, samples.Length);
            if (average.b > 0.4f) return QolGroundType.StonePath;
            if (average.r > 0.4f) return QolGroundType.DirtPath;
            if (average.g > 0.4f) return QolGroundType.Cultivated;
        }
        catch (Exception exception)
        {
            Plugin.LogWarning($"Speedy paths ground detection failed once: {exception.Message}");
        }
        return QolGroundType.Untamed;
    }
}

[HarmonyPatch(typeof(Player), "FixedUpdate")]
internal static class SpeedyPathsUpdatePatch
{
    private static void Prefix(Player __instance)
    {
        SpeedyPathsState.Update(__instance);
    }
}

[HarmonyPatch(typeof(Player), "CheckRun")]
internal static class SpeedyPathsStaminaPatch
{
    private static void Prefix(Player __instance, out float __state)
    {
        __state = __instance.m_runStaminaDrain;
        if ((Plugin.IsFeatureEnabled(Plugin.SpeedyPaths) || Plugin.IsFeatureEnabled(Plugin.NoStaminaOnPaths)) && Plugin.IsLocalPlayer(__instance))
            __instance.m_runStaminaDrain *= SpeedyPathsState.ActiveStaminaMultiplier;
    }

    private static void Postfix(Player __instance, float __state)
    {
        __instance.m_runStaminaDrain = __state;
    }
}

[HarmonyPatch(typeof(Player), "GetJogSpeedFactor")]
internal static class SpeedyPathsJogPatch
{
    private static void Postfix(Player __instance, ref float __result)
    {
        if (Plugin.IsFeatureEnabled(Plugin.SpeedyPaths) && Plugin.IsLocalPlayer(__instance))
            __result *= SpeedyPathsState.ActiveSpeedMultiplier;
    }
}

[HarmonyPatch(typeof(Player), "GetRunSpeedFactor")]
internal static class SpeedyPathsRunPatch
{
    private static void Postfix(Player __instance, ref float __result)
    {
        if (Plugin.IsFeatureEnabled(Plugin.SpeedyPaths) && Plugin.IsLocalPlayer(__instance))
            __result *= SpeedyPathsState.ActiveSpeedMultiplier;
    }
}

internal static class NoAfkRaidsState
{
    private readonly struct Activity
    {
        internal readonly Vector3 Position;
        internal readonly DateTime LastActivity;

        internal Activity(Vector3 position, DateTime lastActivity)
        {
            Position = position;
            LastActivity = lastActivity;
        }
    }

    private static readonly Dictionary<long, Activity> Players = new();

    private static HashSet<long> GetConnectedPlayers()
    {
        HashSet<long> connected = new();
        if (ZNet.instance == null) return connected;
        foreach (ZNetPeer peer in ZNet.instance.m_peers)
        {
            if (peer?.m_characterID.UserID != 0) connected.Add(peer.m_characterID.UserID);
        }
        if (!ZNet.instance.IsDedicated()) connected.Add(ZNet.GetUID());
        return connected;
    }

    private static bool TryGetEventPosition(object[] args, out Vector3 position)
    {
        if (args != null)
        {
            foreach (object argument in args)
            {
                if (argument is Vector3 directPosition)
                {
                    position = directPosition;
                    return true;
                }

                if (argument == null) continue;
                Type type = argument.GetType();
                foreach (string name in new[] { "m_pos", "m_position", "m_eventPos", "m_spawnPoint", "m_spawnPos" })
                {
                    FieldInfo field = AccessTools.Field(type, name);
                    if (field?.GetValue(argument) is Vector3 fieldPosition)
                    {
                        position = fieldPosition;
                        return true;
                    }
                    PropertyInfo property = AccessTools.Property(type, name);
                    if (property?.GetValue(argument) is Vector3 propertyPosition)
                    {
                        position = propertyPosition;
                        return true;
                    }
                }
            }
        }

        position = default;
        return false;
    }

    internal static bool ShouldBlock(object[] args)
    {
        if (!Plugin.IsFeatureEnabled(Plugin.NoAfkRaids) || ZNet.instance == null || !ZNet.instance.IsServer()) return false;

        // SetRandomEvent's last argument is its explicit/forced flag. Looking
        // at every bool would also classify unrelated future parameters as a
        // forced raid.
        bool forced = args != null && args.Length > 0 && args[args.Length - 1] is bool forcedArgument && forcedArgument;
        if (forced && !Plugin.IsFeatureEnabled(Plugin.BlockForcedRaids)) return false;

        List<ZDO> characters;
        try
        {
            characters = ZNet.instance.GetAllCharacterZDOS();
        }
        catch
        {
            return false;
        }

        if (characters == null || characters.Count == 0) return false;
        DateTime now = DateTime.UtcNow;
        HashSet<long> connected = GetConnectedPlayers();
        int connectedPlayers = 0;
        int afkPlayers = 0;
        HashSet<long> seen = new();

        foreach (ZDO zdo in characters)
        {
            if (zdo == null || zdo.m_uid.UserID == 0) continue;
            long userId = zdo.m_uid.UserID;
            if (connected.Count > 0 && !connected.Contains(userId)) continue;
            seen.Add(userId);
            connectedPlayers++;
            Vector3 position = zdo.m_position;
            if (!Players.TryGetValue(userId, out Activity previous) ||
                Vector3.Distance(previous.Position, position) >= Mathf.Max(0.01f, Plugin.AfkMovementThreshold.Value))
            {
                Players[userId] = new Activity(position, now);
                continue;
            }

            if ((now - previous.LastActivity).TotalMinutes >= Mathf.Max(0.1f, Plugin.AfkMinutes.Value)) afkPlayers++;
        }

        foreach (long userId in Players.Keys.ToArray())
            if (!seen.Contains(userId)) Players.Remove(userId);

        if (connectedPlayers <= 0) return false;

        // NoAFKRaids protects the event area, not merely the whole world. A
        // moving player elsewhere must not make an AFK player beside the raid
        // vulnerable, and an AFK player far away must not suppress a local
        // event. If the current Valheim build does not expose the event point
        // in the RPC arguments, retain the safe all-AFK fallback.
        if (TryGetEventPosition(args, out Vector3 eventPosition) && Plugin.AfkProtectionRadius.Value > 0f)
        {
            float radius = Plugin.AfkProtectionRadius.Value;
            foreach (ZDO zdo in characters)
            {
                if (zdo == null || zdo.m_uid.UserID == 0 || (connected.Count > 0 && !connected.Contains(zdo.m_uid.UserID))) continue;
                if (!Players.TryGetValue(zdo.m_uid.UserID, out Activity activity)) continue;
                if ((DateTime.UtcNow - activity.LastActivity).TotalMinutes < Mathf.Max(0.1f, Plugin.AfkMinutes.Value)) continue;
                if (Vector3.Distance(activity.Position, eventPosition) <= radius) return true;
            }
            return false;
        }

        return afkPlayers >= connectedPlayers;
    }
}

[HarmonyPatch(typeof(RandEventSystem), "SetRandomEvent")]
internal static class NoAfkRaidsPatch
{
    private static bool Prefix(object[] __args)
    {
        return !NoAfkRaidsState.ShouldBlock(__args);
    }
}

[HarmonyPatch(typeof(WearNTear), "Awake")]
internal static class NoRainDamagePatch
{
    private static void Postfix(WearNTear __instance)
    {
        if (Plugin.IsFeatureEnabled(Plugin.NoRainDamage)) __instance.m_noRoofWear = false;
    }
}

[HarmonyPatch(typeof(WearNTear), "UpdateWear")]
internal static class NoRainDamageUpdatePatch
{
    private static void Prefix(WearNTear __instance)
    {
        // Loaded structures may not run Awake again after the config is
        // enabled. Reapply the flag at the actual wear calculation point.
        if (Plugin.IsFeatureEnabled(Plugin.NoRainDamage)) __instance.m_noRoofWear = false;
    }
}

[HarmonyPatch(typeof(MonsterAI), "Awake")]
internal static class FleeOnSightPatch
{
    private static void Postfix(MonsterAI __instance)
    {
        if (!Plugin.IsFeatureEnabled(Plugin.FleeOnSight)) return;
        string name = __instance.name.ToLowerInvariant();
        foreach (string configured in Plugin.FleeMobNames.Value.Split(','))
        {
            if (!string.IsNullOrWhiteSpace(configured) && name.Contains(configured.Trim().ToLowerInvariant()))
            {
                __instance.m_fleeIfNotAlerted = true;
                return;
            }
        }
    }
}

[HarmonyPatch(typeof(Player), "Update")]
internal static class PlayerQolUpdatePatch
{
    private static readonly Dictionary<int, float> BaseCrouchSpeed = new();

    private static void Prefix(Player __instance)
    {
        int id = __instance.GetInstanceID();
        if (Plugin.IsFeatureEnabled(Plugin.SneakSpeed))
        {
            if (!BaseCrouchSpeed.ContainsKey(id)) BaseCrouchSpeed[id] = __instance.m_crouchSpeed;
            float factor = __instance.m_skills == null ? 0f : __instance.m_skills.GetSkillFactor(Skills.SkillType.Sneak);
            __instance.m_crouchSpeed = BaseCrouchSpeed[id] * Mathf.Lerp(1f, Plugin.SneakSpeedMultiplier.Value, factor);
        }
    }

    private static void Postfix(Player __instance)
    {
        if (Plugin.IsLocalPlayer(__instance) && Plugin.IsFeatureEnabled(Plugin.SwimImprovements) && __instance.IsSwimming() && __instance.GetMoveDir().magnitude < 0.1f && Plugin.SwimIdleStaminaPerSecond.Value > 0f)
            __instance.UseStamina(-Plugin.SwimIdleStaminaPerSecond.Value * Time.deltaTime);

    }
}

[HarmonyPatch(typeof(Character), "UpdateSwimming")]
internal static class SwimImprovementsPatch
{
    private static readonly Dictionary<int, float> BaseSwimSpeed = new();

    private static void Prefix(Character __instance)
    {
        if (!Plugin.IsFeatureEnabled(Plugin.SwimImprovements) || __instance is not Player player) return;

        int id = player.GetInstanceID();
        if (!BaseSwimSpeed.ContainsKey(id)) BaseSwimSpeed[id] = player.m_swimSpeed;
        float factor = player.m_skills == null ? 0f : player.m_skills.GetSkillFactor(Skills.SkillType.Swim);
        float speed = BaseSwimSpeed[id] * Mathf.Lerp(1f, Plugin.MaxSwimSpeedMultiplier.Value, factor);
        if (Plugin.SwimSprint.Value && Plugin.IsLocalPlayer(player) && (ZInput.GetButton("Run") || ZInput.GetButton("JoyRun"))) speed *= 1.25f;
        player.m_swimSpeed = speed;
    }
}

[HarmonyPatch(typeof(Player), "FixedUpdate")]
internal static class SitRegenerationFixedPatch
{
    private static readonly Dictionary<int, float> NextHealAt = new();
    private const float HealIntervalSeconds = 1f;

    private static void Postfix(Player __instance)
    {
        int id = __instance.GetInstanceID();
        if (!Plugin.IsLocalPlayer(__instance) || !Plugin.IsFeatureEnabled(Plugin.SitRegeneration) ||
            !__instance.IsSitting() || __instance.GetHealth() >= __instance.GetMaxHealth())
        {
            NextHealAt.Remove(id);
            return;
        }

        // Use an absolute realtime deadline instead of accumulating
        // fixedDeltaTime. In multiplayer, duplicate/replayed FixedUpdate
        // callbacks and simulation catch-up can otherwise add the same time
        // more than once and make sitting regeneration much faster near other
        // networked players. Never catch up multiple ticks: one Heal call is
        // allowed per real-time second for this player.
        float now = Time.realtimeSinceStartup;
        if (!NextHealAt.TryGetValue(id, out float nextHealAt))
        {
            NextHealAt[id] = now + HealIntervalSeconds;
            return;
        }

        if (now < nextHealAt || Plugin.SitHealPerSecond.Value <= 0f) return;

        float amount = Mathf.Min(Plugin.SitHealPerSecond.Value, __instance.GetMaxHealth() - __instance.GetHealth());
        if (amount > 0f) __instance.Heal(amount);
        NextHealAt[id] = now + HealIntervalSeconds;
    }
}

[HarmonyPatch(typeof(Character), "CustomFixedUpdate")]
internal static class DivingPatch
{
    internal static bool DiveToggle;
    // Native IsSwimming can become false for a frame while the player is
    // already below the surface. Keep this state independently so that
    // UpdateMotion/UpdateSwimming cannot interpret that transition as a
    // request to surface and apply the upward launch impulse.
    internal static bool IsUnderwater;
    private static float LastRequestedDepth = 1.6f;

    private static bool IsDiveHeld()
    {
        return Plugin.DiveKey.Value.IsPressed();
    }

    private static bool IsSurfaceHeld()
    {
        return Plugin.SurfaceKey.Value.IsPressed() || ZInput.GetButton("Jump") || ZInput.GetButton("JoyJump");
    }

    private static bool IsForwardHeld()
    {
        return ZInput.GetButton("Forward") || ZInput.GetButton("JoyLStickUp");
    }

    internal static bool IsLocalWaterPlayer(Character character)
    {
        return Plugin.IsFeatureEnabled(Plugin.Diving) && character is Player player &&
               Plugin.IsLocalPlayer(player) && (!player.IsOnGround() || IsDiveGroundContact(player)) && !player.IsDead() &&
               (IsInLiquid(player) || DiveToggle || IsUnderwater || HasDiveTarget(player));
    }

    internal static bool IsInLiquid(Player player)
    {
        if (player == null) return false;
        // IsSwimming remains true during the native transition into a dive;
        // relying only on InWater() makes the input/camera patches drop out
        // for a frame and restores the vanilla surface target.
        return HasPhysicalLiquid(player) || player.IsSwimming();
    }

    internal static bool HasPhysicalLiquid(Player player)
    {
        return player != null &&
               Mathf.Max(0f, player.GetLiquidLevel() - player.transform.position.y) > 0.15f;
    }

    internal static bool HasDiveTarget(Player player)
    {
        return player != null && player.m_swimDepth > 2.5f;
    }

    internal static bool HasNativeSwimDepth(Player player)
    {
        if (player == null) return false;

        // Mirrors Valheim 1.0 Character.InLiquidSwimDepth(): liquid depth
        // must exceed the requested swim depth minus 0.4 metres. This lets a
        // diver remain on a genuinely deep seabed while a shoreline can hand
        // control back to normal walking.
        float liquidDepth = Mathf.Max(0f, player.GetLiquidLevel() - player.transform.position.y);
        return liquidDepth > Mathf.Max(0f, player.m_swimDepth - 0.4f);
    }

    internal static bool IsActuallyUnderwater(Player player)
    {
        return player != null && (!player.IsOnGround() || IsDiveGroundContact(player)) && !player.IsDead() &&
               player.m_swimDepth > 2.5f &&
               Mathf.Max(0f, player.GetLiquidLevel() - player.transform.position.y) > 2.5f;
    }

    // Valheim's IsOnGround also becomes true when the player is standing on
    // the seabed. That is not a land/surface reset condition: resetting the
    // swim target there to 1.6 metres is exactly what launches a diver back to
    // the surface. The target depth may be deeper than the available water
    // column, especially in shallow water; the native collider should hold
    // the player at the seabed instead of this patch resetting the swim state.
    internal static bool IsDiveGroundContact(Player player)
    {
        return player != null && player.IsOnGround() &&
               (DiveToggle || IsUnderwater || HasDiveTarget(player)) &&
               HasNativeSwimDepth(player);
    }

    internal static void ReleaseToWalking(Player player, ref float swimTimer)
    {
        DiveToggle = false;
        IsUnderwater = false;
        LastRequestedDepth = 1.6f;
        player.m_swimDepth = 1.6f;

        // Character.IsSwimming() is m_swimTimer < 0.5f. Set the exact native
        // boundary so UpdateMotion selects UpdateWalking immediately instead
        // of running one more swimming frame and reasserting the dive state.
        swimTimer = 0.5f;
    }

    internal static bool ShouldKeepNativeSwimming(Player player)
    {
        return player != null && Plugin.IsLocalPlayer(player) &&
               (!player.IsOnGround() || IsDiveGroundContact(player)) && !player.IsDead() &&
               (IsActuallyUnderwater(player) || IsUnderwater || DiveToggle || IsDiveHeld() || IsSurfaceHeld());
    }

    private static void Prefix(Character __instance, float dt, ref Vector3 ___m_moveDir, ref Vector3 ___m_lookDir,
        ref float ___m_lastGroundTouch, ref float ___m_swimTimer)
    {
        if (__instance is not Player player || !Plugin.IsFeatureEnabled(Plugin.Diving) || !Plugin.IsLocalPlayer(player)) return;

        // Do not use InWater() as the reset condition while a dive is active.
        // The cached liquid-depth value can briefly report false during the
        // transition below the surface; resetting m_swimDepth there restores
        // the vanilla 1.6 target and launches the player back up.
        if ((player.IsOnGround() && !IsDiveGroundContact(player)) || player.IsDead())
        {
            ReleaseToWalking(player, ref ___m_swimTimer);
            return;
        }

        // Keep a dive alive through the short transition where the vanilla
        // water-volume query is false. Only reset once the player is neither
        // swimming nor submerged; this is the distinction BetterDiving uses
        // to prevent the upward bounce a few seconds after diving.
        if (!IsInLiquid(player) && !player.IsSwimming() && !IsActuallyUnderwater(player))
        {
            DiveToggle = false;
            player.m_swimDepth = 1.6f;
            LastRequestedDepth = 1.6f;
            return;
        }

        float fixedDelta = Mathf.Max(dt, Time.fixedDeltaTime);
        float depth = player.m_swimDepth;
        float speed = Mathf.Max(0.5f, Plugin.DiveSpeed.Value);
        bool directDive = IsDiveHeld();
        bool surface = IsSurfaceHeld();

        if (directDive && !surface)
        {
            depth += speed * fixedDelta;
        }
        else if (surface && !directDive)
        {
            depth -= speed * fixedDelta;
        }
        else if (DiveToggle)
        {
            // BetterDiving uses the look direction to control its target depth.
            // Keep that behavior when the player looks up/down, but make the
            // toggle itself descend instead of requiring a short input event to
            // be observed by FixedUpdate. This is reliable with both keyboard
            // and controller input and keeps the player underwater until the
            // surface key/jump or an upward look is used.
            if (___m_lookDir.y > 0.15f) depth -= speed * fixedDelta;
            else depth += speed * fixedDelta;
        }

        player.m_swimDepth = Mathf.Clamp(depth, 1.6f, 20f);
        LastRequestedDepth = player.m_swimDepth;
        if (player.m_swimDepth > 2.5f)
            IsUnderwater = true;
        if (surface && !directDive && player.m_swimDepth <= 1.65f)
        {
            DiveToggle = false;
            IsUnderwater = false;
        }
        if (player.m_swimDepth > 2.5f && DiveToggle && IsForwardHeld()) player.SetMoveDir(___m_lookDir);

        // BetterDiving keeps both native timers alive while the target is
        // below the surface. This is what prevents the native controller from
        // deciding that the player surfaced and applying the bounce impulse.
        if (ShouldKeepNativeSwimming(player))
        {
            ___m_lastGroundTouch = 0.3f;
            ___m_swimTimer = 0f;
        }

        if (Plugin.DiveStaminaPerSecond.Value > 0f && (directDive || surface || DiveToggle))
            player.UseStamina(Plugin.DiveStaminaPerSecond.Value * fixedDelta);
    }

    private static void Postfix(Character __instance, ref float ___m_lastGroundTouch, ref float ___m_swimTimer)
    {
        if (__instance is not Player player || !Plugin.IsFeatureEnabled(Plugin.Diving) || !Plugin.IsLocalPlayer(player) ||
            (player.IsOnGround() && !IsDiveGroundContact(player)) || player.IsDead() || (!IsInLiquid(player) && !player.IsSwimming() && !HasDiveTarget(player) && !IsUnderwater)) return;

        bool controllingDepth = DiveToggle || IsDiveHeld() || IsSurfaceHeld();
        if (!controllingDepth) return;

        // Character.CustomFixedUpdate can rewrite m_swimDepth after a prefix
        // (and this is also where some water mods do it). Re-apply the target
        // after vanilla has finished so the dive is not cancelled a frame
        // later and the native swim timer cannot launch the player upward.
        player.m_swimDepth = Mathf.Clamp(LastRequestedDepth, 1.6f, 20f);
        if (player.m_swimDepth > 2.5f || DiveToggle)
        {
            ___m_lastGroundTouch = 0.3f;
            ___m_swimTimer = 0f;
        }
    }
}

// Character.UpdateMotion chooses UpdateWalking whenever the native swim timer
// briefly expires. That is the exact frame in which Valheim applies the
// surface correction and the player gets launched upward. A controlled local
// dive is still a swim state even during that transient timer gap; return true
// for the native state query until the player intentionally surfaces or leaves
// the water volume.
[HarmonyPatch(typeof(Character), nameof(Character.IsSwimming))]
internal static class DivingNativeSwimmingStatePatch
{
    private static bool Prefix(Character __instance, ref bool __result)
    {
        if (__instance is not Player player || !Plugin.IsFeatureEnabled(Plugin.Diving) ||
            !Plugin.IsLocalPlayer(player) ||
            (player.IsOnGround() && !DivingPatch.IsDiveGroundContact(player)) || player.IsDead())
            return true;

        if (!DivingPatch.IsUnderwater && !DivingPatch.DiveToggle && !DivingPatch.HasDiveTarget(player))
            return true;

        // Do not pin the state after the player has actually left the liquid;
        // CustomFixedUpdate will clear the persistent flags on its next pass.
        if (!DivingPatch.HasPhysicalLiquid(player) && !player.InWater())
            return true;

        __result = true;
        return false;
    }
}

// Input edge detection belongs in Player.Update. Reading GetButtonDown from a
// fixed-update patch can miss the one rendered frame in which the button was
// pressed, especially when the server/client frame and physics rates differ.
[HarmonyPatch(typeof(Player), "Update")]
internal static class DivingInputPatch
{
    private static void Prefix(Player __instance)
    {
        if (!Plugin.IsFeatureEnabled(Plugin.Diving) || !Plugin.IsLocalPlayer(__instance) ||
            (!DivingPatch.IsInLiquid(__instance) && !__instance.IsSwimming() && !DivingPatch.IsUnderwater && !DivingPatch.DiveToggle) ||
            (__instance.IsOnGround() && !DivingPatch.IsDiveGroundContact(__instance)) || __instance.IsDead()) return;

        if (ZInput.GetButtonDown("Crouch") || ZInput.GetButtonDown("JoyCrouch"))
        {
            // Match BetterDiving's state machine: crouch starts a dive, but
            // pressing crouch again while already below the surface must not
            // cancel the native underwater state. A second press only cancels
            // once the player has returned to the surface band; otherwise the
            // next vanilla swim-timer update launches the player upward.
            if (!DivingPatch.DiveToggle)
                DivingPatch.DiveToggle = true;
            else if (__instance.m_swimDepth <= 2.5f)
                DivingPatch.DiveToggle = false;
        }
    }
}

[HarmonyPatch(typeof(Character), "UpdateMotion")]
internal static class DivingMotionPatch
{
    [HarmonyPriority(Priority.First)]
    private static void Prefix(Character __instance, ref float ___m_lastGroundTouch, ref float ___m_swimTimer)
    {
        if (__instance is not Player player) return;
        if (!Plugin.IsFeatureEnabled(Plugin.Diving) || !Plugin.IsLocalPlayer(player)) return;
        if ((player.IsOnGround() && !DivingPatch.IsDiveGroundContact(player)) || player.IsDead())
        {
            DivingPatch.ReleaseToWalking(player, ref ___m_swimTimer);
            return;
        }

        // This is the exact state BetterDiving maintains. Valheim's native
        // UpdateSwimming applies an upward launch as soon as m_swimTimer is
        // allowed to expire, even when m_swimDepth is still below the surface.
        // Keep the native swimmer alive based on actual liquid depth, not the
        // transient InWater() cache or the input toggle.
        // BetterDiving's actual-liquid check is the important part, but the
        // native swimmer can expire its timer during the short transition in
        // which the target depth is already underwater while the body is only
        // partly submerged. Keep the timer alive for that controlled dive too;
        // otherwise Valheim applies its upward surface impulse after a few
        // seconds, which is the recurring bounce reported on dedicated
        // servers.
        bool controlledDive = player.m_swimDepth > 2.5f &&
                              (DivingPatch.HasPhysicalLiquid(player) || player.IsSwimming() || DivingPatch.DiveToggle || DivingPatch.IsUnderwater);
        if (DivingPatch.IsActuallyUnderwater(player) || controlledDive)
        {
            ___m_lastGroundTouch = 0.3f;
            ___m_swimTimer = 0f;
        }
    }
}

// UpdateSwimming runs after the fixed-motion state on some Valheim 1.0
// builds. Reasserting the native timers here prevents that later method from
// expiring the swim timer and applying the surface impulse while the target
// depth is already underwater.
[HarmonyPatch(typeof(Character), "UpdateSwimming")]
internal static class DivingSwimmingTimerPatch
{
    private static void Postfix(Character __instance, ref float ___m_lastGroundTouch, ref float ___m_swimTimer)
    {
        if (__instance is not Player player || !Plugin.IsFeatureEnabled(Plugin.Diving) ||
            !Plugin.IsLocalPlayer(player) ||
            (player.IsOnGround() && !DivingPatch.IsDiveGroundContact(player)) || player.IsDead()) return;
        if (!DivingPatch.HasDiveTarget(player) && !DivingPatch.IsUnderwater) return;

        ___m_lastGroundTouch = 0.3f;
        ___m_swimTimer = 0f;
        player.m_swimDepth = Mathf.Clamp(player.m_swimDepth, 2.5f, 20f);
    }
}

[HarmonyPatch(typeof(GameCamera), "UpdateCamera")]
internal static class DivingCameraPatch
{
    private static readonly Dictionary<int, float> OriginalWaterDistance = new();
    private static readonly Dictionary<int, float> OriginalMaxDistance = new();

    private static void Prefix(GameCamera __instance, Camera ___m_camera)
    {
        Apply(__instance, ___m_camera);
    }

    private static void Postfix(GameCamera __instance, Camera ___m_camera)
    {
        Apply(__instance, ___m_camera);
    }

    private static void Apply(GameCamera __instance, Camera ___m_camera)
    {
        Player player = Player.m_localPlayer;
        if (!Plugin.IsFeatureEnabled(Plugin.Diving) || player == null || ___m_camera == null) return;

        int id = __instance.GetInstanceID();
        if (!OriginalWaterDistance.ContainsKey(id)) OriginalWaterDistance[id] = __instance.m_minWaterDistance;
        if (!OriginalMaxDistance.ContainsKey(id)) OriginalMaxDistance[id] = __instance.m_maxDistance;

        bool localWaterPlayer = DivingPatch.IsLocalWaterPlayer(player);
        bool targetUnderwater = localWaterPlayer && DivingPatch.HasDiveTarget(player);
        bool cameraUnderwater = localWaterPlayer &&
                                ___m_camera.transform.position.y < player.GetLiquidLevel() - 0.05f;

        // BetterDiving applies the minimum-water override before the game's
        // camera solve and keeps it in a postfix as well. The prefix matters
        // on Valheim 1.0 because UpdateCamera can clamp the camera back above
        // the player during the same frame; the postfix protects against
        // camera mods that run later in the chain.
        if (localWaterPlayer)
        {
            __instance.m_minWaterDistance = -5000f;
            __instance.m_maxDistance = targetUnderwater || cameraUnderwater
                ? Mathf.Min(3f, OriginalMaxDistance[id])
                : OriginalMaxDistance[id];
        }
        else
        {
            __instance.m_minWaterDistance = OriginalWaterDistance[id];
            __instance.m_maxDistance = OriginalMaxDistance[id];
        }
    }
}

[HarmonyPatch(typeof(Humanoid), "Pickup")]
internal static class CurrencyPickupPatch
{
    [HarmonyPriority(Priority.LowerThanNormal)]
    private static bool Prefix(Humanoid __instance, GameObject go, bool autoPickupDelay, bool __runOriginal, ref bool __result)
    {
        // If another pickup patch already handled this drop, do not credit it
        // a second time. This also makes leaving an old CurrencyPocket copy in
        // the plugin folder fail closed instead of duplicating the balance.
        if (!__runOriginal || !Plugin.IsFeatureEnabled(Plugin.CurrencyPocket) || __instance is not Player player || go == null || player.IsTeleporting()) return true;
        ItemDrop drop = go.GetComponent<ItemDrop>();
        if (drop == null || drop.m_itemData?.m_shared == null || drop.m_itemData.m_shared.m_name != Plugin.CoinToken) return true;
        if (drop.m_nview == null || drop.m_nview.GetZDO() == null) return true;
        if (!drop.CanPickup(autoPickupDelay)) return true;
        ZDO dropZdo = drop.m_nview.GetZDO();
        // Pickup can be entered twice in one AutoPickup pass, and two clients
        // can receive the same drop before the network destroy arrives. Mark
        // the authoritative ZDO before changing the pocket balance so a coin
        // stack can never be counted twice.
        if (dropZdo.GetBool(Plugin.CoinConsumedKey))
        {
            __result = true;
            return false;
        }
        CurrencyAutoPickupContextPatch.Active = false;
        dropZdo.Set(Plugin.CoinConsumedKey, true);
        if (drop.m_itemData.m_dropPrefab == null && ObjectDB.instance != null)
            drop.m_itemData.m_dropPrefab = ObjectDB.instance.GetItemPrefab(Utils.GetPrefabName(go));
        int amount = drop.m_itemData.m_stack;
        Plugin.SetPocketCoins(player, Plugin.GetPocketCoins(player) + amount);
        if (ZNetScene.instance != null) ZNetScene.instance.Destroy(go);
        if (player.m_pickupEffects != null) player.m_pickupEffects.Create(drop.transform.position, Quaternion.identity);
        player.ShowPickupMessage(drop.m_itemData, amount);
        __result = true;
        return false;
    }
}

[HarmonyPatch(typeof(Player), "AutoPickup")]
internal static class CurrencyAutoPickupContextPatch
{
    internal static bool Active;

    private static void Prefix()
    {
        Active = true;
    }

    private static void Finalizer()
    {
        Active = false;
    }
}

[HarmonyPatch(typeof(Inventory), "CanAddItem", typeof(ItemDrop.ItemData), typeof(int))]
internal static class CurrencyAutoPickupCapacityPatch
{
    private static void Postfix(ItemDrop.ItemData item, ref bool __result)
    {
        if (!__result && CurrencyAutoPickupContextPatch.Active && Plugin.IsFeatureEnabled(Plugin.CurrencyPocket) &&
            item?.m_shared != null && item.m_shared.m_name == Plugin.CoinToken)
            __result = true;
    }
}

internal sealed class CurrencyPocketDropTarget : MonoBehaviour, IPointerClickHandler, IDropHandler
{
    public void OnPointerClick(PointerEventData eventData)
    {
        // Valheim's InventoryGrid finishes a drag over UI with a pointer
        // click, not a Unity drop event. Only treat the click as a deposit
        // when a coin stack is actually being dragged; the old unconditional
        // click handler moved the whole inventory stack whenever the pocket
        // card was merely selected.
        Plugin.DepositDraggedCoins();
    }

    public void OnDrop(PointerEventData eventData)
    {
        // Some UI layouts raise OnDrop instead of OnPointerClick. The
        // DepositDraggedCoins guard is idempotent because it clears the drag
        // item after a successful transfer.
        Plugin.DepositDraggedCoins();
    }
}

[HarmonyPatch(typeof(StoreGui), "GetPlayerCoins")]
internal static class CurrencyStorePatch
{
    private static void Postfix(ref int __result)
    {
        if (Plugin.IsFeatureEnabled(Plugin.CurrencyPocket) && Player.m_localPlayer != null) __result += Plugin.GetPocketCoins(Player.m_localPlayer);
    }
}

[HarmonyPatch(typeof(InventoryGui), "Show")]
internal static class CurrencyUiPatch
{
    [HarmonyPriority(Priority.VeryLow)]
    private static void Postfix(InventoryGui __instance)
    {
        if (!Plugin.IsFeatureEnabled(Plugin.CurrencyPocket)) return;
        Plugin.CreatePocketUi(__instance);
        Plugin.SchedulePocketUiReposition(__instance);
        Plugin.UpdatePocketUi();
    }
}

[HarmonyPatch(typeof(InventoryGui), "Awake")]
internal static class CurrencyUiAwakePatch
{
    private static void Postfix(InventoryGui __instance)
    {
        if (Plugin.IsFeatureEnabled(Plugin.CurrencyPocket)) Plugin.CreatePocketUi(__instance);
    }
}

// Show/Awake can run before another UI mod finishes rebuilding the inventory.
// Retry only while the pocket is missing; this is cheap and makes the UI
// recover after inventory-layout changes and world/player transitions.
[HarmonyPatch(typeof(InventoryGui), "Update")]
internal static class CurrencyUiRecoveryPatch
{
    private static void Postfix(InventoryGui __instance)
    {
        Transform root = Plugin.GetPocketLayoutRoot(__instance);
        if (Plugin.IsFeatureEnabled(Plugin.CurrencyPocket) &&
            (Plugin.PocketUi == null || !Plugin.PocketUi ||
             (root != null && !Plugin.PocketUi.transform.IsChildOf(root)) ||
             (Plugin.PocketUi != null && Plugin.PocketUi.activeInHierarchy == false)))
            Plugin.CreatePocketUi(__instance);
    }
}

[HarmonyPatch(typeof(Game), "Start")]
internal static class SleepRpcRegistrationPatch
{
    private static ZRoutedRpc RegisteredRpc;

    private static void Postfix()
    {
        if (!Plugin.IsFeatureEnabled(Plugin.SleepSkip) || ZRoutedRpc.instance == null) return;
        if (RegisteredRpc == ZRoutedRpc.instance) return;
        RegisteredRpc = ZRoutedRpc.instance;
        ZRoutedRpc.instance.Register(nameof(SleepRpc.OpenPopup), new Action<long>(SleepRpc.OpenPopup));
        ZRoutedRpc.instance.Register(nameof(SleepRpc.VoteYes), new Action<long, long>(SleepRpc.VoteYes));
        ZRoutedRpc.instance.Register(nameof(SleepRpc.VoteNo), new Action<long, long>(SleepRpc.VoteNo));
        ZRoutedRpc.instance.Register(nameof(SleepRpc.UpdateDisplay), new Action<long, string>(SleepRpc.UpdateDisplay));
        ZRoutedRpc.instance.Register(nameof(SleepRpc.Reset), new Action<long>(SleepRpc.Reset));
        ZRoutedRpc.instance.Register(nameof(SleepRpc.Result), new Action<long, string>(SleepRpc.Result));
    }
}

internal static class SleepRpc
{
    internal static void OpenPopup(long sender)
    {
        if (Player.m_localPlayer == null) return;
        if (Plugin.SleepPopupOpen) return;
        if (Plugin.SleepAutoAccept.Value)
        {
            Plugin.SleepPopupOpen = true;
            Vote(true);
            return;
        }
        Plugin.SleepPopupOpen = true;
        UnifiedPopup.Push(new YesNoPopup("Skip the night?", Plugin.SleepVoteBody(), () => Vote(true), () => Vote(false)));
    }

    private static void Vote(bool yes)
    {
        if (ZRoutedRpc.instance == null) return;
        ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, yes ? nameof(VoteYes) : nameof(VoteNo), ZNet.GetUID());
        if (Plugin.SleepPopupOpen && UnifiedPopup.instance != null) UnifiedPopup.Pop();
        Plugin.SleepPopupOpen = false;
    }

    internal static void VoteYes(long sender, long playerId)
    {
        if (ZNet.instance == null || !ZNet.instance.IsServer()) return;
        Plugin.SleepYes.Add(playerId);
        Plugin.SleepNo.Remove(playerId);
    }

    internal static void VoteNo(long sender, long playerId)
    {
        if (ZNet.instance == null || !ZNet.instance.IsServer()) return;
        Plugin.SleepNo.Add(playerId);
        Plugin.SleepYes.Remove(playerId);
    }

    internal static void UpdateDisplay(long sender, string data)
    {
        string[] parts = data.Split(',');
        if (parts.Length != 5) return;
        int.TryParse(parts[0], out Plugin.SleepInBed);
        int.TryParse(parts[1], out Plugin.SleepYesCount);
        int.TryParse(parts[2], out Plugin.SleepNoCount);
        int.TryParse(parts[3], out Plugin.SleepWaiting);
        int.TryParse(parts[4], out Plugin.SleepTotal);
        if (Plugin.SleepPopupOpen && UnifiedPopup.instance != null && UnifiedPopup.instance.bodyText != null)
            UnifiedPopup.instance.bodyText.text = Plugin.SleepVoteBody();
    }

    internal static void Reset(long sender)
    {
        if (Plugin.SleepPopupOpen && UnifiedPopup.instance != null) UnifiedPopup.Pop();
        Plugin.SleepPopupOpen = false;
        Plugin.SleepYes.Clear();
        Plugin.SleepNo.Clear();
        Plugin.SleepPopupSent.Clear();
        Plugin.SleepVoteActive = false;
        Plugin.SleepVoteStarted = DateTime.MinValue;
        Plugin.SleepWarningStarted = DateTime.MinValue;
        Plugin.LastSleepDisplay = null;
        Plugin.SleepInBed = Plugin.SleepYesCount = Plugin.SleepNoCount = Plugin.SleepWaiting = Plugin.SleepTotal = 0;
    }

    internal static void Result(long sender, string message)
    {
        if (Player.m_localPlayer != null) Player.m_localPlayer.Message(MessageHud.MessageType.Center, message);
        Reset(sender);
    }

    internal static void BroadcastReset()
    {
        if (ZRoutedRpc.instance != null) ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, nameof(Reset));
        Reset(0);
    }
}

[HarmonyPatch(typeof(Game), "EverybodyIsTryingToSleep")]
internal static class SleepSkipPatch
{
    private static readonly HashSet<long> CurrentPlayers = new();
    private static readonly HashSet<long> InBed = new();

    private static bool Prefix(ref bool __result)
    {
        if (!Plugin.IsFeatureEnabled(Plugin.SleepSkip) || ZNet.instance == null || !ZNet.instance.IsServer()) return true;
        List<ZDO> characters = ZNet.instance.GetAllCharacterZDOS();
        int total = characters.Count;
        if (total <= 0) { __result = false; return false; }
        InBed.Clear();
        foreach (ZDO zdo in characters) if (zdo.GetBool(ZDOVars.s_inBed)) InBed.Add(zdo.m_uid.UserID);
        int inBed = InBed.Count;
        if (inBed == 0 || (Plugin.SleepCooldownSeconds.Value > 0 && Plugin.LastSleepCompleted != DateTime.MinValue && DateTime.UtcNow < Plugin.LastSleepCompleted.AddSeconds(Plugin.SleepCooldownSeconds.Value)))
        {
            if (Plugin.SleepVoteActive || Plugin.SleepWarningStarted != DateTime.MinValue || Plugin.SleepVoteStarted != DateTime.MinValue) SleepRpc.BroadcastReset();
            __result = false;
            return false;
        }
        if (inBed >= total || (total > 1 && inBed < Plugin.SleepPlayersNeeded.Value))
        {
            if (Plugin.SleepVoteActive || Plugin.SleepWarningStarted != DateTime.MinValue || Plugin.SleepVoteStarted != DateTime.MinValue) SleepRpc.BroadcastReset();
            __result = inBed >= total;
            return false;
        }

        DateTime now = DateTime.UtcNow;
        if (Plugin.SleepWarningSeconds.Value > 0 && Plugin.SleepWarningStarted == DateTime.MinValue)
        {
            Plugin.SleepWarningStarted = now;
            __result = false;
            return false;
        }
        if (Plugin.SleepWarningSeconds.Value > 0 && now < Plugin.SleepWarningStarted.AddSeconds(Plugin.SleepWarningSeconds.Value))
        {
            __result = false;
            return false;
        }

        CurrentPlayers.Clear();
        foreach (ZNetPeer peer in ZNet.instance.m_peers) CurrentPlayers.Add(peer.m_characterID.UserID);
        if (!ZNet.instance.IsDedicated()) CurrentPlayers.Add(ZNet.GetUID());
        Plugin.SleepYes.IntersectWith(CurrentPlayers);
        Plugin.SleepNo.IntersectWith(CurrentPlayers);
        Plugin.SleepYes.ExceptWith(InBed);
        Plugin.SleepNo.ExceptWith(InBed);

        int explicitYes = Plugin.SleepYes.Count(id => !InBed.Contains(id));
        int explicitNo = Plugin.SleepNo.Count(id => !InBed.Contains(id));
        int waiting = Math.Max(0, total - inBed - explicitYes - explicitNo);
        bool timedOut = Plugin.SleepVoteStarted != DateTime.MinValue && Plugin.SleepVoteTimeoutSeconds.Value > 0 && now >= Plugin.SleepVoteStarted.AddSeconds(Plugin.SleepVoteTimeoutSeconds.Value);
        if (timedOut) waiting = 0;
        int effectiveTotal = Math.Max(1, total - (timedOut ? Math.Max(0, total - inBed - explicitYes - explicitNo) : 0));
        int yes = inBed + explicitYes;
        Plugin.SleepInBed = inBed;
        Plugin.SleepYesCount = explicitYes;
        Plugin.SleepNoCount = explicitNo;
        Plugin.SleepWaiting = waiting;
        Plugin.SleepTotal = total;

        if (!Plugin.SleepVoteActive)
        {
            Plugin.SleepVoteActive = true;
            Plugin.SleepVoteStarted = now;
        }

        // Send the popup to every eligible player that was not already
        // notified. This also handles a player joining after the vote starts.
        foreach (ZNetPeer peer in ZNet.instance.m_peers)
        {
            if (!InBed.Contains(peer.m_characterID.UserID) && Plugin.SleepPopupSent.Add(peer.m_characterID.UserID))
            {
                ZRoutedRpc.instance.InvokeRoutedRPC(peer.m_characterID.UserID, nameof(SleepRpc.OpenPopup));
            }
        }
        if (!ZNet.instance.IsDedicated() && !InBed.Contains(ZNet.GetUID())) SleepRpc.OpenPopup(0);
        string display = $"{inBed},{explicitYes},{explicitNo},{waiting},{total}";
        if (Plugin.LastSleepDisplay != display)
        {
            Plugin.LastSleepDisplay = display;
            foreach (ZNetPeer peer in ZNet.instance.m_peers) ZRoutedRpc.instance.InvokeRoutedRPC(peer.m_characterID.UserID, nameof(SleepRpc.UpdateDisplay), display);
            if (!ZNet.instance.IsDedicated()) SleepRpc.UpdateDisplay(0, display);
        }

        float ratio = (float)yes / effectiveTotal * 100f;
        int bestCase = yes + (timedOut ? 0 : waiting);
        if (ratio >= Plugin.SleepPercent.Value)
        {
            Plugin.LastSleepCompleted = now;
            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, nameof(SleepRpc.Result), "Sleep vote passed.");
            __result = true;
            return false;
        }
        if ((float)bestCase / effectiveTotal * 100f < Plugin.SleepPercent.Value)
        {
            Plugin.LastSleepCompleted = now;
            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, "SleepStop");
            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, nameof(SleepRpc.Result), "Sleep vote failed.");
            __result = false;
            return false;
        }
        __result = false;
        return false;
    }
}

