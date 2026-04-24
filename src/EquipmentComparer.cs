using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace TangledeepAccess
{
    /// <summary>
    /// Utility to compare equipment and generate concise delta strings.
    /// </summary>
    public static class EquipmentComparer
    {
        public static string GetComparisonString(Equipment newItem)
        {
            if (newItem == null) return "";

            HeroPC hero = GameMasterScript.heroPCActor;
            if (hero == null) return "";

            // Find equipped item in the same slot
            Equipment equippedItem = null;
            if (newItem.slot == EquipmentSlots.WEAPON)
            {
                // In Tangledeep, the 'active' weapon is what we should compare against
                equippedItem = hero.myEquipment.GetWeapon();
            }
            else if (newItem.slot != EquipmentSlots.COUNT)
            {
                equippedItem = hero.myEquipment.equipment[(int)newItem.slot];
            }

            if (equippedItem == null) return "";
            if (equippedItem == newItem) return ""; // Already equipped

            List<string> deltas = new List<string>();

            // 1. Compare Primary Stats
            if (newItem is Weapon newWep && equippedItem is Weapon oldWep)
            {
                AddDelta(deltas, Loc.Get("equip_power"), newWep.power, oldWep.power);
            }
            else if (newItem is Armor newArm && equippedItem is Armor oldArm)
            {
                AddDelta(deltas, Loc.Get("equip_defense"), newArm.defense, oldArm.defense);
            }
            else if (newItem is Offhand newOff && equippedItem is Offhand oldOff)
            {
                AddDelta(deltas, Loc.Get("equip_block"), newOff.blockChance, oldOff.blockChance);
            }

            // 2. Compare Adventure Stats (Strength, Swiftness, Spirit, Discipline, Guile)
            // Indices: 0=STR, 1=SWI, 2=SPI, 3=DIS, 4=GUI
            string[] statKeys = { "stat_strength", "stat_swiftness", "stat_spirit", "stat_discipline", "stat_guile" };
            for (int i = 0; i < 5; i++)
            {
                AddDelta(deltas, StringManager.GetString(statKeys[i]), newItem.adventureStats[i], equippedItem.adventureStats[i]);
            }

            if (deltas.Count == 0) return "";

            return string.Join(", ", deltas);
        }

        private static void AddDelta(List<string> list, string statName, float newVal, float oldVal)
        {
            float diff = newVal - oldVal;
            if (Mathf.Abs(diff) < 0.01f) return;

            string sign = diff > 0 ? "+" : "";
            list.Add($"{statName} {sign}{diff:0.#}");
        }
    }
}
