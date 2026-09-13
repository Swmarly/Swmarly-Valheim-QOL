# Swmarly Valheim QOL — development guide

This file is the hand-off document for agents maintaining the mod. It explains the intended behavior, the multiplayer rules, the Valheim integration points, and the reason behind the less-obvious implementation choices.

## Project contract

- This is one BepInEx plugin: `Swmarly.ValheimQOL` (`SwmarlyValheimQOL.dll`).
- The target is Valheim 1.0 and the package is built against the live Valheim dedicated-server assemblies plus BepInExPack Valheim 5.4.2350.
- Every feature has its own config toggle. New config entries must have a safe default and must not make a headless dedicated server depend on client UI objects.
- The same package is installed on the dedicated server and on every client. Server-owned world state must be changed by the authoritative ZDO owner; client-only presentation and input must be guarded by `Plugin.IsLocalPlayer`.
- The package has no required runtime dependency other than BepInExPack Valheim. AzuAutoStore is detected as a soft dependency only for the multi-user chest compatibility marker.
- Preserve Valheim's native transaction, spawn, drop, and synchronization paths whenever possible. A Harmony patch should narrow or supplement native behavior rather than recreate an entire subsystem without a reason.

## Runtime lifecycle

`Plugin.Awake` is the entry point:

1. `Instance` is assigned so static patch classes can start coroutines and log warnings.
2. `BindConfig` creates or loads the config entries.
3. `Config.Save` runs before patch registration. This is intentional: a dedicated server must create its config even though it never creates inventory or trader UI.
4. `NormalizeConfigFile` moves `[Features]` to the top and removes the obsolete `[Flee on sight]` section without rewriting unrelated user values.
5. `RegisterAzuAutoStoreMultiUserChestCompatibility` publishes the official MultiUserChest opt-out marker only when QOL multi-user chests are enabled and the real MultiUserChest plugin is not already present. This prevents AzuAutoStore from installing duplicate chest patches.
6. `ApplyHarmonyPatches` discovers patch classes in this assembly and registers them independently. One version-specific target failure is logged but must not prevent unrelated features from loading.

When adding a patch, keep the following questions explicit in its comments:

- Is this code running on the server, on every client, or only for the local player?
- Who owns the object/ZDO at the moment it is changed?
- Is the patch replacing a native action or only changing a value at a native call site?
- What happens if the target method is renamed in a later Valheim build?
- Does this state need to be reset on world reload, player replacement, or object destruction?

## Configuration and feature map

All entries in the `Features` section default to enabled unless their declaration says otherwise.

