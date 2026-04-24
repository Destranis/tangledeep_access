using HarmonyLib;
using UnityEngine;
using System.Text.RegularExpressions;
using System;
using System.Collections.Generic;

namespace TangledeepAccess
{
    /// <summary>
    /// All Harmony patches for the mod.
    /// StatBlock patches are deferred because StatBlock's static constructor
    /// calls StringManager before it's ready, crashing PatchAll().
    /// </summary>
    public static class Patches
    {
        /// <summary>
        /// Apply patches that can't run during PatchAll() due to early type initialization issues.
        /// Call this after the game is fully loaded.
        /// </summary>
        public static void ApplyDeferredPatches(Harmony harmony)
        {
            try
            {
                var adjustLevel = AccessTools.Method(typeof(StatBlock), nameof(StatBlock.AdjustLevel));
                var adjustLevelPostfix = AccessTools.Method(typeof(Patch_LevelUp), nameof(Patch_LevelUp.Postfix));
                harmony.Patch(adjustLevel, postfix: new HarmonyMethod(adjustLevelPostfix));

                var addStatus = AccessTools.Method(typeof(StatBlock), nameof(StatBlock.AddStatus));
                var addStatusPostfix = AccessTools.Method(typeof(StatusEffectHandler.Patch_StatBlock_AddStatus),
                    nameof(StatusEffectHandler.Patch_StatBlock_AddStatus.Postfix));
                harmony.Patch(addStatus, postfix: new HarmonyMethod(addStatusPostfix));

                var removeStatus = AccessTools.Method(typeof(StatBlock), nameof(StatBlock.RemoveStatus));
                var removeStatusPostfix = AccessTools.Method(typeof(StatusEffectHandler.Patch_StatBlock_RemoveStatus),
                    nameof(StatusEffectHandler.Patch_StatBlock_RemoveStatus.Postfix));
                harmony.Patch(removeStatus, postfix: new HarmonyMethod(removeStatusPostfix));

                Main.Log.LogInfo("Patches: Deferred StatBlock patches applied.");

                // Critical hit announcement (GetCriticalEffect is private)
                var getCritEffect = AccessTools.Method(typeof(CombatManagerScript), "GetCriticalEffect");
                var critPostfix = AccessTools.Method(typeof(Patch_CriticalHit), nameof(Patch_CriticalHit.Postfix));
                harmony.Patch(getCritEffect, postfix: new HarmonyMethod(critPostfix));
                Main.Log.LogInfo("Patches: Critical hit patch applied.");
            }
            catch (Exception ex)
            {
                Main.Log.LogError($"Patches: Failed to apply deferred patches: {ex}");
            }
        }

        [HarmonyPatch(typeof(UIManagerScript), nameof(UIManagerScript.ChangeUIFocus))]
        public static class Patch_Focus
        {
            public static void Postfix(UIManagerScript.UIObject obj)
            {
                if (obj == null) return;
                // Skip if the exact same UI object is re-focused (e.g. edge of list)
                if (obj == UIHandler.LastFocusedObject)
                    return;
                UIHandler.LastFocusedObject = obj;

                // If a dialog is open, we don't want to interrupt the NPC speech
                bool interrupt = !UIManagerScript.dialogBoxOpen;

                string text = UIHandler.GetTextFromUIObject(obj);
                if (!string.IsNullOrEmpty(text))
                {
                    UIHandler.LastFocusedText = text;
                    ScreenReader.Say(text, interrupt);
                }
            }
        }

        [HarmonyPatch(typeof(GameLogScript), nameof(GameLogScript.GameLogWrite))]
        public static class Patch_Log
        {
            public static void Postfix(string content)
            {
                string clean = UIHandler.CleanText(content);

                // Summarize common combat messages into concise screen reader output
                Match match;

                match = Regex.Match(clean, @"You hit (.*) for ([\d\.]+) damage", RegexOptions.IgnoreCase);
                if (match.Success) { ScreenReader.Say(Loc.Get("log_player_hit", match.Groups[1].Value, match.Groups[2].Value), false); return; }

                match = Regex.Match(clean, @"(.*) hits you for ([\d\.]+) damage", RegexOptions.IgnoreCase);
                if (match.Success) { ScreenReader.Say(Loc.Get("log_enemy_hit", match.Groups[1].Value, match.Groups[2].Value), false); return; }

                match = Regex.Match(clean, @"You miss(?:ed)? (.*?)[\.\!]?$", RegexOptions.IgnoreCase);
                if (match.Success) { ScreenReader.Say(Loc.Get("log_player_miss", match.Groups[1].Value), false); return; }

                match = Regex.Match(clean, @"(.*) miss(?:es|ed)? you", RegexOptions.IgnoreCase);
                if (match.Success) { ScreenReader.Say(Loc.Get("log_enemy_miss", match.Groups[1].Value), false); return; }

                match = Regex.Match(clean, @"(?:defeated|destroyed|killed) (.*?)[\.\!]?$", RegexOptions.IgnoreCase);
                if (match.Success) { ScreenReader.Say(Loc.Get("log_player_defeat_enemy", match.Groups[1].Value), false); return; }

                ScreenReader.Say(clean, false);
            }
        }

