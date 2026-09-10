using System.Diagnostics;
using HarmonyLib;

namespace SwmarlyValheimQOL {
    // InventoryGui.UpdateContainer still performs a local ownership check on
    // some Valheim 1.0 builds. The container has already passed the server's
    // native open response at this point, so make only that one GUI check see
    // the opened container as usable. Other IsOwner callers retain the real
    // network ownership result used by the transaction RPCs.
    [HarmonyPatch(typeof(Container), nameof(Container.IsOwner))]
    internal static class MultiUserChestGuiOwnerPatch {
        private static bool Prefix(Container __instance, ref bool __result) {
            if (!Plugin.IsFeatureEnabled(Plugin.MultiUserChests) || __instance == null || __instance.IgnoreInventory())
                return true;

            InventoryGui gui = InventoryGui.instance;
            if (gui == null || gui.m_currentContainer != __instance) return true;

            StackTrace stack = new StackTrace();
            for (int i = 2; i < stack.FrameCount && i < 10; ++i) {
                if (stack.GetFrame(i)?.GetMethod()?.Name == nameof(InventoryGui.UpdateContainer)) {
                    __result = true;
                    return false;
                }
            }

            return true;
        }
    }
}
