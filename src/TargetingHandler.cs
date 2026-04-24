using UnityEngine;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;

namespace TangledeepAccess
{
    /// <summary>
    /// Handles targeting mode: cycling enemies and announcing virtual cursor moves.
    /// Tab outside targeting: enters targeting on nearest enemy (ranged uses game targeting, melee uses soft cycling).
    /// Tab inside targeting: cycles enemies.
    /// </summary>
    public class TargetingHandler
    {
        private static Vector2 _lastCursorPos = Vector2.zero;
        private static bool _isTargeting = false;

        // Soft targeting for melee: cycles enemies without entering game targeting mode
        private static bool _isSoftTargeting = false;
        private static List<Actor> _softTargetList = new List<Actor>();
        private static int _softTargetIndex = -1;
        private static Vector2 _softTargetPlayerPos;

        public void Update()
        {
            if (!GameMasterScript.gameLoadSequenceCompleted) return;

            bool nowTargeting = UIManagerScript.singletonUIMS.CheckTargeting();
            if (nowTargeting && !_isTargeting)
            {
                OnTargetingStarted();
            }
            else if (!nowTargeting && _isTargeting)
            {
                OnTargetingEnded();
            }
            _isTargeting = nowTargeting;

            // Exit soft targeting if player moved, a menu opened, or Escape pressed
            if (_isSoftTargeting)
            {
                Vector2 currentPos = GameMasterScript.heroPCActor.GetPos();
                if (currentPos != _softTargetPlayerPos
                    || UIManagerScript.AnyInteractableWindowOpen()
                    || _isTargeting
                    || Input.GetKeyDown(KeyCode.Escape))
                {
                    ExitSoftTargeting();
                }
            }

            if (Input.GetKeyDown(KeyCode.Tab))
            {
                bool reverse = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

                if (_isTargeting)
                {
                    // Game targeting mode: cycle enemies
                    CycleTargets(reverse);
                }
                else if (_isSoftTargeting)
                {
                    // Soft targeting mode: cycle enemies
                    CycleSoftTargets(reverse);
                }
                else if (!UIManagerScript.AnyInteractableWindowOpen())
                {
                    // Not targeting and no menu: enter targeting on nearest enemy
                    EnterEnemyTargeting();
                }
            }

            // Enter during soft targeting: attack or walk toward target
            if (_isSoftTargeting && Input.GetKeyDown(KeyCode.Return)
                && !GameMasterScript.gmsSingleton.turnExecuting
                && !GameMasterScript.playerMovingAnimation)
            {
                ConfirmSoftTarget();
            }

            if (!_isTargeting) return;
        }

        private void EnterEnemyTargeting()
        {
            var hero = GameMasterScript.heroPCActor;
            if (hero == null || hero.myEquipment == null) return;

            // Find nearest alive enemy
            var map = MapMasterScript.activeMap;
            Vector2 myPos = hero.GetPos();

            Actor nearestEnemy = null;
            float nearestDist = float.MaxValue;

            foreach (var monster in map.monstersInMap)
            {
                if (monster == null || monster.destroyed) continue;
                if (monster.actorfaction != Faction.ENEMY) continue;
                if (monster.myStats == null || !monster.myStats.IsAlive()) continue;

                float dist = Vector2.Distance(myPos, monster.GetPos());
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearestEnemy = monster;
                }
            }

            if (nearestEnemy == null)
            {
                ScreenReader.Say(Loc.Get("targeting_no_targets"));
                return;
            }

            // Check if ranged weapon is equipped to set up proper targeting
            Weapon weapon = hero.myEquipment.GetWeapon();
            bool isRanged = weapon != null && weapon.isRanged && weapon.range > 1;

            if (isRanged)
            {
                // Ranged: enter full targeting mode so player can fire
                int maxRange = hero.GetMaxAttackRange();
                hero.SetActorData("fireranged", 1);
                GameMasterScript.rangedWeaponAbilityDummy.range = maxRange;
                GameMasterScript.rangedWeaponAbilityDummy.targetRange = maxRange;
                GameMasterScript.gmsSingleton.SetAbilityToTry(GameMasterScript.rangedWeaponAbilityDummy);
                UIManagerScript.singletonUIMS.EnterTargeting(GameMasterScript.rangedWeaponAbilityDummy, Directions.NEUTRAL);
                TDInputHandler.targetClicksMax = 1;
                TDInputHandler.targetClicksRemaining = 1;
            }
            else
            {
                // Melee: enter soft targeting mode for enemy cycling
                EnterSoftTargeting(myPos, map);
                return;
            }

