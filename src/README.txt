TangledeepAccess - Screen Reader Accessibility Mod for Tangledeep
==================================================================

Version: 1.1.0
Requires: BepInEx 6.0.0-pre.1, Tolk screen reader library
Compatible with: NVDA, JAWS, Windows SAPI

INSTALLATION
============

1. Install BepInEx 6.0.0-pre.1 into your Tangledeep game folder.
   - Download from: https://github.com/BepInEx/BepInEx/releases
   - Extract into the Tangledeep game folder (where Tangledeep.exe is).
   - Run the game once to let BepInEx generate its folder structure.

2. Install Tolk screen reader bridge.
   - Copy TolkDotNet.dll into the Tangledeep game folder.
   - Copy Tolk.dll into the Tangledeep game folder.
   - Copy nvdaControllerClient64.dll into the Tangledeep game folder.

3. Install TangledeepAccess.
   - Copy TangledeepAccess.dll into BepInEx\plugins\ folder.

4. Launch the game. You should hear "TangledeepAccess loaded. Shift F1 for help."

KEYBOARD SHORTCUTS
==================

General:
  Shift+F1    Context-sensitive help (changes based on what you're doing)
  F1          Game's built-in help
  F2          Full status (Health, Stamina, Energy, XP, JP, Gold, Quests, Pet)
  F3          Toggle radar on/off
  F4          Item details (in menus) or enemy info (on radar target)
  F12         Toggle debug mode

World / Exploration:
  Arrow Keys       Move your character
  Shift+Arrows     Cycle radar categories (Left/Right) and items (Up/Down)
  O                Walk to radar target (auto-pathfinding, avoids hazards)
  E                Auto-explore
  L                Look around (nearby interesting tiles)
  Tab              Target nearest enemy
  Tab/Shift+Tab    Cycle enemies while targeting
  Enter            Confirm attack on target / walk to target if out of range
  Shift+A          Auto-attack nearest enemy in range
  Shift+H          Read hotbar abilities
  Shift+L          Read recent message log
  Shift+M          Floor overview (enemy count, NPCs, items, stairs, exploration %)
  Shift+N          Adjacent tiles scan (all 8 directions)
  Shift+Q          Read active quests
  G                Announce current gold
  C                Open character sheet
  Keys 1-8         Use hotbar abilities
  Escape           Open menu

Menus / Inventory:
  Arrow Keys       Navigate items and options
  Enter            Select / equip / use
  Tab/Shift+Tab    Cycle tabs or character sheet sections
  F4               Read full item tooltip
  Escape           Close menu

Targeting:
  Arrow Keys       Move targeting cursor
  Tab/Shift+Tab    Cycle between enemies
  Enter            Confirm target
  Escape           Cancel targeting

Settings:
  Arrow Keys       Navigate options
  Enter            Toggle on/off or start slider adjustment
  Left/Right       Adjust slider value
  L                Cycle game language
  Escape           Close settings

Dialog / NPCs:
  Arrow Keys       Browse dialog options
  Enter            Select option
  Escape           Go back / close

Character Creation:
  Arrow Keys       Navigate jobs, feats, modes
  Enter            Select
  R                Randomize name

FEATURES
========

Navigation:
  - Full radar system with categories (Enemies, NPCs, Shops, Items, Stairs, Destructibles)
  - Auto-walk to any radar target with hazard-avoiding pathfinding
  - Auto-explore for mapping dungeons
  - Wall bump detection with audio feedback
  - Stereo panning beacon toward radar targets
  - Floor overview and adjacent tile scanning

Combat:
  - Automatic combat log reading (hits, misses, blocks, parries, dodges)
  - Critical hit announcements
  - Health/Stamina/Energy alerts at 75%, 50%, 25%, 10%
  - Boss health tracking with threshold alerts
  - Status effect gain/loss announcements
  - Enemy info via F4 (HP%, level, type, behavior, resistances, champion mods)
  - Ability targeting with shape announcements (line, cone, burst, area, etc.)
  - Monster ability charge warnings

Menus and UI:
  - Full inventory navigation with item names, rarity, and equipment comparison
  - Shop prices (buy and sell)
  - Character sheet with 6 navigable sections
  - Skill sheet with ability details (costs, cooldown, range, description)
  - Hotbar reading
  - Settings with slider values and toggle states
  - Journal (Recipes, Rumors, Combat Log, Monster Pedia)
  - NPC dialog with option counting
  - Save slot details (name, level, job, location, playtime)

Advanced:
  - Job Change UI with cost announcement
  - Recipe details (ingredients, description, healing, inventory count)
  - Crafting screen accessibility
  - Monster Corral (pet management with stats, happiness, beauty)
  - Item World / Dream navigation (item browsing, orb selection)
  - Casino announcements
  - Level-up and XP progress
  - Loot and gold pickup announcements
  - Map transition announcements
  - Hazard detection (lava, electric, mud, water, traps)
  - Death/Game Over announcement

KNOWN LIMITATIONS
=================

- Bank, Town Portal, Save/Load confirmations, and Campfire rest use the game's
  dialog system and should be accessible, but have not been extensively tested.
- Casino game state (specific cards, dice, slot results) is not announced in detail.
- The mod does not change any game controls. All mod keys use Shift+key combinations
  or function keys that don't conflict with game bindings.
