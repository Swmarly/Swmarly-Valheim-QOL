using HarmonyLib;

namespace SwmarlyValheimQOL {
    [HarmonyPatch]
    public static class HumanoidPatch {
        [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.Awake)), HarmonyPostfix]
        public static void HumanoidAwakePatch(Humanoid __instance) {
            __instance.gameObject.AddComponent<HumanoidExtend>();
        }
    }
}

