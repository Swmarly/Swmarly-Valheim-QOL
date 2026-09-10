using HarmonyLib;

namespace SwmarlyValheimQOL {
    [HarmonyPatch]
    public static class HumanoidPatch {
        [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.Awake)), HarmonyPostfix]
        public static void HumanoidAwakePatch(Humanoid __instance) {
            if (!Plugin.IsFeatureEnabled(Plugin.MultiUserChests)) return;
            __instance.gameObject.AddComponent<HumanoidExtend>();
        }
    }
}
