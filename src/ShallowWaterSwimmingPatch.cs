using HarmonyLib;
using UnityEngine;

namespace SwmarlyValheimQOL;

// A shallow water column is not a valid swimming volume in Valheim. Without
// this guard, jumping from the seabed/ground can make the native controller
// enter swimming even though the player cannot actually swim there.
[HarmonyPatch(typeof(Character), nameof(Character.IsSwimming))]
internal static class ShallowWaterSwimmingStatePatch
{
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(Character __instance, ref bool __result)
    {
        if (__instance is not Player player ||
            !Plugin.IsFeatureEnabled(Plugin.Diving) ||
            !Plugin.IsLocalPlayer(player) ||
            player.IsDead())
            return true;

        if (!IsShallowWater(player))
            return true;

        // A dive target left over from a previous deep-water swim must not
        // survive into shallow water and re-enable the swimming state.
        DivingPatch.DiveToggle = false;
        DivingPatch.IsUnderwater = false;
        player.m_swimDepth = 1.6f;

        __result = false;
        return false;
    }

    internal static bool IsShallowWater(Player player)
    {
        return DivingPatch.HasPhysicalLiquid(player) &&
               !DivingPatch.HasNativeSwimDepth(player);
    }
}

// Keep the native movement method on its walking path for the same frame as
// the state guard. This matters when the player jumps from shallow water:
 // UpdateMotion can otherwise consume a stale swim timer before the next
 // IsSwimming query, causing a one-frame swim transition.
[HarmonyPatch(typeof(Character), nameof(Character.UpdateMotion))]
internal static class ShallowWaterMovementPatch
{
    [HarmonyPriority(Priority.First)]
    private static void Prefix(Character __instance, ref float ___m_swimTimer)
    {
        if (__instance is not Player player ||
            !Plugin.IsFeatureEnabled(Plugin.Diving) ||
            !Plugin.IsLocalPlayer(player) ||
            player.IsDead() ||
            !ShallowWaterSwimmingStatePatch.IsShallowWater(player))
            return;

        DivingPatch.DiveToggle = false;
        DivingPatch.IsUnderwater = false;
        player.m_swimDepth = 1.6f;

        // Character.IsSwimming() is true while this timer is below 0.5.
        // Leaving it at the native walking boundary prevents the stale swim
        // timer from turning a shallow-water jump into a swim.
        ___m_swimTimer = 0.5f;
    }
}