        [HarmonyPatch(typeof(UIManagerScript), nameof(UIManagerScript.CycleUITabs))]
        public static class Patch_Tabs
        {
            public static void Postfix()
            {
                try {
                    UITabs tab = UIManagerScript.GetUITabSelected();
                    string tabName = tab.ToString().ToLower();
                    string localized = Loc.Get("ui_tab_" + tabName);
                    ScreenReader.Say(localized);
                } catch (Exception) { }
            }
        }

        [HarmonyPatch(typeof(UIManagerScript), nameof(UIManagerScript.UpdateDialogBox))]
        public static class Patch_Dialog
        {
            public static void Postfix()
            {
                ReadCurrentDialogText();
            }
        }

        [HarmonyPatch(typeof(UIManagerScript), nameof(UIManagerScript.SwitchConversationBranch))]
        public static class Patch_SwitchBranch
        {
            public static void Postfix(TextBranch tb)
            {
                if (tb == null || string.IsNullOrEmpty(tb.text)) return;
                string clean = UIHandler.CleanText(tb.text);
                if (!string.IsNullOrEmpty(clean) && clean != UIHandler.LastDialogText)
                {
                    UIHandler.LastDialogText = clean;
                    ScreenReader.Say(clean, false);
                }
            }
        }

        /// <summary>
        /// Reads and announces dialog text from the current conversation branch or dialog box.
        /// Prefers the rendered TMPro text (fully resolved) over raw branch text.
        /// </summary>
        public static void ReadCurrentDialogText()
        {
            if (!UIManagerScript.dialogBoxOpen) return;

            string text = "";

            // Prefer the rendered dialog text (fully resolved with merge tags, button assignments, etc.)
            if (UIManagerScript.myDialogBoxComponent != null)
            {
                var tmp = UIManagerScript.myDialogBoxComponent.GetDialogText();
                if (tmp != null) text = tmp.text;
            }

            // Fallback to raw branch text
            if (string.IsNullOrEmpty(text) && UIManagerScript.currentTextBranch != null)
            {
                text = UIManagerScript.currentTextBranch.text;
            }

            string clean = UIHandler.CleanText(text);
            if (!string.IsNullOrEmpty(clean) && clean != UIHandler.LastDialogText)
            {
                UIHandler.LastDialogText = clean;
                ScreenReader.Say(clean, false);
            }
        }

        [HarmonyPatch(typeof(TitleScreenScript), "UpdateCursorDuringSlotSelection")]
        public static class Patch_SaveSlots
        {
            public static void Postfix(TitleScreenScript __instance)
            {
                if (TitleScreenScript.CreateStage != CreationStages.SELECTSLOT) return;
                if (!UIManagerScript.saveDataComponentsCreated) return;

                int slotIndex = (int)AccessTools.Field(typeof(TitleScreenScript), "idxActiveSaveSlotInMenu").GetValue(__instance);
                if (slotIndex == UIHandler.LastSaveSlotIndex) return;
                UIHandler.LastSaveSlotIndex = slotIndex;

                var components = UIManagerScript.saveDataDisplayComponents;
                if (components == null || slotIndex < 0 || slotIndex >= components.Length) return;

                string announcement = UIHandler.GetSaveSlotAnnouncement(components[slotIndex]);
                ScreenReader.Say(announcement);
            }
        }

        [HarmonyPatch(typeof(CharCreation), nameof(CharCreation.PrepareNameEntryPage))]
        public static class Patch_NameEntry
        {
            public static void Prefix()
            {
                // Set gamepad mode temporarily to trick game into not focusing input
                // Or just clear it in Postfix
            }

            public static void Postfix()
            {
                if (CharCreation.singleton != null)
                {
                    AccessTools.Field(typeof(CharCreation), "nameInputIsActive").SetValue(CharCreation.singleton, false);
                    if (CharCreation.nameInputTextBox != null)
                    {
                        CharCreation.nameInputTextBox.DeactivateInputField();
                    }
                }

                // Build character summary: job, mode, feats
                var parts = new System.Collections.Generic.List<string>();

                // Job
                string jobName = "";
                if (!RandomJobMode.IsCurrentGameInRandomJobMode() && !RandomJobMode.preparingEntryForRandomJobMode)
                {
                    var jobData = CharacterJobData.GetJobDataByEnum((int)GameStartData.jobAsEnum);
                    if (jobData != null) jobName = jobData.DisplayName;
                }
                else
                {
                    jobName = StringManager.GetString("job_wanderer");
                }
                if (!string.IsNullOrEmpty(jobName))
                    parts.Add(Loc.Get("creation_job", jobName));

                // Game mode
                string modeName = GameStartData.GetGameMode() switch
                {
                    GameModes.NORMAL => StringManager.GetLocalizedStringInCurrentLanguage("modename_heroic"),
                    GameModes.ADVENTURE => StringManager.GetLocalizedStringInCurrentLanguage("modename_adventure"),
                    GameModes.HARDCORE => StringManager.GetLocalizedStringInCurrentLanguage("modename_hardcore"),
                    _ => ""
                };
                if (RandomJobMode.IsCurrentGameInRandomJobMode() || RandomJobMode.preparingEntryForRandomJobMode)
                    modeName = StringManager.GetString("randomjob_mode");
                if (!string.IsNullOrEmpty(modeName))
                    parts.Add(Loc.Get("creation_mode", modeName));

                // Feats
                if (GameStartData.playerFeats != null && GameStartData.playerFeats.Count > 0)
                {
                    var featNames = new System.Collections.Generic.List<string>();
                    foreach (string featRef in GameStartData.playerFeats)
                    {
                        var feat = CreationFeat.FindFeatBySkillRef(featRef);
                        if (feat != null && !string.IsNullOrEmpty(feat.featName))
                            featNames.Add(feat.featName);
                    }
                    if (featNames.Count > 0)
                        parts.Add(Loc.Get("creation_feats", string.Join(", ", featNames)));
                }

                if (parts.Count > 0)
                    ScreenReader.Say(string.Join(". ", parts));

                string name = CharCreation.nameInputTextBox?.text ?? "";
                ScreenReader.Say(Loc.Get("name_entry_screen", name, Loc.Get("name_enter_to_edit"), Loc.Get("name_r_to_randomize"), Loc.Get("name_enter_twice_to_continue")), false);
            }
        }

