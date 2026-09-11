using System.Runtime.CompilerServices;
using UnityEngine;

namespace SwmarlyValheimQOL {
    public class ContainerExtend : MonoBehaviour {
        private static readonly ConditionalWeakTable<Inventory, ContainerInventoryOwner> Containers =
            new ConditionalWeakTable<Inventory, ContainerInventoryOwner>();

        private Container container;
        private Inventory inventory;
        private float nextRegistrationAttempt;

        private void Awake() {
            if (!TryGetComponent(out container)) {
                return;
            }

            TryRegister();
            nextRegistrationAttempt = Time.unscaledTime + 0.25f;
        }

        private void Update() {
            if (!container) {
                return;
            }

            if (inventory != null &&
                Containers.TryGetValue(inventory, out ContainerInventoryOwner owner) &&
                owner != null &&
                owner.Container == container) {
                return;
            }

            if (Time.unscaledTime < nextRegistrationAttempt) {
                return;
            }

            nextRegistrationAttempt = Time.unscaledTime + 0.25f;
            TryRegister();
        }

        private bool TryRegister() {
            if (!container) {
                return false;
            }

            Inventory current = container.GetInventory();
            if (current == null) {
                return false;
            }

            if (inventory != null && inventory != current) {
                Containers.Remove(inventory);
            }

            inventory = current;

            if (Containers.TryGetValue(current, out ContainerInventoryOwner existing)) {
                if (existing != null && existing.Container == container) {
                    return true;
                }

                Containers.Remove(current);
            }

            Containers.Add(current, new ContainerInventoryOwner(container));
            return true;
        }

        private void OnDestroy() {
            if (inventory != null) {
                Containers.Remove(inventory);
                inventory = null;
            }
        }

        internal static bool EnsureRegistered(Container container) {
            if (!container) {
                return false;
            }

            ContainerExtend extension = container.GetComponent<ContainerExtend>();
            return extension != null && extension.TryRegister();
        }

        public static bool GetContainer(Inventory inventory, out ContainerInventoryOwner container) {
            if (inventory == null) {
                container = null;
                return false;
            }

            return Containers.TryGetValue(inventory, out container);
        }
    }
}
