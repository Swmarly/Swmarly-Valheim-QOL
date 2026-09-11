using HarmonyLib;

namespace SwmarlyValheimQOL {
    [HarmonyPatch]
    public static class ContainerPatch {
        public const string ItemMoveRPC = "MUC_RequestItemMove";
        public const string ItemMoveResponseRPC = "MUC_RequestItemMoveResponse";

        public const string ItemAddRPC = "MUC_RequestItemAdd";
        public const string ItemAddResponseRPC = "MUC_RequestItemAddResponse";

        public const string ItemRemoveRPC = "MUC_RequestItemRemove";
        public const string ItemRemoveResponseRPC = "MUC_RequestItemRemoveResponse";

        public const string ItemConsumeRPC = "MUC_RequestItemConsume";
        public const string ItemConsumeResponseRPC = "MUC_RequestItemConsumeResponse";

        public const string ItemDropRPC = "MUC_RequestItemDrop";
        public const string ItemDropResponseRPC = "MUC_RequestItemDropResponse";

        [HarmonyPatch(typeof(Container), nameof(Container.Awake)), HarmonyPostfix]
        public static void ContainerAwakePatch(Container __instance) {
            if (!Plugin.IsFeatureEnabled(Plugin.MultiUserChests) || __instance == null) return;

            // Awake can be reached more than once by some networked container
            // prefabs. Do not attach duplicate state components.
            if (!__instance.GetComponent<ContainerExtend>())
                __instance.gameObject.AddComponent<ContainerExtend>();

            if (__instance.IgnoreInventory()) return;

            if (!__instance.m_nview)
                __instance.m_nview = __instance.m_rootObjectOverride
                    ? __instance.m_rootObjectOverride.GetComponent<ZNetView>()
                    : __instance.GetComponent<ZNetView>();

            // Container inventories can be created after Container.Awake during
            // networked zone loading. Register again once the inventory exists.
            ContainerExtend.EnsureRegistered(__instance);
        }

        // Keep Valheim's native open/stack RPCs. The old combined patch
        // replaced those RPCs and could leave a dedicated-server client with
        // no successful OpenRespons response at all. Suppressing only the
        // native in-use result is enough to allow simultaneous users while
        // preserving the current Valheim 1.0 access/ownership flow.
        [HarmonyPatch(typeof(Container), nameof(Container.IsInUse)), HarmonyPrefix]
        public static bool ContainerIsInUsePatch(Container __instance, ref bool __result) {
            if (!Plugin.IsFeatureEnabled(Plugin.MultiUserChests) || __instance == null || __instance.IgnoreInventory())
                return true;

            __result = false;
            return false;
        }
    }
}
