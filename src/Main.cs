using BepInEx;
using UnityEngine;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;

namespace TangledeepAccess
{
    [BepInPlugin("com.niki.tangledeepaccess", "TangledeepAccess", "1.1.0")]
    public class Main : BaseUnityPlugin
    {
        public static Main singleton;
        public static BepInEx.Logging.ManualLogSource Log { get; private set; }
        public static bool DebugMode = false;
        private Harmony _harmony;
        private bool _initialized = false;

        // Handler instances
        public static UIHandler UI { get; private set; }
        public static WorldHandler World { get; private set; }
        public static RadarHandler Radar { get; private set; }
        public static AutoNavigationHandler AutoNav { get; private set; }
        public static StatsHandler Stats { get; private set; }
        public static TargetingHandler Targeting { get; private set; }
        public static InputHandler Input_ { get; private set; }

        void Awake()
        {
            singleton = this;
            Log = Logger;
            try
            {
                ScreenReader.Initialize();
                _harmony = new Harmony("com.niki.tangledeepaccess");
                _harmony.PatchAll();
                Log.LogInfo("Main: Awake complete.");
            }
            catch (Exception ex)
            {
                Log.LogError($"Main: Awake failed: {ex}");
            }
        }

        void Update()
        {
            if (!_initialized)
            {
                // Check if game is actually ready to initialize our logic
                if (GameMasterScript.gmsSingleton != null && UIManagerScript.singletonUIMS != null)
                {
                    InitializeAll();
                    _initialized = true;
                }
                return;
            }

            ProcessHotkeys();

            // Update systems
            UI?.Update();
            World?.Update();
            Radar?.Update();
            AutoNav?.Update();
            Stats?.Update();
            Targeting?.Update();
            Input_?.Update();
            CharacterSheetNav.Update();
        }

        private void InitializeAll()
        {
            try
            {
                Log.LogInfo("Main: Initializing logic...");
                Loc.Initialize();
                UI = new UIHandler();
                World = new WorldHandler();
                Radar = new RadarHandler();
                Radar.Initialize();
                AutoNav = new AutoNavigationHandler();
                Stats = new StatsHandler();
                Targeting = new TargetingHandler();
                Input_ = new InputHandler();
                
                Patches.ApplyDeferredPatches(_harmony);

                ScreenReader.Say(Loc.Get("mod_loaded"));
                Log.LogInfo("Main: Logic initialization complete.");
            }
            catch (Exception ex)
            {
                Log.LogError($"Main: Initialization failed: {ex}");
            }
        }

