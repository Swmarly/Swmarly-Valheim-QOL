using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace SwmarlyValheimQOL;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
[BepInProcess("valheim.exe")]
[BepInProcess("valheim.x86_64")]
public sealed class Plugin : BaseUnityPlugin
{
    public const string PluginGuid = "Swmarly.ValheimQOL";
    public const string PluginName = "Swmarly Valheim QOL";
    public const string PluginVersion = "1.0.0";
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
    internal static TextMeshProUGUI PocketText;

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
    internal static DateTime SleepWarningStarted = DateTime.MinValue;
    internal static int SleepLastWarning = -1;

    private void Awake()
    {
        Instance = this;
        BindConfig();
        Harmony.PatchAll(typeof(Plugin).Assembly);
        Logger.LogInfo($"{PluginName} {PluginVersion} loaded for Valheim 1.0.");
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

    internal static int GetPocketCoins(Player player)
    {
        if (player == null || player.m_customData == null || !player.m_customData.TryGetValue(CoinKey, out string value)) return 0;
        return int.TryParse(value, out int coins) ? Math.Max(0, coins) : 0;
    }

    internal static void SetPocketCoins(Player player, int coins)
    {
        if (player == null) return;
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

    internal static void CreatePocketUi(InventoryGui gui)
    {
        if (!IsFeatureEnabled(CurrencyPocket) || PocketUi != null || gui == null || gui.m_player == null) return;
        Transform inventoryRoot = gui.m_player.transform;
        Transform armor = inventoryRoot.Find("Armor");
        Transform weight = inventoryRoot.Find("Weight");
        if (armor == null) return;

        PocketUi = Object.Instantiate(armor.gameObject, inventoryRoot);
        PocketUi.name = "SwmarlyValheimQOL_CurrencyPocket";
        RectTransform pocketRect = PocketUi.GetComponent<RectTransform>();
        RectTransform armorRect = armor.GetComponent<RectTransform>();
        RectTransform weightRect = weight == null ? null : weight.GetComponent<RectTransform>();
        if (pocketRect != null && armorRect != null)
        {
            pocketRect.anchoredPosition = weightRect == null
                ? armorRect.anchoredPosition + new Vector2(0f, -42f)
                : new Vector2(armorRect.anchoredPosition.x, (armorRect.anchoredPosition.y + weightRect.anchoredPosition.y) * 0.5f);
        }

        Transform icon = PocketUi.transform.Find("armor_icon");
        GameObject coins = ObjectDB.instance == null ? null : ObjectDB.instance.GetItemPrefab(CoinPrefab);
        if (icon != null && coins != null)
        {
            Image image = icon.GetComponent<Image>();
            ItemDrop drop = coins.GetComponent<ItemDrop>();
            if (image != null && drop != null) image.sprite = drop.m_itemData.GetIcon();
        }
        PocketText = PocketUi.transform.Find("ac_text")?.GetComponent<TextMeshProUGUI>();
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
        }
    }
}

[HarmonyPatch(typeof(ItemDrop), "Awake")]
internal static class FloatingItemsPatch
{
    private static void Postfix(ItemDrop __instance)
    {
        if (!Plugin.IsFeatureEnabled(Plugin.FloatItems)) return;
        GameObject go = __instance.gameObject;
        if (go.GetComponent<Rigidbody>() == null && go.GetComponentInChildren<Rigidbody>() == null) return;
        if (go.GetComponent<ZNetView>() == null) return;
        Floating floating = go.GetComponent<Floating>() ?? go.AddComponent<Floating>();
        floating.m_force = Plugin.FloatForce.Value;
        floating.m_damping = Plugin.FloatDamping.Value;
    }
}

[HarmonyPatch(typeof(Character), "IsSwimming")]
internal static class EquipmentInWaterPatch
{
    private static bool Prefix(Character __instance, ref bool __result, float ___m_swimTimer)
    {
        if (!Plugin.IsFeatureEnabled(Plugin.EquipmentInWater) || !__instance.IsPlayer() || ___m_swimTimer >= 0.5f) return true;
        StackTrace trace = new();
        for (int i = 2; i < trace.FrameCount && i < 10; i++)
        {
            string name = trace.GetFrame(i)?.GetMethod()?.Name ?? string.Empty;
            if (name == "EquipItem" || name == "UpdateEquipment")
            {
                __result = false;
                return false;
            }
        }
        return true;
    }
}

[HarmonyPatch(typeof(Player), "UseHotbarItem")]
internal static class EquipWhileRunningPatch
{
    private static bool Prefix(Player __instance, int index)
    {
        if (Plugin.IsFeatureEnabled(Plugin.EquipWhileRunning) || !__instance.IsRunning()) return true;
        ItemDrop.ItemData item = __instance.GetInventory().GetItemAt(index - 1, 0);
        return item == null || !item.IsEquipable();
    }
}

[HarmonyPatch(typeof(Player), "UseStamina")]
internal static class NoStaminaCostsPatch
{
    private static void Prefix(Player __instance, ref float v)
    {
        if (!Plugin.IsFeatureEnabled(Plugin.NoStaminaCosts) || Plugin.StaminaCostMode.Value <= 0) return;
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
    private static float nextSitHeal;

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
            if (Plugin.SwimSprint.Value && (ZInput.GetButton("Run") || ZInput.GetButton("JoyRun"))) speed *= 1.25f;
            __instance.m_swimSpeed = speed;
        }
    }

