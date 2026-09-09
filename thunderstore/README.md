# Swmarly Valheim QOL

One optimized BepInEx plugin containing the quality-of-life changes used by Swmarly's Valheim setup.

## Included features

- Dropped items float using Valheim's native `Floating` physics.
- Equipment can be equipped while swimming.
- Hotbar equipment can be used while running.
- Sleep-skip voting with percentage, warning, timeout, cooldown, and solo-server handling.
- Currency pocket: picked-up coins are stored on the player and are counted by traders.
- Swimming skill speed scaling, idle stamina regeneration, swim sprint, and configurable diving.
- Sneak speed scales with the Sneak skill.
- Health regeneration while sitting.
- Configurable trash mobs flee on sight.
- Uncovered structures no longer take rain wear.
- No stamina cost for hammer, hoe, and cultivator by default; an all-actions mode is available.

## Installation

Install the package with r2modman/Thunderstore Mod Manager, or extract the contents into the Valheim game directory so the DLL is under `BepInEx/plugins`.

For a dedicated server, install the package in the server instance's `BepInEx/plugins` directory and start the server once. The config will be created at `BepInEx/config/Swmarly.ValheimQOL.cfg` inside that server instance. In multiplayer, install the same package on every client as well: sleep voting uses the server for the decision and clients for the vote popup, while equipment-in-water, running hotbar equipment, and the currency-pocket UI require the client copy.

Every feature is enabled by default.

Do not install duplicate copies of the individual mods at the same time. Their overlapping patches can cancel each other out.

## Compatibility

Built against the Valheim 1.0 dedicated-server assemblies and BepInExPack Valheim 5.4.2350. The mod intentionally has no Jötunn or ServerSync dependency.

## Credits

This project is an independent reimplementation inspired by the behavior of the mods listed in `THIRD_PARTY.md`. It does not bundle their DLLs or artwork.

## Thunderstore disclosure

The icon was created specifically for this package with generative image tooling; the package should be submitted with Thunderstore's **AI Generated** category selected.
