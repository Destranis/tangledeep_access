using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TangledeepAccess
{
    /// <summary>
    /// Builds enemy information strings for screen reader announcements.
    /// Provides brief (for cycling) and detailed (for F4/inspect) formats.
    /// </summary>
    public static class EnemyInfoHelper
    {
        /// <summary>
        /// Brief info for Tab cycling: name, HP%, behavior, threat, distance, direction.
        /// </summary>
        public static string GetBriefInfo(Actor actor, Vector2 playerPos)
        {
            var parts = new List<string>();
            string name = actor.displayName;

            if (actor is Monster mon)
            {
                // Boss/Champion prefix
                if (mon.isBoss)
                    name = Loc.Get("enemy_boss") + " " + name;
                else if (mon.isChampion)
                    name = Loc.Get("enemy_champion") + " " + name;
            }

            float dist = Vector2.Distance(playerPos, actor.GetPos());
            string dir = Main.World.GetDirectionName(actor.GetPos() - playerPos);
            parts.Add(Loc.Get("targeting_enemy_info", name, (int)dist, dir));

            // HP
            if (actor is Fighter fighter && fighter.myStats != null)
            {
                int curHP = (int)fighter.myStats.GetCurStat(StatTypes.HEALTH);
                int maxHP = (int)fighter.myStats.GetMaxStat(StatTypes.HEALTH);
                int hpPct = maxHP > 0 ? (curHP * 100 / maxHP) : 0;
                parts.Add(Loc.Get("enemy_hp", hpPct));
            }

            // Behavior
            if (actor is Monster m)
            {
                string behavior = GetBehaviorString(m);
                if (!string.IsNullOrEmpty(behavior))
                    parts.Add(behavior);
            }

            return string.Join(", ", parts);
        }

        /// <summary>
        /// Detailed info for F4/inspect: everything a sighted player sees on hover.
        /// </summary>
        public static string GetDetailedInfo(Actor actor, Vector2 playerPos)
        {
            var parts = new List<string>();

            if (!(actor is Monster mon) || mon.myStats == null)
                return GetBriefInfo(actor, playerPos);

            // Name with boss/champion
            string name = mon.displayName;
            if (mon.isBoss)
                name = Loc.Get("enemy_boss") + " " + name;
            else if (mon.isChampion)
                name = Loc.Get("enemy_champion") + " " + name;
            parts.Add(name);

            // Level
            parts.Add(Loc.Get("enemy_level", mon.myStats.GetLevel()));

            // Family
            if (!string.IsNullOrEmpty(mon.monFamily))
            {
                string family = Monster.GetFamilyName(mon.monFamily);
                if (!string.IsNullOrEmpty(family))
                    parts.Add(Loc.Get("enemy_family", family));
            }

            // HP
            int curHP = (int)mon.myStats.GetCurStat(StatTypes.HEALTH);
            int maxHP = (int)mon.myStats.GetMaxStat(StatTypes.HEALTH);
            int hpPct = maxHP > 0 ? (curHP * 100 / maxHP) : 0;
            parts.Add(Loc.Get("enemy_hp", hpPct));

            // Threat level
            string threat = UIHandler.CleanText(mon.EvaluateThreatToPlayer());
            if (!string.IsNullOrEmpty(threat))
                parts.Add(Loc.Get("enemy_threat", threat));

            // Behavior
            string behavior = GetBehaviorString(mon);
            if (!string.IsNullOrEmpty(behavior))
                parts.Add(behavior);

            // Champion mods
            if (mon.isChampion && mon.championMods != null)
            {
                var modNames = mon.championMods
                    .Where(cm => cm != null && cm.displayNameOnHover && !string.IsNullOrEmpty(cm.displayName))
                    .Select(cm => UIHandler.CleanText(cm.displayName))
                    .ToList();
                if (modNames.Count > 0)
                    parts.Add(Loc.Get("enemy_champion_mods", string.Join(", ", modNames)));
            }

            // Resistances
            string resists = UIHandler.CleanText(mon.GetMonsterResistanceString(false));
            if (!string.IsNullOrEmpty(resists))
                parts.Add(Loc.Get("enemy_resistances", resists));

            // Status effects
            var statuses = mon.myStats.GetAllStatuses();
            if (statuses != null && statuses.Count > 0)
            {
                var statusNames = statuses
                    .Where(s => s != null && s.showIcon && !string.IsNullOrEmpty(s.abilityName))
                    .Select(s => UIHandler.CleanText(s.abilityName))
                    .ToList();
                if (statusNames.Count > 0)
                    parts.Add(Loc.Get("enemy_statuses", string.Join(", ", statusNames)));
            }

            // Distance and direction
            float dist = Vector2.Distance(playerPos, actor.GetPos());
            string dir = Main.World.GetDirectionName(actor.GetPos() - playerPos);
            parts.Add((int)dist + " tiles " + dir);

            return string.Join(". ", parts);
        }

        private static string GetBehaviorString(Monster mon)
        {
            var hero = GameMasterScript.heroPCActor;

            if (mon.myBehaviorState == BehaviorState.FIGHT || mon.CheckTarget(hero))
                return Loc.Get("enemy_hostile");
            if (mon.myBehaviorState == BehaviorState.CURIOUS || mon.myBehaviorState == BehaviorState.SEEKINGITEM)
                return Loc.Get("enemy_curious");
            if (mon.myBehaviorState == BehaviorState.STALKING)
                return Loc.Get("enemy_stalking");
            if (mon.aggroRange > 0)
                return Loc.Get("enemy_aggressive");
            if (mon.myBehaviorState == BehaviorState.RUN)
                return Loc.Get("enemy_fleeing");
            if (mon.actorfaction != Faction.ENEMY)
                return Loc.Get("enemy_neutral");

            return "";
        }
    }
}
