using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
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
    public const string PluginVersion = "1.0.4";
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

    internal static ConfigEntry<int> SleepPercent;
    internal static ConfigEntry<int> SleepPlayersNeeded;
    internal static ConfigEntry<int> SleepWarningSeconds;
    internal static ConfigEntry<int> SleepVoteTimeoutSeconds;
    internal static ConfigEntry<int> SleepCooldownSeconds;
    internal static ConfigEntry<bool> SleepAutoAccept;

    internal const string CoinKey = "SwmarlyValheimQOL_Coins";
    internal const string CoinPrefab = "Coins";
    internal const string CoinToken = "$item_coins";
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
        Harmony.PatchAll(typeof(Plugin).Assembly);
        Logger.LogInfo($"{PluginName} {PluginVersion} loaded for Valheim 1.0 in process '{Process.GetCurrentProcess().ProcessName}'.");
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

        FloatForce = Config.Bind("Floating items", "Buoyancy force", 0.5f, new ConfigDescription("Native Floating force applied below the surface.", new AcceptableValueRange<float>(0.05f, 3f)));
        FloatDamping = Config.Bind("Floating items", "Damping", 0.05f, new ConfigDescription("Velocity damping while an item is floating.", new AcceptableValueRange<float>(0f, 0.5f)));

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
        if (player == null || player.m_customData == null || !player.m_customData.TryGetValue(CoinKey, out string value)) return 0;
        return int.TryParse(value, out int coins) ? Math.Max(0, coins) : 0;
    }

    internal static void SetPocketCoins(Player player, int coins)
    {
        if (player == null || player.m_customData == null) return;
        player.m_customData[CoinKey] = Math.Max(0, coins).ToString();
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
        player.GetInventory().AddItem(prefab, coins);
        SetPocketCoins(player, 0);
    }

    internal static void DepositInventoryCoins()
    {
        Player player = Player.m_localPlayer;
        if (player == null) return;
        Inventory inventory = player.GetInventory();
        int coins = inventory.CountItems(CoinToken);
        if (coins <= 0) return;

        inventory.RemoveItem(CoinToken, coins);
        SetPocketCoins(player, GetPocketCoins(player) + coins);
    }

    internal static bool DepositDraggedCoins()
    {
        Player player = Player.m_localPlayer;
        InventoryGui gui = InventoryGui.m_instance;
        if (player == null || gui == null || gui.m_dragItem == null || gui.m_dragInventory == null || gui.m_dragAmount <= 0) return false;
        if (gui.m_dragItem.m_shared == null || gui.m_dragItem.m_shared.m_name != CoinToken) return false;

        int amount = Mathf.Min(gui.m_dragAmount, gui.m_dragItem.m_stack);
        if (amount <= 0) return false;
        gui.m_dragInventory.RemoveItem(gui.m_dragItem, amount);
        SetPocketCoins(player, GetPocketCoins(player) + amount);
        gui.SetupDragItem(null, null, 1);
        return true;
    }

    internal static void CreatePocketUi(InventoryGui gui)
    {
        if (!IsFeatureEnabled(CurrencyPocket) || gui == null || gui.m_player == null) return;
        Transform inventoryRoot = gui.m_player.transform;
        Transform armor = inventoryRoot.Find("Armor");
        Transform weight = inventoryRoot.Find("Weight");
        if (armor == null) return;

        if (PocketUi != null)
        {
            RepositionPocketUi(inventoryRoot, armor, weight);
            SetPocketIcon();
            return;
        }

        PocketUi = Object.Instantiate(armor.gameObject, inventoryRoot);
        PocketUi.name = "SwmarlyValheimQOL_CurrencyPocket";
        PocketUi.AddComponent<CurrencyPocketDropTarget>();
        RepositionPocketUi(inventoryRoot, armor, weight);
        SetPocketIcon();
        Transform text = Utils.FindChild(PocketUi.transform, "ac_text");
        PocketText = text == null ? null : text.GetComponent<TextMeshProUGUI>();
        if (PocketText != null) PocketText.text = GetPocketCoins(Player.m_localPlayer).ToString();

        if (gui.m_takeAllButton != null)
        {
            PocketExtractButton = Object.Instantiate(gui.m_takeAllButton, PocketUi.transform);
            PocketExtractButton.name = "SwmarlyValheimQOL_ExtractCoins";
            TextMeshProUGUI buttonText = PocketExtractButton.GetComponentInChildren<TextMeshProUGUI>();
            if (buttonText != null) buttonText.text = "↗";
            RectTransform buttonRect = PocketExtractButton.GetComponent<RectTransform>();
            if (buttonRect != null) buttonRect.localPosition = new Vector3(2.5f, -20f, 0f);
            PocketExtractButton.transform.localScale = new Vector3(0.4f, 0.4f, 1f);
            PocketExtractButton.onClick = new Button.ButtonClickedEvent();
            PocketExtractButton.onClick.AddListener(ExtractPocketCoins);

            PocketDepositButton = Object.Instantiate(gui.m_takeAllButton, PocketUi.transform);
            PocketDepositButton.name = "SwmarlyValheimQOL_DepositCoins";
            TextMeshProUGUI depositText = PocketDepositButton.GetComponentInChildren<TextMeshProUGUI>();
            if (depositText != null) depositText.text = "↓";
            RectTransform depositRect = PocketDepositButton.GetComponent<RectTransform>();
            if (depositRect != null) depositRect.localPosition = new Vector3(-11f, -20f, 0f);
            PocketDepositButton.transform.localScale = new Vector3(0.4f, 0.4f, 1f);
            PocketDepositButton.onClick = new Button.ButtonClickedEvent();
            PocketDepositButton.onClick.AddListener(DepositInventoryCoins);
        }
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
        // clone, then wait one frame more for layout rebuilds.
        yield return null;
        yield return new WaitForEndOfFrame();
        if (gui != null && gui.m_player != null)
        {
            Transform armor = gui.m_player.transform.Find("Armor");
            Transform weight = gui.m_player.transform.Find("Weight");
            if (armor != null) RepositionPocketUi(gui.m_player.transform, armor, weight);
            SetPocketIcon();
            UpdatePocketUi();
        }
        PocketUiRepositionCoroutine = null;
    }

    private static void RepositionPocketUi(Transform inventoryRoot, Transform armor, Transform weight)
    {
        RectTransform pocketRect = PocketUi == null ? null : PocketUi.GetComponent<RectTransform>();
        RectTransform armorRect = armor.GetComponent<RectTransform>();
        RectTransform weightRect = weight == null ? null : weight.GetComponent<RectTransform>();
        if (pocketRect == null || armorRect == null) return;

        // Expanded inventory mods reposition Armor and Weight after vanilla
        // layout. Place the pocket from the final rendered bounds instead of
        // relying on a fixed -234 offset that overlaps their number fields.
        Transform reference = weight != null ? weight : armor;
        RectTransform referenceRect = reference.GetComponent<RectTransform>();
        if (referenceRect == null || referenceRect.parent != pocketRect.parent) return;
        Canvas.ForceUpdateCanvases();

        Vector3[] referenceCorners = new Vector3[4];
        Vector3[] pocketCorners = new Vector3[4];
        referenceRect.GetWorldCorners(referenceCorners);
        pocketRect.GetWorldCorners(pocketCorners);
        Transform parent = pocketRect.parent;
        float referenceBottom = parent.InverseTransformPoint(referenceCorners[0]).y;
        float referenceCenterX = parent.InverseTransformPoint((referenceCorners[0] + referenceCorners[2]) * 0.5f).x;
        Vector3 pocketMin = parent.InverseTransformPoint(pocketCorners[0]);
        Vector3 pocketMax = parent.InverseTransformPoint(pocketCorners[2]);
        float pocketHeight = Mathf.Abs(pocketMax.y - pocketMin.y);
        float referenceGap = Mathf.Max(8f, pocketHeight * 0.12f);
        float desiredCenterY = referenceBottom - referenceGap - pocketHeight * 0.5f;
        Vector3 desiredPivot = parent.InverseTransformPoint(pocketRect.position);
        desiredPivot.x = referenceCenterX;
        desiredPivot.y = desiredCenterY + (pocketRect.pivot.y - 0.5f) * pocketHeight;
        pocketRect.position = parent.TransformPoint(desiredPivot);
        pocketRect.SetSiblingIndex(Mathf.Min(reference.GetSiblingIndex() + 1, pocketRect.parent.childCount - 1));
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
        if (go.GetComponent<Rigidbody>() == null && go.GetComponentInChildren<Rigidbody>() == null) return;
        if (go.GetComponent<ZNetView>() == null && go.GetComponentInChildren<ZNetView>() == null) return;
        Floating floating = go.GetComponent<Floating>() ?? go.AddComponent<Floating>();
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

internal static class EquipmentMovementSupport
{
    private static readonly FieldInfo RunIntentField = AccessTools.Field(typeof(Character), "m_run");
    private static readonly FieldInfo RunningField = AccessTools.Field(typeof(Character), "m_running");

    private static bool GetFlag(FieldInfo field, Character character)
    {
        return field != null && field.GetValue(character) is bool value && value;
    }

    private static void SetFlag(FieldInfo field, Character character, bool value)
    {
        if (field != null) field.SetValue(character, value);
    }

    internal static bool IsRunning(Player player)
    {
        if (player == null) return false;
        return GetFlag(RunIntentField, player) || GetFlag(RunningField, player) ||
               (Plugin.IsLocalPlayer(player) && (ZInput.GetButton("Run") || ZInput.GetButton("JoyRun")));
    }

    internal sealed class State
    {
        internal bool Changed;
        internal bool OldRun;
        internal bool OldRunning;
    }

    internal static State SuspendRunning(Player player)
    {
        State state = new()
        {
            OldRun = GetFlag(RunIntentField, player),
            OldRunning = GetFlag(RunningField, player)
        };
        state.Changed = state.OldRun || state.OldRunning;
        if (state.Changed)
        {
            // Valheim checks m_run in input/equipment code and m_running in
            // movement code. Clearing only one is why the previous patch did
            // not work reliably during a sprint transition.
            SetFlag(RunIntentField, player, false);
            SetFlag(RunningField, player, false);
        }
        return state;
    }

    internal static void RestoreRunning(Player player, State state)
    {
        if (player == null || state == null || !state.Changed) return;
        SetFlag(RunIntentField, player, state.OldRun);
        SetFlag(RunningField, player, state.OldRunning);
    }
}

[HarmonyPatch(typeof(Player), "UseHotbarItem")]
internal static class EquipWhileRunningHotbarPatch
{
    private static bool Prefix(Player __instance, int index)
    {
        if (!Plugin.IsFeatureEnabled(Plugin.EquipWhileRunning) || !EquipmentMovementSupport.IsRunning(__instance)) return true;
        ItemDrop.ItemData item = __instance.GetInventory().GetItemAt(index - 1, 0);
        if (item == null) return true;

        EquipmentMovementSupport.State state = EquipmentMovementSupport.SuspendRunning(__instance);
        try
        {
            // Bypass Player.UseHotbarItem's early running check and enter the
            // same UseItem path as a normal hotbar activation.
            __instance.UseItem(__instance.GetInventory(), item, false);
        }
        finally
        {
            EquipmentMovementSupport.RestoreRunning(__instance, state);
        }
        return false;
    }
}

[HarmonyPatch(typeof(Humanoid), "UseItem")]
internal static class EquipWhileRunningUseItemPatch
{
    private static void Prefix(Humanoid __instance, Inventory inventory, ItemDrop.ItemData item, ref EquipmentMovementSupport.State __state)
    {
        if (!Plugin.IsFeatureEnabled(Plugin.EquipWhileRunning) || __instance is not Player player || !EquipmentMovementSupport.IsRunning(player)) return;
        if (inventory != null && inventory != player.GetInventory()) return;
        __state = EquipmentMovementSupport.SuspendRunning(player);
    }

    private static void Postfix(Humanoid __instance, EquipmentMovementSupport.State __state)
    {
        if (__instance is Player player) EquipmentMovementSupport.RestoreRunning(player, __state);
    }
}

[HarmonyPatch(typeof(Humanoid), "EquipItem")]
internal static class EquipWhileRunningEquipItemPatch
{
    private static void Prefix(Humanoid __instance, ref EquipmentMovementSupport.State __state)
    {
        if (!Plugin.IsFeatureEnabled(Plugin.EquipWhileRunning) || __instance is not Player player || !EquipmentMovementSupport.IsRunning(player)) return;
        __state = EquipmentMovementSupport.SuspendRunning(player);
    }

    private static void Postfix(Humanoid __instance, EquipmentMovementSupport.State __state)
    {
        if (__instance is Player player) EquipmentMovementSupport.RestoreRunning(player, __state);
    }
}

[HarmonyPatch(typeof(Humanoid), "EquipItem")]
internal static class EquipmentInWaterEquipPatch
{
    private sealed class State
    {
        internal bool Changed;
        internal float OldSwimTimer;
    }

    private static void Prefix(Humanoid __instance, ref float ___m_swimTimer, ref State __state)
    {
        if (!Plugin.IsFeatureEnabled(Plugin.EquipmentInWater) || __instance is not Player || ___m_swimTimer >= 0.5f) return;
        __state = new State { Changed = true, OldSwimTimer = ___m_swimTimer };
        // Humanoid.EquipItem rejects swimming players through IsSwimming().
        // Temporarily present a non-swimming state for the equip transaction,
        // then restore the real swimming timer immediately afterward.
        ___m_swimTimer = 1f;
    }

    private static void Postfix(ref float ___m_swimTimer, State __state)
    {
        if (__state != null && __state.Changed) ___m_swimTimer = __state.OldSwimTimer;
    }
}

[HarmonyPatch(typeof(Humanoid), "UpdateEquipment")]
internal static class EquipmentInWaterUpdatePatch
{
    private sealed class State
    {
        internal bool Changed;
        internal float OldSwimTimer;
    }

    private static void Prefix(Humanoid __instance, ref float ___m_swimTimer, ref State __state)
    {
        if (!Plugin.IsFeatureEnabled(Plugin.EquipmentInWater) || __instance is not Player || ___m_swimTimer >= 0.5f) return;
        __state = new State { Changed = true, OldSwimTimer = ___m_swimTimer };
        ___m_swimTimer = 1f;
    }

    private static void Postfix(ref float ___m_swimTimer, State __state)
    {
        if (__state != null && __state.Changed) ___m_swimTimer = __state.OldSwimTimer;
    }
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
    private static readonly Dictionary<int, float> BaseSwimSpeed = new();
    private static readonly Dictionary<int, float> NextSitHeal = new();

    private static void Prefix(Player __instance)
    {
        int id = __instance.GetInstanceID();
        if (Plugin.IsFeatureEnabled(Plugin.SneakSpeed))
        {
            if (!BaseCrouchSpeed.ContainsKey(id)) BaseCrouchSpeed[id] = __instance.m_crouchSpeed;
            float factor = __instance.m_skills == null ? 0f : __instance.m_skills.GetSkillFactor(Skills.SkillType.Sneak);
            __instance.m_crouchSpeed = BaseCrouchSpeed[id] * Mathf.Lerp(1f, Plugin.SneakSpeedMultiplier.Value, factor);
        }
        if (Plugin.IsFeatureEnabled(Plugin.SwimImprovements))
        {
            if (!BaseSwimSpeed.ContainsKey(id)) BaseSwimSpeed[id] = __instance.m_swimSpeed;
            float factor = __instance.m_skills == null ? 0f : __instance.m_skills.GetSkillFactor(Skills.SkillType.Swim);
            float speed = BaseSwimSpeed[id] * Mathf.Lerp(1f, Plugin.MaxSwimSpeedMultiplier.Value, factor);
            if (Plugin.SwimSprint.Value && Plugin.IsLocalPlayer(__instance) && (ZInput.GetButton("Run") || ZInput.GetButton("JoyRun"))) speed *= 1.25f;
            __instance.m_swimSpeed = speed;
        }
    }

    private static void Postfix(Player __instance)
    {
        if (Plugin.IsLocalPlayer(__instance) && Plugin.IsFeatureEnabled(Plugin.SwimImprovements) && __instance.IsSwimming() && __instance.GetMoveDir().magnitude < 0.1f && Plugin.SwimIdleStaminaPerSecond.Value > 0f)
            __instance.UseStamina(-Plugin.SwimIdleStaminaPerSecond.Value * Time.deltaTime);

        int id = __instance.GetInstanceID();
        if (Plugin.IsLocalPlayer(__instance) && Plugin.IsFeatureEnabled(Plugin.SitRegeneration) && __instance.IsSitting() &&
            (!NextSitHeal.TryGetValue(id, out float nextHeal) || Time.time >= nextHeal) && __instance.GetHealth() < __instance.GetMaxHealth())
        {
            NextSitHeal[id] = Time.time + 1f;
            __instance.Heal(Plugin.SitHealPerSecond.Value);
        }
    }
}

[HarmonyPatch(typeof(Character), "UpdateSwimming")]
internal static class DivingPatch
{
    private static void Postfix(Character __instance)
    {
        if (!Plugin.IsFeatureEnabled(Plugin.Diving) || __instance is not Player player || !Plugin.IsLocalPlayer(player) || !player.IsSwimming()) return;
        Rigidbody body = player.GetComponent<Rigidbody>();
        if (body == null) return;

        bool diving = Plugin.DiveKey.Value.IsPressed();
        bool surfacing = Plugin.SurfaceKey.Value.IsPressed();
        if (diving == surfacing) return;

        float direction = diving ? -1f : 1f;
        float speed = Plugin.DiveSpeed.Value;
        float dt = Mathf.Max(Time.fixedDeltaTime, 0.001f);
        Vector3 velocity = body.velocity;
        velocity.y = Mathf.MoveTowards(velocity.y, direction * speed, speed * 6f * dt);
        body.velocity = velocity;
        if (Plugin.DiveStaminaPerSecond.Value > 0f) player.UseStamina(Plugin.DiveStaminaPerSecond.Value * dt);
    }
}

[HarmonyPatch(typeof(Humanoid), "Pickup")]
internal static class CurrencyPickupPatch
{
    [HarmonyPriority(Priority.LowerThanNormal)]
    private static bool Prefix(Humanoid __instance, GameObject go, bool autoPickupDelay, ref bool __result)
    {
        if (!Plugin.IsFeatureEnabled(Plugin.CurrencyPocket) || __instance is not Player player || go == null || player.IsTeleporting()) return true;
        ItemDrop drop = go.GetComponent<ItemDrop>();
        if (drop == null || drop.m_itemData?.m_shared == null || drop.m_itemData.m_shared.m_name != Plugin.CoinToken) return true;
        if (drop.m_nview == null || drop.m_nview.GetZDO() == null) return true;
        if (!drop.CanPickup(autoPickupDelay)) return true;
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

internal sealed class CurrencyPocketDropTarget : MonoBehaviour, IPointerClickHandler
{
    public void OnPointerClick(PointerEventData eventData)
    {
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
