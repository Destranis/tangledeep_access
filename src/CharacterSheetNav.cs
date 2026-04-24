using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace TangledeepAccess
{
    /// <summary>
    /// Section-based character sheet navigation.
    /// Page Down / Page Up cycles through sections. Arrows still work for game's built-in stat hover.
    /// </summary>
    public static class CharacterSheetNav
    {
        private static int _currentSection = -1;
        private static bool _sheetOpen = false;

        // Section indices
        private const int SECTION_OVERVIEW = 0;
        private const int SECTION_CORE_STATS = 1;
        private const int SECTION_COMBAT = 2;
        private const int SECTION_RESISTANCES = 3;
        private const int SECTION_DAMAGE_BONUSES = 4;
        private const int SECTION_STATUS_EFFECTS = 5;
        private const int SECTION_COUNT = 6;

        private static readonly string[] _sectionNames =
        {
            "cs_section_overview",
            "cs_section_core_stats",
            "cs_section_combat",
            "cs_section_resistances",
            "cs_section_damage_bonuses",
            "cs_section_status"
        };

        /// <summary>
        /// Called from Patch_CharacterSheet when sheet is updated/opened.
        /// </summary>
        public static void OnSheetUpdated()
        {
            if (!_sheetOpen)
            {
                _sheetOpen = true;
                _currentSection = -1;
                AnnounceHeader();
            }
        }

        /// <summary>
        /// Called from Main.Update to handle section cycling keys.
        /// </summary>
        public static void Update()
        {
            bool isOpen = UIManagerScript.GetWindowState(UITabs.CHARACTER);

            if (!isOpen && _sheetOpen)
            {
                _sheetOpen = false;
                _currentSection = -1;
                return;
            }

            if (!isOpen) return;

            if (Input.GetKeyDown(KeyCode.Tab))
            {
                bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                if (shift)
                    _currentSection = (_currentSection <= 0) ? SECTION_COUNT - 1 : _currentSection - 1;
                else
                    _currentSection = (_currentSection >= SECTION_COUNT - 1) ? 0 : _currentSection + 1;
                AnnounceSection(_currentSection);
            }
        }

        private static void AnnounceHeader()
        {
            var hero = GameMasterScript.heroPCActor;
            if (hero == null || hero.myStats == null) return;

            var parts = new List<string>();

            string jobName = hero.myJob?.DisplayName ?? "";
            int level = hero.myStats.GetLevel();
            parts.Add(Loc.Get("cs_header", hero.displayName, level, jobName));

            // Vitals
            parts.Add(FormatVital(StatTypes.HEALTH, "stat_health"));
            parts.Add(FormatVital(StatTypes.STAMINA, "stat_stamina"));
            parts.Add(FormatVital(StatTypes.ENERGY, "stat_energy"));

            // Weapon
            var weapon = hero.myEquipment?.GetWeapon();
            if (weapon != null)
                parts.Add(Loc.Get("cs_weapon", weapon.displayName));

            parts.Add(Loc.Get("cs_section_hint"));

            ScreenReader.Say(string.Join(". ", parts));
        }

        private static void AnnounceSection(int section)
        {
            string sectionLabel = Loc.Get(_sectionNames[section]);
            string content = GetSectionContent(section);
            string text = sectionLabel + ". " + content;
            ScreenReader.Say(text);
        }

        private static string GetSectionContent(int section)
        {
            var hero = GameMasterScript.heroPCActor;
            if (hero == null || hero.myStats == null) return "";

            var parts = new List<string>();

            switch (section)
            {
                case SECTION_OVERVIEW:
                    string jobName = hero.myJob?.DisplayName ?? "";
                    int level = hero.myStats.GetLevel();
                    parts.Add(Loc.Get("cs_header", hero.displayName, level, jobName));
                    parts.Add(FormatVital(StatTypes.HEALTH, "stat_health"));
                    parts.Add(FormatVital(StatTypes.STAMINA, "stat_stamina"));
                    parts.Add(FormatVital(StatTypes.ENERGY, "stat_energy"));
                    // XP
                    AddTextField(parts, "XP", UIManagerScript.csExperience, formatXP: true);
                    // Gold
                    parts.Add(Loc.Get("stat_gold", hero.GetMoney()));
                    // Weapon
                    var weapon = hero.myEquipment?.GetWeapon();
                    if (weapon != null)
                        parts.Add(Loc.Get("cs_weapon", weapon.displayName));
                    // Area
                    var map = MapMasterScript.activeMap;
                    if (map != null)
                        parts.Add(Loc.Get("cs_floor", map.GetName(), map.floor));
                    break;

                case SECTION_CORE_STATS:
                    AddStatField(parts, "Strength", UIManagerScript.csStrength);
                    AddStatField(parts, "Swiftness", UIManagerScript.csSwiftness);
                    AddStatField(parts, "Spirit", UIManagerScript.csSpirit);
                    AddStatField(parts, "Discipline", UIManagerScript.csDiscipline);
                    AddStatField(parts, "Guile", UIManagerScript.csGuile);
                    break;

                case SECTION_COMBAT:
                    AddStatField(parts, "Weapon Power", UIManagerScript.csWeaponPower);
                    AddStatField(parts, "Spirit Power", UIManagerScript.csSpiritPower);
                    AddStatField(parts, "Crit Chance", UIManagerScript.csCritChance);
                    AddStatField(parts, "Crit Damage", UIManagerScript.csCritDamage);
                    AddStatField(parts, "Charge Time", UIManagerScript.csChargeTime);
                    AddStatField(parts, "Parry", UIManagerScript.csParryChance);
                    AddStatField(parts, "Block", UIManagerScript.csBlockChance);
                    AddStatField(parts, "Dodge", UIManagerScript.csDodgeChance);
                    AddStatField(parts, "All Damage", UIManagerScript.csDamageMod);
                    AddStatField(parts, "All Defense", UIManagerScript.csDefenseMod);
                    AddStatField(parts, "Powerup Drop", UIManagerScript.csPowerupDrop);
                    break;

                case SECTION_RESISTANCES:
                    AddStatField(parts, "Physical", UIManagerScript.csPhysicalResist);
                    AddStatField(parts, "Fire", UIManagerScript.csFireResist);
                    AddStatField(parts, "Poison", UIManagerScript.csPoisonResist);
                    AddStatField(parts, "Water", UIManagerScript.csWaterResist);
                    AddStatField(parts, "Lightning", UIManagerScript.csLightningResist);
                    AddStatField(parts, "Shadow", UIManagerScript.csShadowResist);
                    break;

                case SECTION_DAMAGE_BONUSES:
                    AddStatField(parts, "Physical", UIManagerScript.csPhysicalDamage);
                    AddStatField(parts, "Fire", UIManagerScript.csFireDamage);
                    AddStatField(parts, "Poison", UIManagerScript.csPoisonDamage);
                    AddStatField(parts, "Water", UIManagerScript.csWaterDamage);
                    AddStatField(parts, "Lightning", UIManagerScript.csLightningDamage);
                    AddStatField(parts, "Shadow", UIManagerScript.csShadowDamage);
                    break;

                case SECTION_STATUS_EFFECTS:
                    string statusText = UIHandler.CleanText(UIManagerScript.csStatusEffects?.text);
                    if (!string.IsNullOrEmpty(statusText))
                        parts.Add(statusText);
                    else
                        parts.Add(Loc.Get("status_none"));

                    string featsText = UIHandler.CleanText(UIManagerScript.csFeatsText?.text);
                    if (!string.IsNullOrEmpty(featsText))
                        parts.Add(Loc.Get("cs_feats", featsText));
                    break;
            }

            return string.Join(". ", parts);
        }

        private static string FormatVital(StatTypes type, string locKey)
        {
            var stats = GameMasterScript.heroPCActor.myStats;
            int cur = Mathf.RoundToInt(stats.GetCurStat(type));
            int max = Mathf.RoundToInt(stats.GetMaxStat(type));
            return Loc.Get("stat_status", Loc.Get(locKey), cur, max);
        }

        private static void AddStatField(List<string> parts, string label, TMPro.TextMeshProUGUI field)
        {
            if (field == null) return;
            string val = UIHandler.CleanText(field.text);
            if (!string.IsNullOrEmpty(val))
                parts.Add(label + " " + val);
        }

        private static void AddTextField(List<string> parts, string label, TMPro.TextMeshProUGUI field, bool formatXP = false)
        {
            if (field == null) return;
            string raw = UIHandler.CleanText(field.text);
            if (string.IsNullOrEmpty(raw)) return;

            if (formatXP)
            {
                string[] lines = raw.Split('\n');
                if (lines.Length >= 3)
                    parts.Add(label + ": " + lines[0].Trim() + " of " + lines[2].Trim());
                else if (lines.Length >= 1)
                    parts.Add(label + ": " + lines[0].Trim());
            }
            else
            {
                parts.Add(label + ": " + raw);
            }
        }
    }
}
