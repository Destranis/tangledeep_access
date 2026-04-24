using UnityEngine;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;

namespace TangledeepAccess
{
    /// <summary>
    /// Handles vital stats (Health, Stamina, Energy) and announces changes.
    /// </summary>
    public class StatsHandler
    {
        private float _lastHealthPercent = 1f;
        private float _lastStaminaPercent = 1f;
        private float _lastEnergyPercent = 1f;

        private readonly float[] _thresholds = { 0.75f, 0.5f, 0.25f, 0.1f };

        public void Update()
        {
            if (!GameMasterScript.gameLoadSequenceCompleted) return;

            var hero = GameMasterScript.heroPCActor;
            if (hero == null || hero.myStats == null) return;

            CheckStat(StatTypes.HEALTH, ref _lastHealthPercent, "stat_health");
            CheckStat(StatTypes.STAMINA, ref _lastStaminaPercent, "stat_stamina");
            CheckStat(StatTypes.ENERGY, ref _lastEnergyPercent, "stat_energy");
        }

        private void CheckStat(StatTypes type, ref float lastPercent, string locKey)
        {
            float currentPercent = GameMasterScript.heroPCActor.myStats.GetCurStatAsPercentOfMax(type);
            
            // Only announce if crossing a threshold downwards
            foreach (float threshold in _thresholds)
            {
                if (currentPercent <= threshold && lastPercent > threshold)
                {
                    string statName = Loc.Get(locKey);
                    int percentInt = Mathf.RoundToInt(threshold * 100f);
                    ScreenReader.Say(Loc.Get("stat_low_alert", statName, percentInt));
                    break;
                }
            }

            // Also announce if healed significantly (crossing upwards)
            if (currentPercent >= 0.99f && lastPercent < 0.95f)
            {
                ScreenReader.Say(Loc.Get("stat_full", Loc.Get(locKey)));
            }

            lastPercent = currentPercent;
        }

        /// <summary>
        /// Full player status (Key: F2).
        /// Reads health, stamina, energy, XP, active effects, pet, and current area.
        /// </summary>
        public void AnnounceFullStatus()
        {
            var hero = GameMasterScript.heroPCActor;
            if (hero == null || hero.myStats == null) return;

            var parts = new List<string>();

            // Vitals
            parts.Add(GetStatString(StatTypes.HEALTH, "stat_health"));
            parts.Add(GetStatString(StatTypes.STAMINA, "stat_stamina"));
            parts.Add(GetStatString(StatTypes.ENERGY, "stat_energy"));

            // XP
            int curXP = hero.myStats.GetXP();
            int xpNeeded = hero.myStats.GetXPToNextLevel();
            int level = hero.myStats.GetLevel();
            parts.Add(Loc.Get("xp_progress", curXP, xpNeeded, level));

            // Job Points
            int jp = UnityEngine.Mathf.RoundToInt(hero.GetCurJP());
            parts.Add(Loc.Get("stat_jp", jp));

            // Active status effects (summary)
            var statuses = hero.myStats.GetAllStatuses();
            if (statuses.Count > 0)
            {
                string effectList = string.Join(", ", statuses.Select(s => UIHandler.CleanText(s.abilityName)));
                parts.Add(Loc.Get("status_list", effectList));
            }

            // Pet
            Monster pet = hero.GetMonsterPet();
            if (pet != null && pet.myStats != null)
            {
                int petHP = Mathf.RoundToInt(pet.myStats.GetCurStat(StatTypes.HEALTH));
                int petMaxHP = Mathf.RoundToInt(pet.myStats.GetMaxStat(StatTypes.HEALTH));
                parts.Add(Loc.Get("pet_status", pet.displayName, petHP, petMaxHP));
            }

            // Area
            var map = MapMasterScript.activeMap;
            if (map != null)
            {
                parts.Add(Loc.Get("area_summary", map.GetName()));
            }

            ScreenReader.Say(string.Join(". ", parts));
        }

        /// <summary>
        /// Announces current gold.
        /// </summary>
        public void AnnounceGold()
        {
            var hero = GameMasterScript.heroPCActor;
            if (hero == null) return;
            ScreenReader.Say(Loc.Get("stat_gold", hero.GetMoney()));
        }

        /// <summary>
        /// Announces active quests.
        /// </summary>
        public void AnnounceQuests()
        {
            var hero = GameMasterScript.heroPCActor;
            if (hero == null) return;

            if (hero.myQuests == null || hero.myQuests.Count == 0)
            {
                ScreenReader.Say(Loc.Get("stat_no_quests"));
                return;
            }

            var questParts = hero.myQuests
                .Where(q => q != null && !q.complete && !string.IsNullOrEmpty(q.displayName))
                .Select(q => q.displayName + (string.IsNullOrEmpty(q.questText) ? "" : ", " + UIHandler.CleanText(q.questText)));

            var list = questParts.ToList();
            if (list.Count == 0)
            {
                ScreenReader.Say(Loc.Get("stat_no_quests"));
                return;
            }

            ScreenReader.Say(Loc.Get("stat_quests", string.Join(". ", list)));
        }

        private string GetStatString(StatTypes type, string locKey)
        {
            var stats = GameMasterScript.heroPCActor.myStats;
            int cur = Mathf.RoundToInt(stats.GetCurStat(type));
            int max = Mathf.RoundToInt(stats.GetMaxStat(type));
            return Loc.Get("stat_status", Loc.Get(locKey), cur, max);
        }
    }
}
