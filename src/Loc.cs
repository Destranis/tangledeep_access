using System.Collections.Generic;

namespace TangledeepAccess
{
    /// <summary>
    /// Central localization for the accessibility mod.
    /// Automatically detects game language.
    /// </summary>
    public static class Loc
    {
        #region Fields

        private static bool _initialized = false;
        private static string _currentLang = "en";

        private static readonly Dictionary<string, string> _english = new();

        #endregion

        #region Public Methods

        public static void Initialize()
        {
            InitializeStrings();
            RefreshLanguage();
            _initialized = true;
        }

        public static void RefreshLanguage()
        {
            // Tangledeep uses StringManager.gameLanguage (enum EGameLanguage)
            EGameLanguage gameLang = StringManager.gameLanguage;

            switch (gameLang)
            {
                // We can add more languages here as needed
                default:
                    _currentLang = "en";
                    break;
            }
        }

        public static string Get(string key)
        {
            if (!_initialized) Initialize();

            var dict = GetCurrentDictionary();

            if (dict.TryGetValue(key, out string value))
                return value;

            if (_english.TryGetValue(key, out string engValue))
                return engValue;

            return key;
        }

        public static string Get(string key, params object[] args)
        {
            string template = Get(key);
            try
            {
                return string.Format(template, args);
            }
            catch
            {
                return template;
            }
        }

        #endregion

        #region Private Methods

        private static Dictionary<string, string> GetCurrentDictionary()
        {
            switch (_currentLang)
            {
                default: return _english;
            }
        }

        private static void Add(string key, string english)
        {
            _english[key] = english;
        }