        [HarmonyPatch(typeof(CharCreation), nameof(CharCreation.OnNameEntryBoxConfirm))]
        public static class Patch_NameConfirm
        {
            public static void Postfix()
            {
                UIHandler.LastConfirmOption = 0;
                string name = CharCreation.nameInputTextBox?.text ?? "";
                ScreenReader.Say(Loc.Get("name_confirm_ready", name) + " " + Loc.Get("name_confirm_begin"));
            }
        }

        [HarmonyPatch(typeof(CharCreation), nameof(CharCreation.HoverJobInfo))]
        public static class Patch_JobHover
        {
            public static void Postfix()
            {
                string text = CharCreation.jobDescText?.text;
                if (!string.IsNullOrEmpty(text)) ScreenReader.Say(UIHandler.CleanText(text), false);
            }
        }

        [HarmonyPatch(typeof(CharCreation), nameof(CharCreation.HoverSkillInfo))]
        public static class Patch_SkillHover
        {
            public static void Postfix()
            {
                string text = CharCreation.ccSkillHoverText?.text;
                if (!string.IsNullOrEmpty(text)) ScreenReader.Say(UIHandler.CleanText(text), false);
            }
        }

        [HarmonyPatch(typeof(DialogBoxScript), nameof(DialogBoxScript.EnableNextIcon))]
        public static class Patch_DialogNext
        {
            public static void Postfix()
            {
                ScreenReader.Say(Loc.Get("ui_press_enter_continue"), false);
            }
        }

        [HarmonyPatch(typeof(DialogBoxScript), nameof(DialogBoxScript.EnableCloseIcon))]
        public static class Patch_DialogClose
        {
            public static void Postfix()
            {
                ScreenReader.Say(Loc.Get("ui_press_enter_close"), false);
            }
        }

        [HarmonyPatch(typeof(CharCreation), nameof(CharCreation.HandleInputNameEntry_ConfirmedAndReady))]
        public static class Patch_NameConfirmNav
        {
            public static void Postfix()
            {
                int currentOption = (int)AccessTools.Field(typeof(CharCreation), "iSelectedConfirmCharacterOption").GetValue(CharCreation.singleton);
                if (currentOption == UIHandler.LastConfirmOption) return;
                UIHandler.LastConfirmOption = currentOption;

                string optionName = "";
                switch (currentOption)
                {
                    case 0: optionName = Loc.Get("name_confirm_begin"); break;
                    case 1: optionName = Loc.Get("name_confirm_back"); break;
                    case 2: optionName = Loc.Get("name_confirm_seed"); break;
                }
                if (!string.IsNullOrEmpty(optionName)) ScreenReader.Say(optionName);
            }
        }

        // ===== FEATURE: Skill Sheet Mode Announcement =====
        [HarmonyPatch(typeof(Switch_UISkillSheet), nameof(Switch_UISkillSheet.EnterNewMode))]
        public static class Patch_SkillSheetMode
        {
            public static void Postfix(ESkillSheetMode newMode)
            {
                string modeName;
                switch (newMode)
                {
                    case ESkillSheetMode.assign_abilities:
                        modeName = Loc.Get("skills_assign_mode");
                        break;
                    case ESkillSheetMode.purchase_abilities:
                        modeName = Loc.Get("skills_learn_mode");
                        break;
                    default:
                        return;
                }
                ScreenReader.Say(modeName);
            }
        }