            // Move cursor to nearest enemy
            UIManagerScript.singletonUIMS.SetVirtualCursorPosition(nearestEnemy.GetPos());

            DebugLogger.LogInput("Tab", $"Target: {nearestEnemy.displayName} at {nearestDist:F0} tiles");
        }

        private void EnterSoftTargeting(Vector2 playerPos, Map map)
        {
            _softTargetPlayerPos = playerPos;
            _softTargetList = map.monstersInMap
                .Where(m => m != null && !m.destroyed && m.actorfaction == Faction.ENEMY
                    && m.myStats != null && m.myStats.IsAlive())
                .OrderBy(m => Vector2.Distance(playerPos, m.GetPos()))
                .Cast<Actor>()
                .ToList();

            if (_softTargetList.Count == 0)
            {
                ScreenReader.Say(Loc.Get("targeting_no_targets"));
                return;
            }

            _isSoftTargeting = true;
            _softTargetIndex = 0;
            AnnounceSoftTarget();
        }

        private void CycleSoftTargets(bool reverse)
        {
            // Remove dead/destroyed enemies
            _softTargetList.RemoveAll(a => a == null || a.destroyed
                || !(a is Fighter f) || f.myStats == null || !f.myStats.IsAlive());

            if (_softTargetList.Count == 0)
            {
                ScreenReader.Say(Loc.Get("targeting_no_targets"));
                ExitSoftTargeting();
                return;
            }

            if (reverse)
                _softTargetIndex = (_softTargetIndex <= 0) ? _softTargetList.Count - 1 : _softTargetIndex - 1;
            else
                _softTargetIndex = (_softTargetIndex >= _softTargetList.Count - 1) ? 0 : _softTargetIndex + 1;

            AnnounceSoftTarget();
        }

        private void AnnounceSoftTarget()
        {
            var target = _softTargetList[_softTargetIndex];
            Vector2 myPos = GameMasterScript.heroPCActor.GetPos();

            string info = EnemyInfoHelper.GetBriefInfo(target, myPos);
            info += " (" + (_softTargetIndex + 1) + " / " + _softTargetList.Count + ")";
            ScreenReader.Say(info);
            DebugLogger.LogInput("Tab", $"Soft target: {target.displayName}");
        }

        private void ConfirmSoftTarget()
        {
            if (_softTargetIndex < 0 || _softTargetIndex >= _softTargetList.Count) return;

            var target = _softTargetList[_softTargetIndex];
            if (target == null || target.destroyed) return;

            var hero = GameMasterScript.heroPCActor;
            if (hero == null) return;

            Vector2 myPos = hero.GetPos();
            float dist = Vector2.Distance(myPos, target.GetPos());
            int maxRange = hero.GetMaxAttackRange();

            ExitSoftTargeting();

            if (dist <= maxRange)
            {
                // In range: attack
                if (hero.myStats.CheckParalyzeChance() == 1f)
                {
                    ScreenReader.Say(Loc.Get("autoattack_paralyzed"));
                    return;
                }

                TurnData turnData = new TurnData();
                turnData.actorThatInitiatedTurn = hero;
                turnData.SetTurnType(TurnTypes.ATTACK);
                turnData.SetSingleTargetActor(target);
                turnData.SetSingleTargetPosition(target.GetPos());
                GameMasterScript.gmsSingleton.TryNextTurn(turnData, true);
                TDInputHandler.timeSinceLastActionInput = Time.time;
            }
            else
            {
                // Out of range: walk toward enemy, stop adjacent
                Main.AutoNav?.SetTarget(target.GetPos(), target.displayName, true);
            }
        }

        private void ExitSoftTargeting()
        {
            _isSoftTargeting = false;
            _softTargetList.Clear();
            _softTargetIndex = -1;
        }

