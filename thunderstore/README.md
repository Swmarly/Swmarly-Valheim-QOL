# Swmarly Valheim QOL

One optimized BepInEx plugin containing the quality-of-life changes used by Swmarly's Valheim setup.

## Included features

- Dropped items float using Valheim's native `Floating` physics.
- Equipment can be equipped while swimming, including the equipment-update transaction used by Valheim's water restriction.
- Hotbar equipment can be used while running; the vanilla `Player.CheckRun` equip gate is removed without changing movement speed or sprint state.
- Sleep-skip voting with percentage, warning, timeout, cooldown, and solo-server handling.
- Currency pocket: picked-up coins are stored on the player and are counted by traders.
- Swimming skill speed scaling, idle stamina regeneration, swim sprint, and configurable diving. Hold the configured Dive key (or crouch) to descend and the configured Surface key (or jump) to return toward the surface; the native swim timers and camera water clamp are held correctly while below the surface.
- Sneak speed scales with the Sneak skill.
- Health regeneration while sitting.
- Configurable trash mobs flee on sight.
- Uncovered structures no longer take rain wear.
- No stamina cost for hammer, hoe, and cultivator by default; an all-actions mode is available.
- Multiple players can open the same chest together, using the No-Chest-Block/MultiUserChest open/stack behavior.
- SpeedyPaths-style dirt/stone path and structure movement bonuses, with no running stamina usage on dirt and stone paths by default.
- Server-side NoAFKRaids behavior: random raids are blocked when all connected players have been stationary for the configured AFK period.

## Installation

Install the package with r2modman/Thunderstore Mod Manager, or extract the contents into the Valheim game directory so the DLL is under `BepInEx/plugins`.

For a dedicated server, install the package in the server instance's `BepInEx/plugins` directory and start the server once. The config will be created at `BepInEx/config/Swmarly.ValheimQOL.cfg` inside that server instance. In multiplayer, install the same package on every client as well: sleep voting, chest sharing, and NoAFKRaids use the server for authority, while equipment-in-water, running hotbar equipment, SpeedyPaths movement, diving, and the currency-pocket UI require the client copy.

The currency pocket is a separate coin balance: picked-up coins appear in the pocket and are included in trader totals. Open the inventory after picking up coins; click the pocket or use the down arrow to move normal-inventory coins into it, use the up arrow to extract all pocket coins, or drag a coin stack onto the pocket. The panel removes duplicate copies and is positioned from the final rendered Armor/Weight bounds after expanded-inventory mods finish their UI layout.

When updating, remove the old `SwmarlyValheimQOL.dll`/package copy first if your manager leaves duplicate plugin versions behind, then install the new package on both the server and all clients. Do not keep two copies of this mod in different plugin folders.

Every feature is enabled by default.

Do not install duplicate copies of the individual mods at the same time. Their overlapping patches can cancel each other out.

## Compatibility

Built against the Valheim 1.0 dedicated-server assemblies and BepInExPack Valheim 5.4.2350. The mod intentionally has no Jötunn or ServerSync dependency. Server-authoritative features (sleep voting, chest open/stack access, NoAFKRaids, coin pickup state, floating drops, rain wear, and fleeing behavior) run on the server; client features are owner-local and require the same package on every client.

## Credits

This project is an independent reimplementation inspired by the behavior of the mods listed in `THIRD_PARTY.md`. It does not bundle their DLLs or artwork.

## Thunderstore disclosure

The icon was created specifically for this package with generative image tooling; the package should be submitted with Thunderstore's **AI Generated** category selected.