        // ===== FEATURE: Skill Sheet Item Selection =====
        [HarmonyPatch(typeof(Switch_UISkillSheet), nameof(Switch_UISkillSheet.DisplayItemInfo))]
        public static class Patch_SkillSheetDisplayItemInfo
        {
            public static void Postfix(ISelectableUIObject itemToDisplay)
            {
                if (itemToDisplay == null) return;
                if (!(itemToDisplay is AbilityScript ability)) return;

                string name = UIHandler.CleanText(ability.abilityName);
                var parts = new System.Collections.Generic.List<string> { name };

                // Costs
                string costs = UIHandler.CleanText(ability.GetDisplayCosts());
                if (!string.IsNullOrEmpty(costs))
                    parts.Add(costs);

                // Cooldown
                if (ability.maxCooldownTurns > 0)
                    parts.Add(Loc.Get("skill_cooldown", ability.maxCooldownTurns));

                // Range
                if (ability.range > 0)
                    parts.Add(Loc.Get("skill_range", ability.range));

                // Passive
                if (ability.passiveAbility)
                    parts.Add(Loc.Get("skill_passive"));

                // Toggled status
                if (ability.toggled)
                    parts.Add(Loc.Get("skill_toggled"));

                // Current cooldown
                int curCD = ability.GetCurCooldownTurns();
                if (curCD > 0)
                    parts.Add(Loc.Get("skill_on_cooldown", curCD));

                // Short description
                string desc = UIHandler.CleanText(ability.shortDescription);
                if (!string.IsNullOrEmpty(desc))
                    parts.Add(desc);

                ScreenReader.Say(string.Join(". ", parts));
            }
        }

        // ===== FEATURE: Ability Usage from Hotbar =====
        [HarmonyPatch(typeof(GameMasterScript), nameof(GameMasterScript.CheckAndTryAbility))]
        public static class Patch_AbilityUsage
        {
            public static void Postfix(AbilityScript abil)
            {
                if (abil == null) return;

                string name = UIHandler.CleanText(abil.abilityName);
                if (string.IsNullOrEmpty(name)) return;

                // If targeting started, the targeting handler will announce it
                if (UIManagerScript.singletonUIMS.CheckTargeting()) return;

                // Non-targeted ability used immediately
                ScreenReader.Say(Loc.Get("skill_used", name), false);
            }
        }

        // ===== FEATURE: Hotbar Switch Announcement =====
        [HarmonyPatch(typeof(UIManagerScript), nameof(UIManagerScript.ToggleSecondaryHotbar))]
        public static class Patch_HotbarSwitch
        {
            public static void Postfix()
            {
                int bar = UIManagerScript.GetIndexOfActiveHotbar() + 1;
                ScreenReader.Say(Loc.Get("hotbar_switched", bar), false);
            }
        }

        // ===== FEATURE: Cooking Result Announcement =====
        [HarmonyPatch(typeof(UIManagerScript), nameof(UIManagerScript.CookingIngredientHover))]
        public static class Patch_CookingHover
        {
            public static void Postfix(int slot)
            {
                if (UIManagerScript.cookingResultText == null) return;
                string text = UIHandler.CleanText(UIManagerScript.cookingResultText.text);
                if (!string.IsNullOrEmpty(text))
                {
                    ScreenReader.Say(text, false);
                }
            }
        }

        // ===== FEATURE 3: Level-Up Announcement =====
        // NOTE: Cannot use [HarmonyPatch] attribute for StatBlock because its static
        // constructor calls StringManager before it's ready, crashing PatchAll().
        // This patch is applied manually in Main.InitializeAll() via ApplyDeferredPatches().
        public static class Patch_LevelUp
        {
            public static void Postfix(StatBlock __instance, int amount)
            {
                if (amount <= 0) return;

                var owner = (Fighter)AccessTools.Field(typeof(StatBlock), "owner").GetValue(__instance);
                if (owner == null || owner.GetActorType() != ActorTypes.HERO) return;

                int newLevel = __instance.GetLevel();
                ScreenReader.Say(Loc.Get("level_up", newLevel));
            }
        }

        // ===== FEATURE 4: Loot Pickup Announcement =====
        [HarmonyPatch(typeof(GameMasterScript), nameof(GameMasterScript.LootAnItem))]
        public static class Patch_LootItem
        {
            public static void Postfix(Item lootedItem, Fighter looter)
            {
                if (lootedItem == null || looter == null) return;
                if (looter.GetActorType() != ActorTypes.HERO) return;

                string name = UIHandler.GetItemNameWithRarity(lootedItem);
                if (string.IsNullOrEmpty(name)) return;

                ScreenReader.Say(Loc.Get("loot_picked_up", name), false);
            }
        }