        private void OnTargetingStarted()
        {
            _lastCursorPos = UIManagerScript.singletonUIMS.GetVirtualCursorPosition();
            var ability = GameMasterScript.GetAbilityToTry();
            string abilityName = ability?.abilityName ?? "";
            string announcement = Loc.Get("targeting_started");
            if (!string.IsNullOrEmpty(abilityName))
                announcement += " " + UIHandler.CleanText(abilityName);

            // Announce targeting shape and range
            if (ability != null)
            {
                string shape = GetShapeName(ability.boundsShape);
                if (!string.IsNullOrEmpty(shape))
                    announcement += ", " + shape;
                if (ability.range > 0)
                    announcement += ", " + Loc.Get("skill_range", ability.range);
            }

            ScreenReader.Say(announcement);
            AnnounceCurrentCursorTile();
        }

        private static string GetShapeName(TargetShapes shape)
        {
            switch (shape)
            {
                case TargetShapes.POINT: return Loc.Get("shape_point");
                case TargetShapes.BURST: return Loc.Get("shape_burst");
                case TargetShapes.CIRCLE: return Loc.Get("shape_circle");
                case TargetShapes.CROSS: return Loc.Get("shape_cross");
                case TargetShapes.FLEXCROSS: return Loc.Get("shape_cross");
                case TargetShapes.FLEXLINE: return Loc.Get("shape_line");
                case TargetShapes.VLINE: return Loc.Get("shape_line");
                case TargetShapes.HLINE: return Loc.Get("shape_line");
                case TargetShapes.CONE: return Loc.Get("shape_cone");
                case TargetShapes.FLEXCONE: return Loc.Get("shape_cone");
                case TargetShapes.RECT: return Loc.Get("shape_area");
                default: return "";
            }
        }

        private void OnTargetingEnded()
        {
            ScreenReader.Say(Loc.Get("targeting_ended"), false);
        }

        private void CycleTargets(bool reverse)
        {
            var hero = GameMasterScript.heroPCActor;
            var map = MapMasterScript.activeMap;
            Vector2 myPos = hero.GetPos();

            // Find all targetable monsters in range
            var targets = map.actorsInMap
                .Where(a => !a.destroyed && a.GetActorType() == ActorTypes.MONSTER && a.actorfaction == Faction.ENEMY)
                .Where(a => a is Fighter f && f.myStats != null && f.myStats.IsAlive())
                .OrderBy(a => Vector2.Distance(myPos, a.GetPos()))
                .ToList();

            if (targets.Count == 0)
            {
                ScreenReader.Say(Loc.Get("targeting_no_targets"));
                return;
            }

            Vector2 currentCursor = UIManagerScript.singletonUIMS.GetVirtualCursorPosition();
            int currentIndex = targets.FindIndex(t => Vector2.Distance(t.GetPos(), currentCursor) < 0.1f);

            int nextIndex;
            if (reverse)
            {
                nextIndex = (currentIndex <= 0) ? targets.Count - 1 : currentIndex - 1;
            }
            else
            {
                nextIndex = (currentIndex >= targets.Count - 1) ? 0 : currentIndex + 1;
            }

            var nextTarget = targets[nextIndex];
            UIManagerScript.singletonUIMS.SetVirtualCursorPosition(nextTarget.GetPos());
            // Announcement will happen via the SetVirtualCursorPosition patch
        }

        public static void AnnounceCurrentCursorTile()
        {
            Vector2 pos = UIManagerScript.singletonUIMS.GetVirtualCursorPosition();
            var tile = MapMasterScript.activeMap.GetTile(pos);

            // Use WorldHandler's logic for consistency
            string desc = Main.World.GetShortTileDescription(tile);

            float dist = Vector2.Distance(GameMasterScript.heroPCActor.GetPos(), pos);
            string dir = Main.World.GetDirectionName(pos - GameMasterScript.heroPCActor.GetPos());

            // Non-interrupting so it doesn't cut off the "Targeting for X" announcement
            ScreenReader.Say(Loc.Get("targeting_cursor_at", desc, Mathf.RoundToInt(dist), dir), false);
        }

        [HarmonyPatch(typeof(UIManagerScript), nameof(UIManagerScript.SetVirtualCursorPosition))]
        public static class Patch_UIManagerScript_SetVirtualCursorPosition
        {
            public static void Postfix(Vector2 pos)
            {
                if (!UIManagerScript.singletonUIMS.CheckTargeting()) return;
                if (pos == _lastCursorPos) return;
                
                _lastCursorPos = pos;
                AnnounceCurrentCursorTile();
            }
        }
    }
}