        private void ProcessHotkeys()
        {
            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

            // Shift+F1: Context-sensitive mod help (plain F1 opens game's built-in help dialog)
            if (shift && Input.GetKeyDown(KeyCode.F1)) ScreenReader.Say(GetContextHelp());

            // F2: Full player status (health, stamina, energy, XP, effects, pet)
            if (Input.GetKeyDown(KeyCode.F2)) Stats?.AnnounceFullStatus();

            // F3: Radar (handled in RadarHandler.Update)

            // F4: Context-sensitive details
            if (Input.GetKeyDown(KeyCode.F4))
            {
                if (UIManagerScript.AnyInteractableWindowOpen())
                {
                    UIHandler.AnnounceFocusedItemDetails();
                }
            }

            // Shift+H: Hotbar reading
            if (shift && Input.GetKeyDown(KeyCode.H)) AnnounceHotbar();

            // Shift+Q: Quests
            if (shift && Input.GetKeyDown(KeyCode.Q)) Stats?.AnnounceQuests();

            // G: Gold (world only)
            if (Input.GetKeyDown(KeyCode.G) && !UIManagerScript.AnyInteractableWindowOpen())
            {
                Stats?.AnnounceGold();
            }

            // O: Walk to radar target (world only)
            if (Input.GetKeyDown(KeyCode.O) && !UIManagerScript.AnyInteractableWindowOpen())
            {
                var radarTarget = Radar?.CurrentTarget;
                if (radarTarget != null)
                {
                    // Arrive adjacent to actors you can't walk onto (enemies, NPCs, destructibles)
                    // Arrive on top of items and stairs
                    bool adjacent = false;
                    if (radarTarget.GameActor != null)
                    {
                        var actorType = radarTarget.GameActor.GetActorType();
                        adjacent = actorType == ActorTypes.MONSTER || actorType == ActorTypes.NPC
                            || actorType == ActorTypes.DESTRUCTIBLE;
                    }
                    AutoNav?.SetTarget(radarTarget.Position, radarTarget.Name, adjacent);
                }
                else
                    ScreenReader.Say(Loc.Get("auto_no_radar_target"));
            }

            // Shift+L: Message log
            if (shift && Input.GetKeyDown(KeyCode.L))
            {
                ReadMessageLog();
            }

            // Shift+A: Auto-attack nearest adjacent enemy (world only)
            if (shift && Input.GetKeyDown(KeyCode.A) && !UIManagerScript.AnyInteractableWindowOpen()
                && !GameMasterScript.gmsSingleton.turnExecuting
                && !GameMasterScript.playerMovingAnimation
                && !GameMasterScript.IsGameInCutsceneOrDialog())
            {
                AutoAttackNearest();
            }

            // Shift+M: Floor overview (world only)
            if (shift && Input.GetKeyDown(KeyCode.M) && !UIManagerScript.AnyInteractableWindowOpen())
            {
                AnnounceFloorOverview();
            }

            // Shift+N: Adjacent tiles scan (world only)
            if (shift && Input.GetKeyDown(KeyCode.N) && !UIManagerScript.AnyInteractableWindowOpen())
            {
                AnnounceAdjacentTiles();
            }

            // F12: Debug toggle
            if (Input.GetKeyDown(KeyCode.F12))
            {
                DebugMode = !DebugMode;
                ScreenReader.Say(Loc.Get(DebugMode ? "debug_enabled" : "debug_disabled"));
            }

            // L: Cycle language (in options menu only, when not adjusting a slider)
            if (Input.GetKeyDown(KeyCode.L) && !shift
                && UIManagerScript.GetWindowState(UITabs.OPTIONS)
                && !UIManagerScript.movingSliderViaKeyboard)
            {
                CycleLanguage();
            }
        }

        private static readonly string[] _langNames = { "English", "Deutsch", "Japanese", "Español", "Chinese" };

        private void CycleLanguage()
        {
            EGameLanguage current = StringManager.gameLanguage;
            int next = ((int)current + 1) % (int)EGameLanguage.COUNT;
            EGameLanguage newLang = (EGameLanguage)next;

            TDPlayerPrefs.SetString(GlobalProgressKeys.LANGUAGE, newLang.ToString());
            StringManager.SetGameLanguage(newLang);
            PlayerOptions.WriteOptionsToFile();
            TDPlayerPrefs.Save();

            string langName = (int)newLang < _langNames.Length ? _langNames[(int)newLang] : newLang.ToString();
            ScreenReader.Say(Loc.Get("language_changed", langName));
        }

        private void AnnounceHotbar()
        {
            var hotbar = UIManagerScript.hotbarAbilities;
            if (hotbar == null) return;

            int activeBar = UIManagerScript.GetIndexOfActiveHotbar();
            int start = activeBar * 8;
            int end = System.Math.Min(start + 8, hotbar.Length);

            var parts = new System.Collections.Generic.List<string>();
            for (int i = start; i < end; i++)
            {
                var slot = hotbar[i];
                if (slot == null || slot.actionType == HotbarBindableActions.NOTHING) continue;

                int keyNum = i - start + 1;
                string name = "";
                if (slot.actionType == HotbarBindableActions.ABILITY && slot.ability != null)
                {
                    name = UIHandler.CleanText(slot.ability.abilityName);
                }
                else if (slot.actionType == HotbarBindableActions.CONSUMABLE && slot.consume != null)
                {
                    name = Loc.Get("hotbar_consumable", UIHandler.CleanText(slot.consume.displayName), slot.consume.Quantity);
                }

                if (!string.IsNullOrEmpty(name))
                {
                    parts.Add(Loc.Get("hotbar_slot", keyNum, name));
                }
            }

            if (parts.Count == 0) ScreenReader.Say(Loc.Get("hotbar_empty_all"));
            else ScreenReader.Say(string.Join(". ", parts));
        }