        // ===== FEATURE 5: Map Transition Announcement =====
        [HarmonyPatch(typeof(MapMasterScript), nameof(MapMasterScript.SwitchMaps))]
        public static class Patch_SwitchMaps
        {
            public static void Postfix(Map originDestinationMap, bool __result)
            {
                if (!__result || originDestinationMap == null) return;

                string mapName = originDestinationMap.GetName();
                int floor = originDestinationMap.floor;
                ScreenReader.Say(Loc.Get("map_entered", mapName, floor));
            }
        }
        // ===== FEATURE 6: Hazard Detection on Movement =====
        [HarmonyPatch(typeof(TileInteractions), nameof(TileInteractions.HandleEffectsForHeroMovingIntoTile))]
        public static class Patch_TileHazard
        {
            public static void Postfix(MapTileData mtd)
            {
                if (mtd == null) return;
                string hazard = null;
                if (mtd.CheckTag(LocationTags.LAVA)) hazard = Loc.Get("tile_lava");
                else if (mtd.CheckTag(LocationTags.ELECTRIC)) hazard = Loc.Get("tile_electric");
                else if (mtd.CheckTag(LocationTags.MUD)) hazard = Loc.Get("tile_mud");
                else if (mtd.CheckTag(LocationTags.WATER) || mtd.CheckTag(LocationTags.ISLANDSWATER)) hazard = Loc.Get("tile_water");

                if (hazard != null)
                    ScreenReader.Say(Loc.Get("hazard_warning", hazard), false);

                // Check for trap destructibles on the tile
                foreach (var actor in mtd.GetAllActors())
                {
                    if (actor is Destructible dt && dt.destroyOnStep && !dt.destroyed)
                    {
                        string name = !string.IsNullOrEmpty(dt.displayName) ? dt.displayName : Loc.Get("hazard_trap");
                        ScreenReader.Say(Loc.Get("hazard_warning", name), false);
                        break;
                    }
                }
            }
        }

        // ===== FEATURE 7: Gold Pickup Announcement =====
        [HarmonyPatch(typeof(HeroPC), nameof(HeroPC.ChangeMoney))]
        public static class Patch_GoldChange
        {
            public static void Postfix(HeroPC __instance, int amount)
            {
                if (amount > 0 && __instance.GetActorType() == ActorTypes.HERO)
                    ScreenReader.Say(Loc.Get("loot_gold", amount), false);
            }
        }

        // ===== FEATURE 8: Boss Health Tracking =====
        [HarmonyPatch(typeof(BossHealthBarScript), nameof(BossHealthBarScript.EnableBoss))]
        public static class Patch_BossAppear
        {
            public static void Postfix(Monster mn)
            {
                if (mn == null) return;
                ScreenReader.Say(Loc.Get("boss_appeared", mn.displayName));
            }
        }

        [HarmonyPatch(typeof(BossHealthBarScript), nameof(BossHealthBarScript.SetBossHealthFill))]
        public static class Patch_BossHealth
        {
            private static int _lastBossHpPct = -1;

            public static void Postfix(float amount)
            {
                int pct = (int)(amount * 100);
                // Announce at 75%, 50%, 25%, 10%, and 0%
                if (pct != _lastBossHpPct && (pct == 75 || pct == 50 || pct == 25 || pct == 10 || pct == 0))
                {
                    _lastBossHpPct = pct;
                    if (pct == 0)
                        ScreenReader.Say(Loc.Get("boss_defeated"), false);
                    else
                        ScreenReader.Say(Loc.Get("boss_hp", pct), false);
                }
            }
        }