| Config entry | Implementation | Scope and important behavior |
| --- | --- | --- |
| `Float dropped items` | `FloatingItemsPatch`, `FloatingItemsStartPatch` | Adds/configures Valheim's native `Floating` component on networked drops. `Start` is also patched because some network prefabs do not have their `ZNetView` ready during `Awake`. |
| `Use equipment while swimming` | `EquipmentInWaterSwimmingPatch` plus the two compatibility patches | Overrides `Character.IsSwimming` only while the call stack is performing `EquipItem`/`UpdateEquipment`. Movement, drowning, stamina, and normal swimming still see the real state. |
| `Equip hotbar items while running` | `EquipWhileRunningPatch`, `EquipWhileRunningSlowdownPatch` | Removes only the `ClearActionQueue` call that cancels a queued hotbar equip and suppresses the equip animation's minor-action slowdown while Run is held. It does not change sprint calculations or stamina. |
| `Sleep skip voting` | `SleepRpcRegistrationPatch`, `SleepRpc`, `SleepSkipPatch` | The server calculates eligible players, vote state, timeout, cooldown, and percentage; clients display/vote through routed RPCs. Bed occupants count as yes and are removed from explicit vote sets. |
| `Currency pocket` | `CurrencyPickupPatch`, capacity/context patches, `CurrencyStorePatch`, `CurrencyStorePurchasePatch`, UI patches | Coins are stored in player custom data. Trader totals include the pocket, and purchases debit inventory coins first then the pocket remainder. Coin pickup marks the coin drop ZDO before destroying it to prevent duplicate credit. |
| `Swimming improvements` | `PlayerQolUpdatePatch`, `SwimImprovementsPatch` | Scales swim speed by Swim skill, optionally adds sprint speed, and regenerates stamina only while the local player is nearly stationary in water. |
| `Diving` | `DivingPatch`, `DivingNativeSwimmingStatePatch`, `DivingInputPatch`, `DivingMotionPatch`, `DivingSwimmingTimerPatch`, `DivingCameraPatch` | Maintains the native swim-depth target/timers while underwater, reads input in render `Update`, applies optional stamina cost, and keeps the camera below the water plane. It must release back to the normal walking state on real land/death. |
| `Sneak speed scaling` | `PlayerQolUpdatePatch` | Stores the original crouch speed once per player object and applies a skill-based multiplier. Do not use the current modified speed as the new base. |
| `Regenerate while sitting` | `SitRegenerationFixedPatch` | Uses a real-time deadline and one heal call per second. It is local-player-only and clears its timer whenever the player stops sitting, dies, or reaches max health. |
| `No rain damage` | `NoRainDamagePatch`, `NoRainDamageUpdatePatch` | Disables only the uncovered-roof wear flag at both initialization and the wear calculation, preserving support/structural wear. |
| `No stamina costs` | `NoStaminaToolUseScopePatch`, `NoStaminaCostsPatch` | Mode 1 makes only hammer/hoe/cultivator tool actions free. `UpdatePlacement` is included because Valheim calls `UseStamina` after `PlacePiece`/`RemovePiece` return. Mode 2 intentionally makes all positive stamina costs free; negative values still regenerate. |
| `Allow multiple users in chests` | `MultiUserChest_*` files | Keeps the native chest open flow, removes only the in-use block for supported containers, and routes cross-owner inventory transactions through server-side RPC handlers and previews. |
| `Speedy paths` | `SpeedyPathsState` and three movement patches | Samples terrain paint/structure material for the local player, after physics ground contact. A single local-player cache prevents remote players from consuming the sensor timer; the masked fallback raycast avoids treating nearby characters as ground. |
| `No stamina on paths` | `SpeedyPathsState`, `SpeedyPathsStaminaPatch` | Sets running drain to zero on dirt/stone paths by default. Other surfaces use their configured multiplier. This feature and `Speedy paths` may be configured independently. |
| `No AFK raids` | `NoAfkRaidsState`, `NoAfkRaidsPatch` | Runs only on the server. It tracks authoritative character ZDO positions, filters disconnected players, checks an event radius when Valheim exposes an event position, and otherwise uses the safe all-connected-players fallback. |
| `Eternal fires and lights` | `EternalFireState`, `EternalFireUpdatePatch`, `EternalFireSetFuelPatch` | Only configured prefab names are affected. The owner writes the synchronized ZDO fuel value, and the setter patch covers local/native fuel changes. |
| `Automatically replant trees` | `AutoReplantState`, `AutoReplantTreePatch` | Maps exact stump names to sapling prefabs. Only the stump/ZDO owner schedules a delayed `ZNetScene.SpawnObject`, preventing client duplicates. |
| `Automatically remove tree stumps` | `AutoRemoveTreeStumpPatch` and `AutoReplantState` | Runs after native `TreeBase.SpawnLog`, waits one frame for the spawned stump components to initialize, finds the matching stump, and calls native `Destructible.Destroy`. This preserves the stump's `DropOnDestroyed` log drops and automatically passes through the replant prefix. |
| `Auto repair at workbenches` | `AutoRepairAtWorkbenchPatch` | Runs the native station eligibility loop while a local crafting station is open, with a defensive iteration cap. |

The `Features` section is deliberately kept first in the generated config. Do not rename an existing config key casually: users' values are keyed by section and entry name.

## Multiplayer and dedicated-server invariants

### Authority

Valheim's `ZNetView`/`ZDO` owner is the authority for persistent networked objects. Code that spawns or destroys world objects must either run only on the owner or check `nview.IsOwner()` immediately before the mutation. Clients receive the result through Valheim's normal replication.

The following are server-authoritative in this plugin:

- sleep vote calculation and result;
- multi-user chest container transaction handling;
- AFK raid blocking;
- eternal-fire ZDO fuel writes;
- tree stump destruction and sapling spawning.

The following are local-player/client presentation or input features and therefore must be installed on every client:

- inventory currency UI and trader display additions;
- equipment-in-water and hotbar-equip input behavior;
- swimming, diving, sneak, stamina, and SpeedyPaths movement changes;
- floating item component setup, which is applied wherever the networked item exists.

### Static state

Static state is safe only when its ownership is clear. In particular:

- `SpeedyPathsState` caches only the local player and resets when that player changes or the feature is disabled.
- `SleepRpc`/`SleepSkipPatch` state is vote state and is reset after every result, cancellation, or world reload.
- `AutoReplantState` uses ZDO flags plus local pending sets as duplicate guards. The ZDO flag is the durable/network-visible guard; the instance ID set only suppresses repeated same-frame scheduling.
- `CurrencyPickupPatch` marks the coin drop ZDO before crediting the pocket. Never move that mark after the balance change.
- Per-player dictionaries keyed by `GetInstanceID()` must remove entries when their feature is no longer active or the object is destroyed.

### Do not use these shortcuts