    private static void Postfix(Player __instance)
    {
        if (Plugin.IsFeatureEnabled(Plugin.SwimImprovements) && __instance.IsSwimming() && __instance.GetMoveDir().magnitude < 0.1f && Plugin.SwimIdleStaminaPerSecond.Value > 0f)
            __instance.UseStamina(-Plugin.SwimIdleStaminaPerSecond.Value * Time.deltaTime);

        if (Plugin.IsFeatureEnabled(Plugin.Diving) && __instance.IsSwimming())
        {
            Rigidbody body = __instance.GetComponent<Rigidbody>();
            if (body != null)
            {
                float targetY = 0f;
                if (Plugin.DiveKey.Value.IsDown()) targetY = -Plugin.DiveSpeed.Value;
                else if (Plugin.SurfaceKey.Value.IsDown()) targetY = Plugin.DiveSpeed.Value;
                if (Mathf.Abs(targetY) > 0.01f)
                {
                    Vector3 velocity = body.velocity;
                    velocity.y = Mathf.MoveTowards(velocity.y, targetY, Plugin.DiveSpeed.Value * 4f * Time.deltaTime);
                    body.velocity = velocity;
                    __instance.UseStamina(Plugin.DiveStaminaPerSecond.Value * Time.deltaTime);
                }
            }
        }

        if (Plugin.IsFeatureEnabled(Plugin.SitRegeneration) && __instance.IsSitting() && Time.time >= nextSitHeal && __instance.GetHealth() < __instance.GetMaxHealth())
        {
            nextSitHeal = Time.time + 1f;
            __instance.Heal(Plugin.SitHealPerSecond.Value);
        }
    }
}

[HarmonyPatch(typeof(Humanoid), "Pickup")]
internal static class CurrencyPickupPatch
{
    private static bool Prefix(Humanoid __instance, GameObject go, bool autoPickupDelay, ref bool __result)
    {
        if (!Plugin.IsFeatureEnabled(Plugin.CurrencyPocket) || __instance is not Player player || go == null || player.IsTeleporting()) return true;
        ItemDrop drop = go.GetComponent<ItemDrop>();
        if (drop == null || drop.m_itemData?.m_shared == null || drop.m_itemData.m_shared.m_name != Plugin.CoinToken) return true;
        if (!drop.CanPickup(autoPickupDelay)) return true;
        int amount = drop.m_itemData.m_stack;
        Plugin.SetPocketCoins(player, Plugin.GetPocketCoins(player) + amount);
        if (ZNetScene.instance != null) ZNetScene.instance.Destroy(go);
        player.ShowPickupMessage(drop.m_itemData, amount);
        __result = true;
        return false;
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
    private static void Postfix(InventoryGui __instance)
    {
        if (!Plugin.IsFeatureEnabled(Plugin.CurrencyPocket)) return;
        Plugin.CreatePocketUi(__instance);
        Plugin.UpdatePocketUi();
    }
}

[HarmonyPatch(typeof(Game), "Start")]
internal static class SleepRpcRegistrationPatch
{
    private static void Postfix()
    {
        if (!Plugin.IsFeatureEnabled(Plugin.SleepSkip) || ZRoutedRpc.instance == null) return;
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
        if (Plugin.SleepAutoAccept.Value)
        {
            Vote(true);
            return;
        }
        UnifiedPopup.Push(new YesNoPopup("Skip the night?", Plugin.SleepVoteBody(), () => Vote(true), () => Vote(false)));
    }

    private static void Vote(bool yes)
    {
        if (ZRoutedRpc.instance == null) return;
        ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, yes ? nameof(VoteYes) : nameof(VoteNo), ZNet.GetUID());
        if (UnifiedPopup.instance != null) UnifiedPopup.Pop();
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
    }

    internal static void Reset(long sender)
    {
        Plugin.SleepYes.Clear();
        Plugin.SleepNo.Clear();
        Plugin.SleepPopupSent.Clear();
        Plugin.SleepVoteActive = false;
        Plugin.SleepVoteStarted = DateTime.MinValue;
        Plugin.SleepWarningStarted = DateTime.MinValue;
        Plugin.SleepInBed = Plugin.SleepYesCount = Plugin.SleepNoCount = Plugin.SleepWaiting = Plugin.SleepTotal = 0;
    }

    internal static void Result(long sender, string message)
    {
        if (Player.m_localPlayer != null) Player.m_localPlayer.Message(MessageHud.MessageType.Center, message);
        Reset(sender);
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
            if (Plugin.SleepVoteActive) SleepRpc.Reset(0);
            __result = false;
            return false;
        }
        if (inBed >= total || (total > 1 && inBed < Plugin.SleepPlayersNeeded.Value))
        {
            if (Plugin.SleepVoteActive) SleepRpc.Reset(0);
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
            foreach (ZNetPeer peer in ZNet.instance.m_peers)
            {
                if (!InBed.Contains(peer.m_characterID.UserID))
                {
                    ZRoutedRpc.instance.InvokeRoutedRPC(peer.m_characterID.UserID, nameof(SleepRpc.OpenPopup));
                    Plugin.SleepPopupSent.Add(peer.m_characterID.UserID);
                }
            }
            if (!ZNet.instance.IsDedicated() && !InBed.Contains(ZNet.GetUID())) SleepRpc.OpenPopup(0);
        }
        string display = $"{inBed},{explicitYes},{explicitNo},{waiting},{total}";
        foreach (ZNetPeer peer in ZNet.instance.m_peers) ZRoutedRpc.instance.InvokeRoutedRPC(peer.m_characterID.UserID, nameof(SleepRpc.UpdateDisplay), display);
        if (!ZNet.instance.IsDedicated()) SleepRpc.UpdateDisplay(0, display);

        float ratio = (float)yes / effectiveTotal * 100f;
        int bestCase = yes + (timedOut ? 0 : waiting);
        if (ratio >= Plugin.SleepPercent.Value)
        {
            Plugin.LastSleepCompleted = now;
            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, nameof(SleepRpc.Result), "Sleep vote passed.");
            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, nameof(SleepRpc.Reset));
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