        // ===== Suppress "View Help" when Shift+H is used for hotbar =====
        [HarmonyPatch(typeof(UIManagerScript), nameof(UIManagerScript.StartConversation))]
        public static class Patch_BlockViewHelp
        {
            public static bool Prefix(Conversation convo)
            {
                if (convo != null && convo.refName == "tutorial"
                    && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)))
                {
                    return false; // Block tutorial dialog when Shift is held
                }
                return true;
            }
        }


        // ===== FEATURE 9: Death / Game Over =====
        [HarmonyPatch(typeof(GameMasterScript), nameof(GameMasterScript.GameOver))]
        public static class Patch_GameOver
        {
            public static void Postfix()
            {
                ScreenReader.Say(Loc.Get("player_died"));
            }
        }

        // ===== FEATURE: Character Sheet Accessibility =====
        [HarmonyPatch(typeof(UIManagerScript), nameof(UIManagerScript.UpdatePlayerCharacterSheet))]
        public static class Patch_CharacterSheet
        {
            public static void Postfix()
            {
                if (!UIManagerScript.GetWindowState(UITabs.CHARACTER)) return;
                CharacterSheetNav.OnSheetUpdated();
            }
        }

        // ===== FEATURE: Character Sheet Stat Navigation =====
        // When navigating stats with arrows, HoverStatInfo builds a detailed tooltip.
        // Announce it so the user hears stat name, value, and all derived bonuses.
        [HarmonyPatch(typeof(UIManagerScript), nameof(UIManagerScript.HoverStatInfo))]
        public static class Patch_HoverStatInfo
        {
            public static void Postfix(int stat)
            {
                if (stat < 0) return;
                // The method just set charSheetStatInfo.text with the full tooltip
                if (UIManagerScript.charSheetStatInfo == null) return;
                string text = UIHandler.CleanText(UIManagerScript.charSheetStatInfo.text);
                if (!string.IsNullOrEmpty(text))
                    ScreenReader.Say(text);
            }
        }

        // ===== FEATURE: Monster Ability Preparation Announcement =====
        [HarmonyPatch(typeof(Monster), nameof(Monster.PrepareChargeTurn))]
        public static class Patch_MonsterCharge
        {
            public static void Postfix(Monster __instance, TurnData checkData)
            {
                if (__instance == null || checkData?.tAbilityToTry == null) return;
                // Only announce if the monster is visible to the player
                Vector2 pos = __instance.GetPos();
                if (!MapMasterScript.InBounds(pos)) return;
                if (!GameMasterScript.heroPCActor.visibleTilesArray[(int)pos.x, (int)pos.y]) return;

                string monsterName = __instance.displayName;
                string abilityName = UIHandler.CleanText(checkData.tAbilityToTry.abilityName);
                if (string.IsNullOrEmpty(abilityName))
                    abilityName = Loc.Get("monster_charge_generic");

                ScreenReader.Say(Loc.Get("monster_charging", monsterName, abilityName), false);
            }
        }

        // ===== FIX: Escape reopening last menu =====
        // The game's CheckForToggleMenuSelectInput opens the last-opened UI tab
        // when no menu is open. This is confusing for screen reader users who expect
        // Escape to do nothing when no menu is active.
        [HarmonyPatch(typeof(TDInputHandler), "CheckForToggleMenuSelectInput")]
        public static class Patch_SuppressLastMenuReopen
        {
            public static bool Prefix(ref bool __result)
            {
                if (TDInputHandler.player == null) return true;
                if (!TDInputHandler.player.GetButtonDown("Toggle Menu Select")) return true;

                // If no interactable window is open, block the method to prevent
                // reopening the last-used menu. Let the normal Options Menu handling run instead.
                if (!UIManagerScript.AnyInteractableWindowOpen())
                {
                    __result = false;
                    return false;
                }
                return true;
            }
        }
        // ===== FEATURE: Announce slider value when adjusting in options =====
        [HarmonyPatch(typeof(UIManagerScript), "ChangeSliderValue")]
        public static class Patch_OptionsSliderChange
        {
            public static void Postfix()
            {
                UIHandler.AnnounceSliderValue();
            }
        }

        // Language cycling is handled via hotkey in Main.ProcessHotkeys()

        // ===== FEATURE: Options Menu — Toggle announcement on state change =====
        [HarmonyPatch(typeof(UIManagerScript.UIObject), "DoOptionToggleAndGetState")]
        public static class Patch_OptionsToggle
        {
            public static void Postfix(UIManagerScript.UIObject __instance, bool __result)
            {
                if (__instance == null || __instance.gameObj == null) return;

                string label = "";
                var tmp = __instance.gameObj.GetComponentInChildren<TMPro.TextMeshProUGUI>();
                if (tmp != null) label = UIHandler.CleanText(tmp.text);
                if (string.IsNullOrEmpty(label)) label = __instance.gameObj.name;

                string state = __result ? Loc.Get("option_on") : Loc.Get("option_off");
                ScreenReader.Say(label + ": " + state);
            }
        }

        // ===== FEATURE: Journal Tab Switching =====
        [HarmonyPatch(typeof(JournalScript), nameof(JournalScript.SwitchJournalTab))]
        public static class Patch_JournalTab
        {
            public static void Postfix(int i)
            {
                string tabName;
                switch ((JournalTabs)i)
                {
                    case JournalTabs.RECIPES:
                        tabName = Loc.Get("journal_recipes");
                        break;
                    case JournalTabs.RUMORS:
                        tabName = Loc.Get("journal_rumors");
                        break;
                    case JournalTabs.COMBATLOG:
                        tabName = Loc.Get("journal_combatlog");
                        // Also read the combat log content
                        ReadJournalCombatLog();
                        break;
                    case JournalTabs.MONSTERPEDIA:
                        tabName = Loc.Get("journal_monsterpedia");
                        break;
                    default:
                        return;
                }
                ScreenReader.Say(tabName);
            }

            private static void ReadJournalCombatLog()
            {
                if (JournalScript.singleton == null) return;
                var sheet = JournalScript.singleton.GetComponentInChildren<UIJournalSheet>();
                if (sheet?.combatLogText == null) return;
                string text = UIHandler.CleanText(sheet.combatLogText.text);
                if (!string.IsNullOrEmpty(text))
                    ScreenReader.Say(text, false);
            }
        }

        // ===== FEATURE: Journal Quest/Rumor Content =====
        [HarmonyPatch(typeof(JournalScript), nameof(JournalScript.UpdateQuests))]
        public static class Patch_JournalQuests
        {
            public static void Postfix()
            {
                if (JournalScript.journalState != JournalTabs.RUMORS) return;

                var hero = GameMasterScript.heroPCActor;
                if (hero?.myQuests == null) return;

                var questParts = new List<string>();
                foreach (var q in hero.myQuests)
                {
                    if (q == null || q.complete || string.IsNullOrEmpty(q.displayName)) continue;
                    string entry = q.displayName;
                    if (!string.IsNullOrEmpty(q.questText))
                        entry += ": " + UIHandler.CleanText(q.questText);
                    questParts.Add(entry);
                }

                if (questParts.Count == 0)
                    ScreenReader.Say(Loc.Get("stat_no_quests"), false);
                else
                    ScreenReader.Say(string.Join(". ", questParts), false);
            }
        }

        // ===== FEATURE: Monster Pedia Entry =====
        [HarmonyPatch(typeof(JournalScript), nameof(JournalScript.UpdateMonsterPedia))]
        public static class Patch_MonsterPedia
        {
            public static void Postfix()
            {
                if (JournalScript.journalState != JournalTabs.MONSTERPEDIA) return;

                var sheet = JournalScript.singleton?.GetComponentInChildren<UIJournalSheet>();
                if (sheet?.monsterDescText == null) return;
                string text = UIHandler.CleanText(sheet.monsterDescText.text);
                if (!string.IsNullOrEmpty(text))
                    ScreenReader.Say(text, false);
            }
        }

        // ===== FEATURE: Item World UI =====
        [HarmonyPatch(typeof(ItemWorldUIScript), nameof(ItemWorldUIScript.SelectItem))]
        public static class Patch_ItemWorldUI
        {
            public static void Postfix()
            {
                if (!ItemWorldUIScript.itemWorldInterfaceOpen) return;
                if (ItemWorldUIScript.itemSelected == null) return;

                string name = UIHandler.CleanText(ItemWorldUIScript.itemSelected.displayName);
                ScreenReader.Say(Loc.Get("itemworld_selected", name), false);
            }
        }

        // ===== FEATURE: Item World — Browse Items/Orbs =====
        [HarmonyPatch(typeof(ItemWorldUIScript), nameof(ItemWorldUIScript.ShowItemInfo))]
        public static class Patch_ItemWorldShowInfo
        {
            public static void Postfix()
            {
                if (!ItemWorldUIScript.itemWorldInterfaceOpen) return;
                // Read the item name and description that ShowItemInfo just set
                string name = ItemWorldUIScript.itemInfoName != null ? UIHandler.CleanText(ItemWorldUIScript.itemInfoName.text) : "";
                string info = ItemWorldUIScript.itemInfoText != null ? UIHandler.CleanText(ItemWorldUIScript.itemInfoText.text) : "";
                if (!string.IsNullOrEmpty(name))
                    ScreenReader.Say(name + ". " + info, true);
            }
        }

        // ===== FEATURE: Item World — Open =====
        [HarmonyPatch(typeof(ItemWorldUIScript), nameof(ItemWorldUIScript.OpenItemWorldInterface))]
        public static class Patch_ItemWorldOpen
        {
            public static void Postfix()
            {
                int count = ItemWorldUIScript.playerItemList != null ? ItemWorldUIScript.playerItemList.Count : 0;
                ScreenReader.Say(Loc.Get("itemworld_opened", count));
            }
        }

        // ===== FEATURE: Item World — Orb Selection Phase =====
        [HarmonyPatch(typeof(ItemWorldUIScript), nameof(ItemWorldUIScript.RefreshInterfaceToSelectOrb))]
        public static class Patch_ItemWorldOrbPhase
        {
            public static void Postfix()
            {
                int count = ItemWorldUIScript.playerItemList != null ? ItemWorldUIScript.playerItemList.Count : 0;
                ScreenReader.Say(Loc.Get("itemworld_select_orb", count));
            }
        }

        // ===== FEATURE: Job Change Cost Announcement =====
        [HarmonyPatch(typeof(DialogEventsScript), nameof(DialogEventsScript.OpenJobChangeUI))]
        public static class Patch_JobChangeOpen
        {
            public static void Postfix(bool __result)
            {
                if (!__result) // __result false = UI opened successfully
                {
                    int cost = GameMasterScript.GetJobChangeCost();
                    ScreenReader.Say(Loc.Get("jobchange_opened", cost), false);
                }
            }
        }

        // ===== FEATURE: Job Change Completion =====
        [HarmonyPatch(typeof(HeroPC), nameof(HeroPC.ChangeJobs))]
        public static class Patch_JobChanged
        {
            public static void Postfix(HeroPC __instance, string jobName, bool __result)
            {
                if (!__result) return;
                var jobData = CharacterJobData.GetJobData(jobName);
                if (jobData != null)
                    ScreenReader.Say(Loc.Get("jobchange_complete", jobData.DisplayName));
            }
        }

        // ===== FEATURE: Recipe Details in Journal =====
        [HarmonyPatch(typeof(JournalScript), nameof(JournalScript.GetRecipeInfo))]
        public static class Patch_RecipeInfo
        {
            public static void Postfix(int index)
            {
                if (index >= MetaProgressScript.recipesKnown.Count) return;
                var recipe = CookingScript.FindRecipe(MetaProgressScript.recipesKnown[index]);
                if (recipe == null) return;

                var parts = new List<string>();
                parts.Add(recipe.displayName);

                // Ingredients
                if (!string.IsNullOrEmpty(recipe.ingredientsDescription))
                    parts.Add(Loc.Get("recipe_requires", UIHandler.CleanText(recipe.ingredientsDescription)));

                // Item description
                Item template = Item.GetItemTemplateFromRef(recipe.itemCreated);
                if (template != null)
                {
                    if (!string.IsNullOrEmpty(template.description))
                        parts.Add(UIHandler.CleanText(template.description));

                    // Healing info
                    if (template is Consumable con && con.isHealingFood)
                        parts.Add(UIHandler.CleanText(con.EstimateFoodHealing()));
                }

                // Inventory count
                int qty = GameMasterScript.heroPCActor.myInventory.GetItemQuantity(recipe.itemCreated);
                if (qty > 0)
                    parts.Add(Loc.Get("recipe_in_inventory", qty));

                ScreenReader.Say(string.Join(". ", parts), false);
            }
        }

        // ===== FEATURE: Crafting Screen Accessibility =====
        [HarmonyPatch(typeof(CraftingScreen), nameof(CraftingScreen.TurnOn))]
        public static class Patch_CraftingOpen
        {
            public static void Postfix()
            {
                ScreenReader.Say(Loc.Get("crafting_opened"), false);
            }
        }

        // ===== FEATURE: Corral Monster List =====
        [HarmonyPatch(typeof(MonsterCorralScript), nameof(MonsterCorralScript.OpenCorralInterface))]
        public static class Patch_CorralOpen
        {
            public static void Postfix()
            {
                if (!MonsterCorralScript.corralInterfaceOpen) return;

                int count = 0;
                var corral = MetaProgressScript.localTamedMonstersForThisSlot;
                if (corral != null) count = corral.Count;

                ScreenReader.Say(Loc.Get("corral_opened", count));
            }
        }

        // ===== FEATURE: Corral Food Interface =====
        [HarmonyPatch(typeof(MonsterCorralScript), nameof(MonsterCorralScript.OpenCorralFoodInterface))]
        public static class Patch_CorralFoodOpen
        {
            public static void Postfix(TamedCorralMonster tcm)
            {
                if (tcm == null) return;
                ScreenReader.Say(Loc.Get("corral_food_opened", tcm.monsterObject.displayName));
            }
        }

        // ===== FEATURE: Corral Grooming Interface =====
        [HarmonyPatch(typeof(MonsterCorralScript), "OpenGroomMonsterInterface")]
        public static class Patch_CorralGroomOpen
        {
            public static void Postfix(int monIndex)
            {
                var corral = MetaProgressScript.localTamedMonstersForThisSlot;
                if (corral == null || monIndex < 0 || monIndex >= corral.Count) return;
                var tcm = corral[monIndex];
                ScreenReader.Say(Loc.Get("corral_groom_opened", tcm.monsterObject.displayName));
            }
        }

        // ===== FEATURE: Corral Monster Stats =====
        [HarmonyPatch(typeof(MonsterCorralScript), "OpenMonsterStatsInterface")]
        public static class Patch_CorralStatsOpen
        {
            public static void Postfix(int monIndex)
            {
                var corral = MetaProgressScript.localTamedMonstersForThisSlot;
                if (corral == null || monIndex < 0 || monIndex >= corral.Count) return;
                var tcm = corral[monIndex];
                var mon = tcm.monsterObject;

                var parts = new List<string>();
                parts.Add(mon.displayName);
                parts.Add(Loc.Get("enemy_level", mon.myStats.GetLevel()));

                int hp = UnityEngine.Mathf.RoundToInt(mon.myStats.GetCurStat(StatTypes.HEALTH));
                int maxHp = UnityEngine.Mathf.RoundToInt(mon.myStats.GetMaxStat(StatTypes.HEALTH));
                parts.Add(Loc.Get("stat_status", Loc.Get("stat_health"), hp, maxHp));

                parts.Add(Loc.Get("corral_happiness", tcm.happiness));
                parts.Add(Loc.Get("corral_beauty", tcm.beauty));

                ScreenReader.Say(string.Join(". ", parts));
            }
        }

        // ===== FEATURE: Casino Game Announcements =====
        [HarmonyPatch(typeof(CasinoScript), nameof(CasinoScript.PlayerWonGame))]
        public static class Patch_CasinoWin
        {
            public static void Postfix(int goldWon)
            {
                ScreenReader.Say(Loc.Get("casino_won", goldWon), false);
            }
        }

        [HarmonyPatch(typeof(CasinoScript), nameof(CasinoScript.PlayCurrentGame))]
        public static class Patch_CasinoPlay
        {
            public static void Postfix(int bet)
            {
                ScreenReader.Say(Loc.Get("casino_bet", bet), false);
            }
        }

        // ===== FEATURE: Critical Hit Announcement =====
        // Patched via ApplyDeferredPatches (GetCriticalEffect is private)
        public static class Patch_CriticalHit
        {
            public static void Postfix()
            {
                var data = CombatManagerScript.bufferedCombatData;
                if (data == null) return;

                bool playerIsAttacker = data.attacker != null && data.attacker.GetActorType() == ActorTypes.HERO;
                bool playerIsDefender = data.defender != null && data.defender.GetActorType() == ActorTypes.HERO;

                if (playerIsAttacker)
                    ScreenReader.Say(Loc.Get("combat_crit_dealt"), false);
                else if (playerIsDefender)
                    ScreenReader.Say(Loc.Get("combat_crit_received"), false);
            }
        }
    }
}

