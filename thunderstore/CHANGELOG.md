# Changelog

## 1.0.3

- Fixed hotbar equipment while running by handling both Valheim movement flags and the central `UseItem`/`EquipItem` paths.
- Fixed swimming equipment by bypassing the native swim gate only for the equip transaction, then restoring the real swim state.
- Fixed diving by using held keys and applying vertical velocity after native swimming physics.
- Fixed currency pocket placement with rendered UI bounds instead of an expanded-inventory hardcoded offset.
- Added a deposit button and drag-and-drop coin deposit from the normal inventory.
- Allowed coin autopickup when normal inventory space is full and persisted the dedicated-server config before patch registration.
- Improved sleep vote cleanup and late-joiner popup delivery.

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