        private static void InitializeStrings()
        {
            // ===== GENERAL =====
            Add("mod_loaded", "TangledeepAccess loaded. Shift F1 for help.");
            Add("help_title", "Help:");
            Add("help_full", "Shift F1 Mod help. F1 Game help. F2 Status. F3 Radar on off. F4 Details or enemy info. Shift H Hotbar. Shift L Message log. C Character sheet, Tab and Shift Tab for sections. G Gold. Shift Q Quests. Shift M Floor overview. Shift N Adjacent tiles. Shift A Auto attack nearest enemy. Tab target nearest enemy, Tab and Shift Tab to cycle. Keys 1 through 8 use hotbar skills. Shift Left and Right cycle radar categories. Shift Up and Down cycle radar items. O walk to radar target. L look around. R randomize name. L in settings to cycle language. F12 Debug.");
            Add("help_world", "World keys: Arrow keys move. F2 Status. F3 Radar. F4 Enemy info on radar target. Shift H Hotbar. Shift L Message log. G Gold. C Character sheet. Shift Q Quests. Shift M Floor overview. Shift N Adjacent tiles. Shift A Auto attack. Tab target enemy. Keys 1 to 8 use hotbar. Shift Arrows cycle radar. O walk to target. L look around. E auto explore. Escape open menu.");
            Add("help_menu", "Menu keys: Arrow keys navigate. Enter select. Escape close. Tab and Shift Tab cycle tabs or sections. F4 item details.");
            Add("help_targeting", "Targeting keys: Arrow keys move cursor. Tab and Shift Tab cycle enemies. Enter confirm. Escape cancel.");
            Add("help_settings", "Settings keys: Arrow keys navigate. Enter toggle or adjust. Left and Right change slider. L cycle language. Escape close.");
            Add("help_dialog", "Dialog keys: Arrow keys browse options. Enter select. Escape back.");
            Add("help_creation", "Creation keys: Arrow keys navigate. Enter select. R randomize name.");
            Add("log_header", "Recent messages:");
            Add("log_empty", "No recent messages.");
            Add("debug_enabled", "Debug mode enabled.");
            Add("debug_disabled", "Debug mode disabled.");
            Add("ui_checked", "Checked");
            Add("ui_press_enter_continue", "Press Enter to continue.");
            Add("ui_press_enter_close", "Press Enter to close.");

            // ===== STATS =====
            Add("stat_health", "Health");
            Add("stat_stamina", "Stamina");
            Add("stat_energy", "Energy");
            Add("stat_gold", "Gold: {0}");
            Add("stat_quests", "Quests: {0}");
            Add("stat_no_quests", "No active quests.");
            Add("stat_low_alert", "{0} low: {1} percent!");
            Add("stat_full", "{0} full.");
            Add("stat_status", "{0}: {1} of {2}");

            // ===== COMBAT LOG =====
            Add("log_player_hit", "You strike {0} for {1} damage!");
            Add("log_enemy_hit", "{0} hits you for {1} damage!");
            Add("log_player_miss", "You miss {0}.");
            Add("log_enemy_miss", "{0} misses you.");
            Add("log_player_defeat_enemy", "You have defeated {0}!");

            // ===== TARGETING =====
            Add("targeting_started", "Targeting");
            Add("targeting_ended", "Targeting ended.");
            Add("targeting_no_targets", "No enemies nearby.");
            Add("targeting_enemy_info", "{0}, {1} tiles {2}");
            Add("targeting_cursor_at", "{0}, {1} tiles {2}");

            // ===== MAIN MENU =====
            Add("menu_main_title", "Main Menu");
            Add("menu_select_slot_title", "Select Save Slot");

            // ===== SAVE SLOTS =====
            Add("save_slot_unknown", "Unknown slot");
            Add("save_slot_empty", "Slot {0}: Empty");
            Add("save_slot_info", "Slot {0}: {1}, Level {2} {3}, {4}, {5} played");

            // ===== NAME ENTRY / CONFIRM =====
            Add("name_entry_screen", "Enter your name. Current name: {0}. {1}. {2}. {3}.");
            Add("name_confirm_ready", "Are you ready? Character name: {0}.");
            Add("name_enter_to_edit", "Enter to edit name");
            Add("name_r_to_randomize", "R to randomize");
            Add("name_enter_twice_to_continue", "Enter twice to continue");
            Add("cs_header", "{0}, Level {1} {2}");
            Add("cs_navigate_hint", "Use arrows to browse stats for details");
            Add("cs_section_hint", "Tab and Shift Tab to browse sections. Arrows for stat details.");
            Add("cs_weapon", "Weapon: {0}");
            Add("cs_floor", "{0}, Floor {1}");
            Add("cs_feats", "Feats: {0}");
            Add("cs_section_overview", "Overview");
            Add("cs_section_core_stats", "Core Stats");
            Add("cs_section_combat", "Combat Stats");
            Add("cs_section_resistances", "Resistances");
            Add("cs_section_damage_bonuses", "Damage Bonuses");
            Add("cs_section_status", "Status Effects and Feats");
            Add("creation_job", "Job: {0}");
            Add("creation_mode", "Mode: {0}");
            Add("creation_feats", "Feats: {0}");
            Add("name_confirm_begin", "Begin Game");
            Add("name_confirm_back", "Go Back");
            Add("name_confirm_seed", "World Seed");

            // ===== UI TABS =====
            Add("ui_tab_equipment", "Equipment");
            Add("ui_tab_inventory", "Inventory");
            Add("ui_tab_skills", "Skills");
            Add("ui_tab_character", "Character Sheet");
            Add("ui_tab_rumors", "Rumors");
            Add("ui_tab_options", "Options");

            // ===== AUTO NAVIGATION =====
            Add("auto_walking_to", "Walking to {0}. Press any key to stop.");
            Add("auto_stopped", "Auto-navigation stopped.");
            Add("auto_stopped_combat", "Taking damage! Auto-navigation stopped.");
            Add("auto_no_path", "No path found.");
            Add("auto_no_radar_target", "No radar target. Press F3 to scan, then Page Up and Page Down to pick a target.");
            Add("auto_arrived", "Arrived at {0}.");

            // ===== STATUS EFFECTS =====
            Add("status_gained", "Gained {0}");
            Add("status_lost", "Lost {0}");
            Add("status_list", "Active effects: {0}");
            Add("status_none", "No active status effects.");

            // ===== RADAR =====
            Add("radar_no_map", "No map available.");
            Add("radar_nothing", "Nothing detected nearby.");
            Add("radar_scan_done", "Radar: {0} detected.");
            Add("radar_cat_count", "{0}: {1}");
            Add("radar_category", "{0}, {1} found.");
            Add("radar_cat_empty", "No {0} found.");
            Add("radar_item_simple", "{0}, {1} tiles {2}");
            Add("radar_nothing_tracked", "Nothing currently tracked.");
            Add("radar_here", "{0} is here.");
            Add("radar_off", "Radar off.");

            // ===== RADAR CATEGORIES =====
            Add("radar_cat_enemies", "Enemies");
            Add("radar_cat_npcs", "NPCs");
            Add("radar_cat_shops", "Shops");
            Add("radar_cat_items", "Items");
            Add("radar_cat_stairs", "Stairs");
            Add("radar_cat_destructibles", "Destructibles");
            Add("radar_cat_terrain", "Terrain");

            // ===== ENEMY INFO =====
            Add("enemy_hp", "Health {0} percent");
            Add("enemy_level", "Level {0}");
            Add("enemy_boss", "Boss");
            Add("enemy_champion", "Champion");
            Add("enemy_family", "Type: {0}");
            Add("enemy_threat", "Threat: {0}");
            Add("enemy_hostile", "Hostile");
            Add("enemy_curious", "Curious");
            Add("enemy_stalking", "Stalking");
            Add("enemy_aggressive", "Aggressive");
            Add("enemy_fleeing", "Fleeing");
            Add("enemy_neutral", "Neutral");
            Add("enemy_champion_mods", "Mods: {0}");
            Add("enemy_resistances", "Resistances: {0}");
            Add("enemy_statuses", "Effects: {0}");

            // ===== DIRECTIONS =====
            Add("dir_here", "Here");
            Add("dir_north", "North");
            Add("dir_south", "South");
            Add("dir_east", "East");
            Add("dir_west", "West");
            Add("dir_northeast", "North East");
            Add("dir_northwest", "North West");
            Add("dir_southeast", "South East");
            Add("dir_southwest", "South West");

            // ===== WORLD =====
            Add("world_unknown", "Unknown");
            Add("world_current_tile", "Current tile");
            Add("world_around_empty", "Nothing but ground nearby.");
            Add("world_wall", "Wall");
            Add("world_items_here", "Items here:");
            Add("world_stairs_here", "Stairs here.");
            Add("stairs_up", "Stairs up");
            Add("stairs_down", "Stairs down");
            Add("area_summary", "Current area: {0}");

            // ===== ITEM RARITY =====
            Add("rarity_uncommon", "Uncommon");
            Add("rarity_magical", "Magical");
            Add("rarity_ancient", "Ancient");
            Add("rarity_artifact", "Artifact");
            Add("rarity_legendary", "Legendary");
            Add("rarity_gearset", "Set");

            // ===== EQUIPMENT COMPARISON =====
            Add("equip_power", "Power");
            Add("equip_defense", "Defense");
            Add("equip_block", "Block");

            // ===== DIALOG CHOICES =====
            Add("dialog_choice", "Option {0} of {1}: {2}");
            Add("dialog_choices_available", "{0} options available.");

            // ===== HOTBAR =====
            Add("hotbar_slot", "Press {0}: {1}");
            Add("hotbar_empty_all", "Hotbar is empty. Learn skills from the Skills menu and they will be assigned here.");
            Add("hotbar_switched", "Hotbar {0} active. Press Shift H to read.");
            Add("hotbar_consumable", "{0}, quantity {1}");

            // ===== SKILL SHEET =====
            Add("skills_assign_mode", "Skills: Assign to hotbar. Select a skill to place it on your hotbar.");
            Add("skills_learn_mode", "Skills: Learn new skills. Spend JP to learn or master abilities.");
            Add("skill_cooldown", "Cooldown {0} turns");
            Add("skill_range", "Range {0}");
            Add("skill_passive", "Passive");
            Add("skill_toggled", "Active");
            Add("skill_on_cooldown", "On cooldown: {0} turns remaining");
            Add("skill_used", "{0} used");

            // ===== LEVEL UP / XP =====
            Add("level_up", "Level up! Now level {0}.");
            Add("xp_progress", "XP: {0} of {1}, level {2}.");

            // ===== LOOT =====
            Add("loot_picked_up", "Picked up {0}.");
            Add("loot_gold", "Plus {0} gold.");

            // ===== MAP TRANSITIONS =====
            Add("map_entered", "Entered {0}, floor {1}.");

            // ===== SHOP =====
            Add("shop_price", "{0} gold");
            Add("shop_sell_price", "Sells for {0} gold");

            // ===== HAZARDS =====
            Add("hazard_warning", "Warning: {0}!");
            Add("hazard_trap", "Trap");

            // ===== BOSS =====
            Add("monster_charging", "Warning: {0} is preparing {1}!");
            Add("monster_charge_generic", "an ability");
            Add("boss_appeared", "Boss: {0}!");
            Add("boss_hp", "Boss health: {0} percent.");
            Add("boss_defeated", "Boss defeated!");

            // ===== DEATH =====
            Add("player_died", "You have died.");

            // ===== PET / SUMMON =====
            Add("pet_status", "Pet: {0}, Health {1} of {2}.");
            Add("pet_none", "No active pet.");

            // ===== AUTO ATTACK =====
            Add("autoattack_no_target", "No enemy in range.");
            Add("autoattack_paralyzed", "You are paralyzed!");
            Add("autoattack_swing", "Attack {0}, {1}");

            // ===== FLOOR OVERVIEW =====
            Add("floor_enemies", "{0} enemies");
            Add("floor_npcs", "{0} NPCs");
            Add("floor_items", "{0} items on ground");
            Add("floor_explored", "{0} percent explored");
            Add("adjacent_clear", "All clear around you.");

            // ===== TILE TYPES =====
            Add("tile_ground", "Ground");
            Add("tile_water", "Water");
            Add("tile_grass", "Grass");
            Add("tile_lava", "Lava");
            Add("tile_mud", "Mud");
            Add("tile_electric", "Electric tile");

            // ===== JOURNAL =====
            Add("journal_recipes", "Recipes");
            Add("journal_rumors", "Rumors");
            Add("journal_combatlog", "Combat Log");
            Add("journal_monsterpedia", "Monster Pedia");

            // ===== ITEM WORLD =====
            Add("itemworld_selected", "Selected: {0}");

            // ===== SETTINGS / OPTIONS =====
            Add("option_changed", "{0}: {1}");
            Add("option_on", "On");
            Add("option_off", "Off");
            Add("option_of", "of");
            Add("option_slider_hint", "Press Enter to adjust, then left and right arrows.");

            // ===== LANGUAGE =====
            Add("language_changed", "Language changed to {0}. Please restart the game.");

            // ===== JOB POINTS =====
            Add("stat_jp", "JP: {0}");

            // ===== JOB CHANGE =====
            Add("jobchange_opened", "Job Change. Cost: {0} gold. Select a job to switch to.");
            Add("jobchange_complete", "Changed job to {0}.");

            // ===== RECIPES =====
            Add("recipe_requires", "Requires: {0}");
            Add("recipe_in_inventory", "{0} in inventory");

            // ===== CRAFTING =====
            Add("crafting_opened", "Crafting. Select items to craft.");

            // ===== CORRAL =====
            Add("corral_opened", "Monster Corral. {0} monsters.");
            Add("corral_food_opened", "Feed {0}. Select food from inventory.");
            Add("corral_groom_opened", "Groom {0}.");
            Add("corral_happiness", "Happiness: {0}");
            Add("corral_beauty", "Beauty: {0}");

            // ===== CASINO =====
            Add("casino_won", "You won {0} gold!");
            Add("casino_bet", "Bet: {0} gold.");

            // ===== COMBAT DETAIL =====
            Add("combat_crit_dealt", "Critical hit!");
            Add("combat_crit_received", "Critical hit on you!");

            // ===== TARGETING SHAPES =====
            Add("shape_point", "Single target");
            Add("shape_burst", "Burst area");
            Add("shape_circle", "Circle area");
            Add("shape_cross", "Cross pattern");
            Add("shape_line", "Line");
            Add("shape_cone", "Cone");
            Add("shape_area", "Area");

            // ===== ITEM WORLD =====
            Add("itemworld_opened", "Item Dream. {0} items. Select an item to enchant.");
            Add("itemworld_select_orb", "Select an orb. {0} orbs available.");
        }

        #endregion
    }
}
