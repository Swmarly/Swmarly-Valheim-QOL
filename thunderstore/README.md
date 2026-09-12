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
- No stamina cost for hammer, hoe, and cultivator use by default; holding a tool does not remove stamina costs from running or other activities, and an all-actions mode is available.
- Configurable eternal fuel for native campfires, hearths, braziers, torches, and other configured `Fireplace` pieces. The server maintains the synchronized fuel value, so the light stays lit for every player.
- Configurable automatic replanting for supported tree stumps. The default mappings replant Beech, Fir, Pine, Birch, and Oak with their matching saplings; custom `stump=sapling` mappings are supported for compatible tree mods.
- Configurable automatic stump removal after felling a tree. It uses Valheim's native stump destruction path so the stump's normal log drops are preserved, and it works with automatic replanting.
- Configurable automatic repair when a workbench/crafting station is open. It uses Valheim's native repair eligibility checks and repairs every eligible weapon, tool, armor, bow, shield, and normal durability item the station can handle.
- Multiple players can open and interact with the same chest together, including wagon inventories. Chest item moves are routed through an authoritative server-owner transaction layer so simultaneous moves are accepted or rejected without client-side duplicate inventories.
- SpeedyPaths-style dirt/stone path and structure movement bonuses, with no running stamina usage on dirt and stone paths by default.
- Server-side NoAFKRaids behavior: random raids are blocked when an AFK player is within the configured event protection radius; if Valheim does not expose an event position, it safely falls back to blocking only when all connected players are AFK.

## Installation

Install the package with r2modman/Thunderstore Mod Manager, or extract the contents into the Valheim game directory so the DLL is under `BepInEx/plugins`.

For a dedicated server, install the package in the server instance's `BepInEx/plugins` directory and start the server once. The config will be created at `BepInEx/config/Swmarly.ValheimQOL.cfg` inside that server instance. In multiplayer, install the same package on every client as well: sleep voting, chest sharing, and NoAFKRaids use the server for authority, while equipment-in-water, running hotbar equipment, SpeedyPaths movement, diving, and the currency-pocket UI require the client copy.

The currency pocket is a separate coin balance: picked-up coins appear in the pocket and are included in trader totals. Open the inventory after picking up coins; use the down arrow to move normal-inventory coins into it, or use the up arrow to extract all pocket coins. The panel removes duplicate copies and is placed between the final rendered Armor/Weight bounds after expanded-inventory mods finish their UI layout.

When updating, remove the old `SwmarlyValheimQOL.dll`/package copy first if your manager leaves duplicate plugin versions behind, then install the new package on both the server and all clients. Do not keep two copies of this mod in different plugin folders.

Every feature is enabled by default.

The new settings are created in `BepInEx/config/Swmarly.ValheimQOL.cfg`:

- `Features / Eternal fires and lights`
- `Features / Automatically replant trees`
- `Features / Automatically remove tree stumps`
- `Features / Auto repair at workbenches`
- `Eternal fires and lights / Prefab names` controls exactly which native or compatible `Fireplace` prefab names stay fueled.
- `Automatic tree replanting / Stump to sapling mappings` controls the exact stump-to-sapling pairs. The feature never chooses a random tree.
- `Automatic tree replanting / Replant delay seconds` controls the server-side spawn delay.

Automatic stump removal is enabled by default. Disable `Features / Automatically remove tree stumps` if you want to leave stumps in the world; manual stump destruction can still trigger automatic replanting when that feature is enabled.

Do not install duplicate copies of the individual mods at the same time. Their overlapping patches can cancel each other out.

## Compatibility

Built against the Valheim 1.0 dedicated-server assemblies and BepInExPack Valheim 5.4.2350. The only Thunderstore dependency is BepInExPack Valheim; Jötunn, ServerSync, and Server_devcommands are not required. Eternal fuel and tree spawning are server-authoritative, while automatic repair runs for each local player through Valheim's normal inventory synchronization. Install the same package on the dedicated server and every client for the complete feature set.

## Credits

This project is an independent reimplementation inspired by the behavior of the mods listed in `THIRD_PARTY.md`. It does not bundle their DLLs or artwork.

## Thunderstore disclosure

The icon was created specifically for this package with generative image tooling.