        private void ReadMessageLog()
        {
            var messages = ScreenReader.GetRecentMessages(10);
            if (messages.Count == 0)
            {
                ScreenReader.Say(Loc.Get("log_empty"));
                return;
            }
            // Read oldest to newest (messages list is newest-first)
            messages.Reverse();
            ScreenReader.Say(Loc.Get("log_header") + " " + string.Join(". ", messages));
        }

        private void AutoAttackNearest()
        {
            var hero = GameMasterScript.heroPCActor;
            if (hero == null || hero.myStats == null) return;

            Vector2 myPos = hero.GetPos();
            var map = MapMasterScript.activeMap;

            // Find nearest adjacent enemy (within melee range, max attack range)
            int maxRange = hero.GetMaxAttackRange();
            Actor bestTarget = null;
            float bestDist = float.MaxValue;

            foreach (var monster in map.monstersInMap)
            {
                if (monster == null || monster.destroyed) continue;
                if (monster.actorfaction != Faction.ENEMY) continue;
                if (monster.myStats == null || !monster.myStats.IsAlive()) continue;

                float dist = Vector2.Distance(myPos, monster.GetPos());
                if (dist <= maxRange && dist < bestDist)
                {
                    bestDist = dist;
                    bestTarget = monster;
                }
            }

            if (bestTarget == null)
            {
                ScreenReader.Say(Loc.Get("autoattack_no_target"));
                return;
            }

            // Execute attack
            TurnData turnData = new TurnData();
            turnData.actorThatInitiatedTurn = hero;
            turnData.SetTurnType(TurnTypes.ATTACK);
            turnData.SetSingleTargetActor(bestTarget);
            turnData.SetSingleTargetPosition(bestTarget.GetPos());

            // Check for paralysis
            if (hero.myStats.CheckParalyzeChance() == 1f)
            {
                ScreenReader.Say(Loc.Get("autoattack_paralyzed"));
                return;
            }

            GameMasterScript.gmsSingleton.TryNextTurn(turnData, true);
            TDInputHandler.timeSinceLastActionInput = UnityEngine.Time.time;

            string dir = World.GetDirectionName(bestTarget.GetPos() - myPos);
            ScreenReader.Say(Loc.Get("autoattack_swing", bestTarget.displayName, dir), false);
        }

