# Changelog

## 1.0.4

- Fixed dedicated-server chest opening by preserving Valheim's native open RPC and disabling only the in-use rejection for supported containers.
- Added a guarded GUI ownership fallback and hardened the chest GUI transpiler for Valheim 1.0.
- Fixed null inventory-owner handling that could break chest item transactions.

## 1.0.3

- Added configurable eternal fuel for native campfires, hearths, braziers, torches, and other `Fireplace` pieces using the synchronized ZDO fuel value.
- Added server-authoritative stump-to-sapling replanting with exact mappings for the supported vanilla tree types; no random or mismatched saplings are selected.
- Added native workbench auto-repair for every item the current station can repair, including armor, tools, weapons, bows, and shields.
- Kept the package at version 1.0.3 and kept BepInExPack Valheim as the only dependency.

- Audit pass against the current SleepSkip, Use Equipment in Water, EquipGearWhileRunning, VikingsDoSwim/BetterDiving, CurrencyPocket, SpeedyPaths, MultiUserChest, NoAFKRaids, and NoRainDamage implementations.
- Hardened deferred hotbar equip transactions, moved the currency card between Armor/Weight, added true drag-drop coin deposit, and marked consumed coin drops in their network ZDO to prevent duplicate pocket credit.
- Applied the diving camera water override before and after the camera solve, added cultivated-ground SpeedyPaths settings, and made AFK raid protection use active connections and event radius when available.

- Added No-Chest-Block/MultiUserChest-style simultaneous chest opening and stack acknowledgment.
- Added the reference-style authoritative chest item transaction RPCs for add, remove, move, consume, drop, preview, and rollback handling.
- Added SpeedyPaths-style ground detection, movement multipliers, and configurable no-stamina dirt/stone paths.
- Added dedicated-server NoAFKRaids protection based on authoritative player movement positions.
- Replaced Rigidbody diving with native `m_swimDepth` control, persistent dive state, native timer maintenance, and a post-camera water-clamp override.

- Uses the reference `Player.CheckRun` queue-clear removal plus a `Humanoid.ClearActionQueue` fallback for the running hotbar equip gate, preserving sprint movement.
- Replaces the native swimming check only inside the equipment transaction, leaving the real swimming state, stamina, and movement code untouched.
- Keeps Valheim's native dive target and swim timers active through `UpdateMotion`/`CustomFixedUpdate`, and removes the camera water clamp while the local player is below the surface.
- Fixed one-time CurrencyPocket migration so an old standalone plugin cannot re-add the same balance and duplicate coins; cleaned orphan button clones before rebuilding the pocket row.
- Applied swim-speed scaling at `Character.UpdateSwimming`, where vanilla consumes the speed value, instead of only updating it from the render-frame player update.
- Post-release hardening: rebuilt the running-equipment bypass around Valheim's running query and all equip call paths without changing the package version.
- Reworked diving to drive Valheim's native swim-depth target during fixed updates, so dive/surface input is not overwritten by vanilla buoyancy.
- Deduplicated the currency panel, migrated the standalone CurrencyPocket balance key, and put both coin buttons in a fixed non-overlapping row.
- Added a direct click-to-deposit action and changed sitting regeneration to an exact one-health-per-second accumulator by default.
- Fixed hotbar equipment while running by handling both Valheim movement flags and the central `UseItem`/`EquipItem` paths.
- Fixed swimming equipment by bypassing the native swim gate only for the equip transaction, then restoring the real swim state.
- Fixed diving by using held keys and applying vertical velocity after native swimming physics.
- Fixed currency pocket placement with rendered UI bounds instead of an expanded-inventory hardcoded offset.
- Added a deposit button and drag-and-drop coin deposit from the normal inventory.
- Allowed coin autopickup when normal inventory space is full and persisted the dedicated-server config before patch registration.
- Improved sleep vote cleanup and late-joiner popup delivery.
- Fixed sleep-vote registration across world reloads and stopped stale warning/vote state from carrying into the next vote.
- Removed repeated sleep-display RPC spam and updated the open vote body when counts change.
- Removed in-bed players from explicit vote sets and continued notifying eligible late joiners.

## 1.0.2

- Fixed hotbar equipment while running by bypassing the actual movement-state equip gate.
- Fixed currency pickup reliability and delayed the currency-pocket layout until other inventory mods finish positioning their UI.
- Added compatibility placement for common expanded-inventory layouts and fixed the currency icon/text lookup.
- Preserved stamina regeneration when no-stamina mode is enabled.
- Reapplied no-rain wear protection to already-loaded structures.

## 1.0.1

- Load on dedicated-server processes so the server config and server-side patches initialize.
- Fixed running hotbar equipment to bypass the vanilla movement-state gate.
- Reworked currency-pocket UI placement and inventory-mod compatibility.
- Clarified dedicated-server and client installation requirements.

## 1.0.0

- Initial Valheim 1.0 release.
- Combined the requested quality-of-life features into one configurable plugin.
