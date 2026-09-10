using System.Collections.Generic;

namespace SwmarlyValheimQOL {
    internal static class MultiUserChestInventoryIgnore {
        internal static bool IgnoreInventory(this InventoryOwner owner) {
            if (owner == null || !owner.ZNetView || owner.ZNetView.GetZDO() == null) return false;
            return owner.ZNetView.GetZDO().GetBool("MUC_Ignore");
        }
        internal static bool IgnoreInventory(this Container container) {
            return InventoryOwner.GetOwner(container?.GetInventory()).IgnoreInventory();
        }
        internal static IEnumerable<Inventory> GetInventories(this Inventory inventory) {
            if (inventory != null) yield return inventory;
        }
    }
}