        private void AnnounceFloorOverview()
        {
            var map = MapMasterScript.activeMap;
            var hero = GameMasterScript.heroPCActor;
            if (map == null || hero == null) return;

            var parts = new List<string>();

            // Floor name and number
            parts.Add(Loc.Get("cs_floor", map.GetName(), map.floor));

            // Count enemies
            int enemies = map.monstersInMap.Count(m => m != null && !m.destroyed
                && m.actorfaction == Faction.ENEMY && m.myStats != null && m.myStats.IsAlive());
            parts.Add(Loc.Get("floor_enemies", enemies));

            // Count NPCs
            int npcs = map.actorsInMap.Count(a => a != null && !a.destroyed && a.GetActorType() == ActorTypes.NPC);
            if (npcs > 0)
                parts.Add(Loc.Get("floor_npcs", npcs));

            // Count items on ground
            int items = map.actorsInMap.Count(a => a != null && !a.destroyed && a.GetActorType() == ActorTypes.ITEM);
            if (items > 0)
                parts.Add(Loc.Get("floor_items", items));

            // Count stairs
            var stairs = map.actorsInMap.Where(a => a != null && !a.destroyed && a.GetActorType() == ActorTypes.STAIRS).ToList();
            foreach (var stair in stairs)
            {
                string stairName = !string.IsNullOrEmpty(stair.displayName) ? stair.displayName : Loc.Get("radar_cat_stairs");
                float dist = Vector2.Distance(hero.GetPos(), stair.GetPos());
                string dir = World.GetDirectionName(stair.GetPos() - hero.GetPos());
                parts.Add(stairName + ", " + (int)dist + " tiles " + dir);
            }

            // Exploration percentage
            int explored = 0, total = 0;
            for (int x = 0; x < map.columns; x++)
            {
                for (int y = 0; y < map.rows; y++)
                {
                    var tile = map.mapArray[x, y];
                    if (tile != null && tile.tileType != TileTypes.WALL && tile.tileType != TileTypes.NOTHING)
                    {
                        total++;
                        if (map.exploredTiles[x, y]) explored++;
                    }
                }
            }
            if (total > 0)
            {
                int pct = (explored * 100) / total;
                parts.Add(Loc.Get("floor_explored", pct));
            }

            ScreenReader.Say(string.Join(". ", parts));
        }

        private void AnnounceAdjacentTiles()
        {
            var hero = GameMasterScript.heroPCActor;
            var map = MapMasterScript.activeMap;
            if (hero == null || map == null) return;

            Vector2 myPos = hero.GetPos();
            var parts = new List<string>();

            // Check all 8 directions
            Vector2[] offsets = {
                new Vector2(0, 1), new Vector2(0, -1),
                new Vector2(-1, 0), new Vector2(1, 0),
                new Vector2(-1, 1), new Vector2(1, 1),
                new Vector2(-1, -1), new Vector2(1, -1)
            };

            foreach (var offset in offsets)
            {
                Vector2 checkPos = myPos + offset;
                string dirName = World.GetDirectionName(offset);

                if (!MapMasterScript.InBounds(checkPos))
                {
                    parts.Add(dirName + ": " + Loc.Get("world_wall"));
                    continue;
                }

                var tile = map.GetTile(checkPos);
                if (tile == null || tile.tileType == TileTypes.WALL)
                {
                    parts.Add(dirName + ": " + Loc.Get("world_wall"));
                    continue;
                }

                string desc = World.GetShortTileDescription(tile);
                if (desc != Loc.Get("tile_ground"))
                    parts.Add(dirName + ": " + desc);
            }

            if (parts.Count == 0)
                parts.Add(Loc.Get("adjacent_clear"));

            ScreenReader.Say(string.Join(". ", parts));
        }

        private string GetContextHelp()
        {
            // Character creation (any stage that isn't TITLESCREEN or COUNT means we're in creation)
            var stage = TitleScreenScript.CreateStage;
            if (stage != CreationStages.COUNT && stage != CreationStages.TITLESCREEN)
                return Loc.Get("help_creation");

            // Targeting mode
            if (UIManagerScript.singletonUIMS != null && UIManagerScript.singletonUIMS.CheckTargeting())
                return Loc.Get("help_targeting");

            // Options menu
            if (UIManagerScript.GetWindowState(UITabs.OPTIONS))
                return Loc.Get("help_settings");

            // Dialog open
            if (UIManagerScript.dialogBoxOpen)
                return Loc.Get("help_dialog");

            // Any interactable window (inventory, equipment, skills, character, journal, etc.)
            if (UIManagerScript.AnyInteractableWindowOpen())
                return Loc.Get("help_menu");

            // Default: world/exploration
            return Loc.Get("help_world");
        }

        void OnDestroy()
        {
            _harmony?.UnpatchSelf();
            ScreenReader.Shutdown();
        }
    }
}
