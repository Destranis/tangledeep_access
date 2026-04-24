using UnityEngine;
using HarmonyLib;
using System.Collections.Generic;

namespace TangledeepAccess
{
    /// <summary>
    /// Handles status effect announcements (buffs, debuffs, stances).
    /// NOTE: Cannot use [HarmonyPatch] attributes for StatBlock because its static
    /// constructor calls StringManager before it's ready, crashing PatchAll().
    /// These patches are applied manually via Patches.ApplyDeferredPatches().
    /// </summary>
    public class StatusEffectHandler
    {
        public static class Patch_StatBlock_AddStatus
        {
            public static void Postfix(StatBlock __instance, StatusEffect se)
            {
                if (se == null) return;

                var owner = (Fighter)AccessTools.Field(typeof(StatBlock), "owner").GetValue(__instance);

                if (owner != null && owner.GetActorType() == ActorTypes.HERO)
                {
                    string name = UIHandler.CleanText(se.abilityName);
                    ScreenReader.Say(Loc.Get("status_gained", name));
                }
            }
        }

        public static class Patch_StatBlock_RemoveStatus
        {
            public static void Postfix(StatBlock __instance, StatusEffect se)
            {
                if (se == null) return;

                var owner = (Fighter)AccessTools.Field(typeof(StatBlock), "owner").GetValue(__instance);

                if (owner != null && owner.GetActorType() == ActorTypes.HERO)
                {
                    string name = UIHandler.CleanText(se.abilityName);
                    ScreenReader.Say(Loc.Get("status_lost", name));
                }
            }
        }
    }
}
