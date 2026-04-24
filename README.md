# TangledeepAccess

Screen reader accessibility mod for [Tangledeep](https://store.steampowered.com/app/628770/Tangledeep/). Makes the game fully playable with NVDA, JAWS, or Windows SAPI.

## Features

- Full radar system with categories (Enemies, NPCs, Shops, Items, Stairs, Destructibles)
- Auto-walk to any radar target with hazard-avoiding pathfinding
- Auto-explore for mapping dungeons
- Combat log reading, critical hit alerts, boss health tracking
- Health/Stamina/Energy alerts at key thresholds
- Status effect gain/loss announcements
- Enemy info: HP%, level, type, behavior, resistances, champion mods
- Ability targeting with shape announcements (line, cone, burst, area, etc.)
- Full inventory navigation with rarity, equipment comparison, shop prices
- Character sheet with 6 navigable sections
- Skill sheet with ability details
- Settings menu with slider values and toggle states
- Journal, recipes, quests, monster pedia
- NPC dialog with option counting
- Job Change, Crafting, Monster Corral, Item World, Casino
- Context-sensitive help (Shift+F1)
- Wall bump detection with audio feedback
- Stereo panning beacon toward radar targets

## Installation

1. Purchase [Tangledeep on Steam](https://store.steampowered.com/app/628770/Tangledeep/)
2. Download the latest `TangledeepAccess.zip` from [Releases](../../releases)
3. Extract the ZIP into your Tangledeep game folder (where `Tangledeep.exe` is)
4. Launch the game - you should hear "TangledeepAccess loaded. Shift F1 for help."

The release ZIP contains BepInEx (mod loader), Tolk (screen reader bridge), and the mod DLL. Everything is pre-configured.

## Keyboard Shortcuts

### General

| Key | Action |
|-----|--------|
| Shift+F1 | Context-sensitive help |
| F2 | Full status (HP, Stamina, Energy, XP, JP, Gold, Quests, Pet) |
| F3 | Toggle radar |
| F4 | Item details (menus) / Enemy info (radar target) |
| F12 | Toggle debug mode |

### World / Exploration

| Key | Action |
|-----|--------|
| Arrow Keys | Move |
| Shift+Left/Right | Cycle radar categories |
| Shift+Up/Down | Cycle radar items |
| O | Walk to radar target |
| E | Auto-explore |
| L | Look around |
| Tab | Target nearest enemy |
| Tab/Shift+Tab | Cycle enemies |
| Enter | Attack target / walk if out of range |
| Shift+A | Auto-attack nearest enemy |
| Shift+H | Read hotbar |
| Shift+L | Message log |
| Shift+M | Floor overview |
| Shift+N | Adjacent tiles |
| Shift+Q | Quests |
| G | Gold |
| C | Character sheet |
| 1-8 | Hotbar abilities |

### Menus

| Key | Action |
|-----|--------|
| Arrow Keys | Navigate |
| Enter | Select |
| Tab/Shift+Tab | Cycle tabs/sections |
| F4 | Full item tooltip |
| Escape | Close |

### Settings

| Key | Action |
|-----|--------|
| Enter | Toggle option / start slider |
| Left/Right | Adjust slider |
| L | Cycle language |

## Building from Source

Requires .NET SDK (targeting net472).

1. Clone this repo
2. Open `src/TangledeepAccess.csproj`
3. Update the `HintPath` references to point to your Tangledeep installation
4. `dotnet build --configuration Debug`

## Dependencies

- [BepInEx 6.0.0-pre.1](https://github.com/BepInEx/BepInEx/releases) - Mod loader for Unity games
- [Tolk](https://github.com/ndarilek/tolk) - Screen reader abstraction library
- Tangledeep (Steam version)

## License

This mod is provided as-is for accessibility purposes. Tangledeep is developed by [Impact Gameworks](https://www.impactgameworks.com/).