- Do not make remote `Player` instances use local keyboard/controller input.
- Do not decrement a shared sensor timer from every player instance.
- Do not make a client destroy a chest/tree/stump or spawn a sapling solely because it sees the object.
- Do not patch a broad `UseStamina` call into “free while tool equipped” unless the legacy config explicitly requests it; movement and swimming costs must remain real in the default tool mode.
- Do not replace native inventory/chest operations with a client-only mutation when the inventory has a different network owner.
- Do not manually duplicate stump drops. Native `Destructible.Destroy` invokes the stump's `m_onDestroyed` callback, which is where `DropOnDestroyed` creates its normal logs.

## Multi-user chest transaction model

The multi-user chest implementation is the largest subsystem and is intentionally split into small files:

1. `ContainerExtend` registers a container inventory with an owner wrapper and retries after network reloads.
2. `InventoryOwner`/`HumanoidExtend` associate inventories with their owning humanoid or container and expose the relevant `ZNetView`.
3. `InventoryGuiPatch` keeps the native GUI usable, creates client-side previews, and routes GUI actions that cross inventory owners.
4. `InventoryPatch` intercepts the overloads that can move items between different owners, while leaving local same-owner operations native.
5. `ContainerHandler` validates an add/remove/move request against the current authoritative container inventory and creates a request package.
6. `ContainerRPCHandler` receives routed requests on the container owner, validates sender/request data, applies the transaction, and sends a response.
7. `InventoryPreview` and `SlotPreview` let the client display pending operations without claiming the container or applying an unconfirmed server mutation.
8. `PackageHandler` stores short-lived request/response objects by random IDs while the routed RPC is in flight.
9. `InventoryHelper` serializes enough `ItemData` to validate and reproduce a transaction, including position, durability, quality, crafter, world level, and custom data.
10. `InventoryBlock` temporarily prevents pickup or consume from touching slots while a transaction is pending, avoiding local changes that would race the server response.

The PR that introduced the current chest reload/auto-store behavior is PR #1. Its head commit is part of the current baseline; do not remove or replace its owner-registration retry, AzuAutoStore compatibility marker, or Valheim 1.0-safe inventory patching without reproducing the multiplayer scenario.

## Currency pocket model

The pocket is a player custom-data balance, not a second physical inventory. `CoinKey` is the current key and `LegacyCoinKey` is migrated once. Pickup credit is protected by `CoinConsumedKey` on the coin drop's ZDO.

Trader flow is intentionally split:

- `GetPlayerCoins` adds the pocket to the native inventory count shown by the trader.
- If inventory coins alone are enough, native `StoreGui.BuySelectedItem` runs and consumes them normally.
- If the pocket is needed, `CurrencyStorePurchasePatch` adds the item using Valheim's current `Inventory.AddItem` overload, removes available inventory coins, subtracts only the remainder from the pocket, then calls the native trader/effect/UI completion behavior.

If this code changes, test all three cases: inventory-only payment, pocket-only payment, and mixed payment. Also test insufficient total coins and a full inventory.

## Stamina and movement model

Valheim has several independent stamina callers. The default no-stamina mode must not globally zero `Player.UseStamina` while a tool is held. `NoStaminaToolUseScopePatch` marks synchronous action scopes, and `NoStaminaCostsPatch` changes only positive stamina values while one of those scopes is active. Running, jumping, swimming, diving, and other activities remain charged.

SpeedyPaths modifies the temporary `m_runStaminaDrain` value around `Player.CheckRun` and restores it in a postfix. It modifies jog/run speed factors only for the local player. Never leave a modified value in a player component after the original method returns.

## Tree removal/replant model

Native tree felling creates a log and then instantiates `TreeBase.m_stubPrefab`. `AutoRemoveTreeStumpPatch` hooks `TreeBase.SpawnLog` in a postfix, so the log spawn is already complete. The next-frame coroutine locates only the matching stump prefab within the small radius and verifies ownership before calling `Destructible.Destroy`.

That native call performs three important things in order: destruction effects, the stump's `m_onDestroyed` callback (including normal `DropOnDestroyed` logs), and the networked object destroy. The existing `AutoReplantTreePatch` runs before the same call and schedules the configured sapling on the owner. This is why stump removal and replanting remain compatible.

## Build and packaging

The supported build is `.github/workflows/build.yml`:

1. It reads the current public Valheim dedicated-server build ID.
2. It restores/caches the matching Valheim managed DLLs and BepInEx core DLLs.
3. It builds `src/SwmarlyValheimQOL.csproj` for `net472`.
4. `scripts/package.py` stages the DLL, Thunderstore metadata, icon, license, and third-party disclosure into `dist/SwmarlyValheimQOL-<version>.zip`.

Before a release, verify all of the following:

