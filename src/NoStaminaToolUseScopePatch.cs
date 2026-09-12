using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace SwmarlyValheimQOL;

// Marks the synchronous Valheim action that is consuming stamina. The old
// implementation only looked at the equipped item, which also matched the
// running stamina call while a hammer or hoe was held.
[HarmonyPatch]
internal static class NoStaminaToolUseScopePatch
{
    private static readonly HashSet<string> ToolActionNames = new(StringComparer.Ordinal)
    {
        "UseItem",
        "UseItemSwitch",
        "Attack",
        // Player.UpdatePlacement owns the actual hammer/hoe/cultivator
        // stamina call in Valheim. The call happens after PlacePiece,
        // RemovePiece, or Repair returns, so those helpers alone are too
        // narrow to keep the scope alive until UseStamina is reached.
        "UpdatePlacement",
        "PlacePiece",
        "RemovePiece",
        "Repair",
        "RaiseTerrain",
        "LowerTerrain",
        "LevelTerrain",
        "Flatten",
        "Cultivate",
        "Plant",
        "PaveRoad",
        "SmoothTerrain"
    };

    [ThreadStatic]
    private static int ScopeDepth;

    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (Type type in new[] { typeof(Player), typeof(Humanoid) })
        {
            foreach (MethodInfo method in AccessTools.GetDeclaredMethods(type))
            {
                if (ToolActionNames.Contains(method.Name))
                    yield return method;
            }
        }
    }

    private static void Prefix(object __instance)
    {
        if (__instance is Player player && IsToolEquipped(player))
            ScopeDepth++;
    }

    private static Exception Finalizer(Exception __exception)
    {
        if (ScopeDepth > 0)
            ScopeDepth--;
        return __exception;
    }

    internal static bool IsActive(Player player)
    {
        if (ScopeDepth > 0)
            return true;

        // Covers tool-use paths introduced by Valheim 1.0 updates without
        // making every positive UseStamina call free while a tool is held.
        StackTrace stack = new();
        for (int i = 2; i < stack.FrameCount && i < 16; i++)
        {
            MethodBase method = stack.GetFrame(i)?.GetMethod();
            if (method != null && ToolActionNames.Contains(method.Name))
                return true;
        }

        return false;
    }

    internal static bool IsToolEquipped(Player player)
    {
        if (player == null)
            return false;

        return IsTool(player.GetRightItem()) || IsTool(player.GetLeftItem());
    }

    private static bool IsTool(ItemDrop.ItemData item)
    {
        string name = item?.m_shared?.m_name ?? string.Empty;
        return name is "$item_hammer" or "$item_hoe" or "$item_cultivator";
    }
}