- `PluginVersion`, `.csproj` `<Version>`, and `thunderstore/manifest.json` agree.
- `git diff --check` passes.
- The build succeeds against the live Valheim assemblies.
- The package contains files at its root, not an extra directory level.
- The manifest contains only the intended BepInEx dependency.
- A dedicated server can create the config without opening client UI.
- A client and dedicated server both log the plugin load and Harmony patch count.

## Complete source-file map

### Plugin and standalone feature patches

- `SwmarlyValheimQOL.cs` — plugin bootstrap, all config entries, currency UI/pocket, and the standalone feature patches listed above.
- `NoStaminaToolUseScopePatch.cs` — action-scope detection for precise tool stamina exemption.
- `ShallowWaterSwimmingPatch.cs` — prevents shallow-water false swimming and preserves walking contact.

### Multi-user chest data contracts

- `MultiUserChest_Data_IPackage.cs` — common serialization contract.
- `MultiUserChest_Data_IRequest.cs` — request contract with source/target inventories and request ID.
- `MultiUserChest_Data_IResponse.cs` — response contract with source ID, success flag, and amount.
- `MultiUserChest_Data_RequestChestAdd.cs` — player-to-chest add request, including optional switch item.
- `MultiUserChest_Data_RequestChestAddResponse.cs` — add response and any switched item.
- `MultiUserChest_Data_RequestChestRemove.cs` — chest-to-player remove request, including destination and switch positions.
- `MultiUserChest_Data_RequestChestRemoveResponse.cs` — remove response and switch state.
- `MultiUserChest_Data_RequestConsume.cs` — request to consume an item at a chest slot.
- `MultiUserChest_Data_RequestConsumeResponse.cs` — consume response with the authoritative item snapshot.
- `MultiUserChest_Data_RequestDrop.cs` — request to drop an item from the container.
- `MultiUserChest_Data_RequestDropResponse.cs` — drop response, sender, and returned item snapshot.
- `MultiUserChest_Data_RequestMove.cs` — same-container move request.
- `MultiUserChest_Data_RequestMoveResponse.cs` — same-container move response.
- `MultiUserChest_Data_SlotPreview.cs` — speculative inventory slot state used by the client GUI.

### Multi-user chest runtime and helpers

- `MultiUserChest_ContainerExtend.cs` — component attached to containers for owner registration, including post-reload retries.
- `MultiUserChest_ContainerHandler.cs` — authoritative validation and request construction for chest operations.
- `MultiUserChest_ContainerRPCHandler.cs` — server-side routed RPC dispatch and transaction application.
- `MultiUserChest_HumanoidExtend.cs` — associates humanoid inventories with an owner wrapper.
- `MultiUserChest_InventoryBlock.cs` — temporary slot/consume locks while requests are pending.
- `MultiUserChest_InventoryHandler.cs` — client-side response handling and authoritative inventory reconciliation.
- `MultiUserChest_InventoryIgnore.cs` — opt-out marker helpers and inventory enumeration.
- `MultiUserChest_InventoryOwner.cs` — common owner abstraction for humanoids and containers.
- `MultiUserChest_InventoryPreview.cs` — queued speculative changes and delayed cleanup.
- `MultiUserChest_PackageHandler.cs` — request/response package ID storage.
- `MultiUserChest_Helper_ConditionalWeakTableExtension.cs` — safe idempotent association helper.
- `MultiUserChest_Helper_InventoryHelper.cs` — item serialization, cloning, stack limits, slot selection, and move helpers.
- `MultiUserChest_Helper_Log.cs` — static logging bridge used by the chest subsystem.
- `MultiUserChest_Helper_ReflectionHelper.cs` — narrowly scoped reflection helpers for compatibility code.

### Multi-user chest Harmony patches

- `MultiUserChest_Patches_ContainerOwnerPatch.cs` — local GUI ownership fallback only for the currently opened supported container.
- `MultiUserChest_Patches_ContainerPatch.cs` — container RPC names and native container hooks.
- `MultiUserChest_Patches_GamePatches.cs` — registers the routed RPC endpoints once per game instance.
- `MultiUserChest_Patches_HumanoidPatch.cs` — adds `HumanoidExtend` to humanoids.
- `MultiUserChest_Patches_InventoryGuiPatch.cs` — GUI routing, previews, and reload reconciliation.
- `MultiUserChest_Patches_InventoryPatch.cs` — inventory add/remove/move/consume interception and owner bookkeeping.
- `MultiUserChest_Patches_PickupPatch.cs` — blocks pickup only while a conflicting slot is temporarily locked.

## Commenting standard for future changes

Every new class or Harmony patch should start with a short comment describing the native method it touches, the player/object scope, and why a prefix/postfix/transpiler was chosen. Every non-obvious branch should explain the invariant it protects, especially ownership, duplicate prevention, world reload behavior, or version-specific reflection. Update this file and the Thunderstore README when a feature, config key, patch target, or multiplayer rule changes.
