using UnityEngine;
using System.Collections.Generic;

namespace TangledeepAccess
{
    /// <summary>
    /// Walks the hero toward a radar target, pathfinding around walls and NPCs.
    /// Items are auto-picked up by the game, combat stops navigation.
    /// </summary>
    public class AutoNavigationHandler
    {
        private Vector2? _targetPos = null;
        private string _targetName = "";
        private bool _isActive = false;
        private bool _hasMoved = false;
        private bool _arriveAdjacent = false;
        private List<Vector2> _currentPath = new List<Vector2>();
        private HashSet<Vector2> _blockedTiles = new HashSet<Vector2>();
        private float _lastMoveTime = 0f;
        private float _lastHealthPct = 1f;
        private int _graceFrames = 0;
        private const float MOVE_COOLDOWN = 0.15f;
        private const int GRACE_FRAME_COUNT = 10;

        public void SetTarget(Vector2 target, string name, bool arriveAdjacent = false)
        {
            _targetPos = target;
            _targetName = name;
            _isActive = true;
            _hasMoved = false;
            _arriveAdjacent = arriveAdjacent;
            _currentPath.Clear();
            _blockedTiles.Clear();
            _graceFrames = GRACE_FRAME_COUNT;
            _lastHealthPct = GameMasterScript.heroPCActor?.myStats?.GetCurStatAsPercentOfMax(StatTypes.HEALTH) ?? 1f;
            ScreenReader.Say(Loc.Get("auto_walking_to", name));
        }

        public void Stop()
        {
            if (_isActive)
            {
                _targetPos = null;
                _isActive = false;
                _hasMoved = false;
                _currentPath.Clear();
                _blockedTiles.Clear();
                ScreenReader.Say(Loc.Get("auto_stopped"));
            }
        }

        private void Finish()
        {
            string name = _targetName;
            _targetPos = null;
            _isActive = false;
            _hasMoved = false;
            _currentPath.Clear();
            _blockedTiles.Clear();
            // Turn off radar silently after arrival
            Main.Radar?.DeactivateRadar(false);
            ScreenReader.Say(Loc.Get("auto_arrived", name));
        }

        public void Update()
        {
            if (!GameMasterScript.gameLoadSequenceCompleted) return;
            if (!_isActive) return;

            // Grace period after starting
            if (_graceFrames > 0)
            {
                _graceFrames--;
                return;
            }

            // Stop on keyboard input (not mouse)
            if (Input.anyKeyDown && !Input.GetMouseButtonDown(0) && !Input.GetMouseButtonDown(1) && !Input.GetMouseButtonDown(2))
            {
                Stop();
                return;
            }

            var hero = GameMasterScript.heroPCActor;
            if (hero == null || _targetPos == null) return;

            // Stop if hero took damage
            if (hero.myStats != null)
            {
                float curHealthPct = hero.myStats.GetCurStatAsPercentOfMax(StatTypes.HEALTH);
                if (curHealthPct < _lastHealthPct - 0.01f)
                {
                    _lastHealthPct = curHealthPct;
                    _targetPos = null;
                    _isActive = false;
                    _hasMoved = false;
                    _currentPath.Clear();
                    _blockedTiles.Clear();
                    ScreenReader.Say(Loc.Get("auto_stopped_combat"));
                    return;
                }
                _lastHealthPct = curHealthPct;
            }

            // If a dialog opened (NPC bump), close it and block that tile
            if (UIManagerScript.dialogBoxOpen && !GameMasterScript.playerDied)
            {
                // Block the tile we tried to walk into so pathfinder goes around
                if (_currentPath.Count > 0)
                {
                    _blockedTiles.Add(_currentPath[0]);
                    _currentPath.Clear(); // Force recalculation
                }
                try
                {
                    UIManagerScript.ToggleDialogBox(DialogType.EXIT, false, false);
                }
                catch { }
                return;
            }

            // Check arrival (only after we've moved at least once)
            float arrivalDist = _arriveAdjacent ? 1.5f : 0.5f;
            if (_hasMoved && Vector2.Distance(hero.GetPos(), _targetPos.Value) < arrivalDist)
            {
                Finish();
                return;
            }

            // Wait for turn system
            if (GameMasterScript.gmsSingleton.turnExecuting || GameMasterScript.playerMovingAnimation) return;
            if (Time.time - _lastMoveTime < MOVE_COOLDOWN) return;

            Vector2 currentPos = hero.GetPos();

            // Recalculate path if needed
            if (_currentPath.Count == 0 || Vector2.Distance(currentPos, _currentPath[0]) > 0.1f)
            {
                _currentPath = FindPath(currentPos, _targetPos.Value);
                if (_currentPath.Count == 0)
                {
                    _targetPos = null;
                    _isActive = false;
                    _hasMoved = false;
                    _blockedTiles.Clear();
                    ScreenReader.Say(Loc.Get("auto_no_path"));
                    return;
                }
            }

            // Skip current position
            if (_currentPath.Count > 0 && Vector2.Distance(currentPos, _currentPath[0]) < 0.1f)
            {
                _currentPath.RemoveAt(0);
            }

            // Take next step
            if (_currentPath.Count > 0)
            {
                Vector2 nextStep = _currentPath[0];
                TurnData turn = new TurnData();
                turn.actorThatInitiatedTurn = hero;
                turn.SetTurnType(TurnTypes.MOVE);
                turn.newPosition = nextStep;
                TDInputHandler.timeSinceLastActionInput = Time.time;
                GameMasterScript.gmsSingleton.TryNextTurn(turn, true);
                _hasMoved = true;
                _lastMoveTime = Time.time;
            }
            else
            {
                Finish();
            }
        }

        #region Pathfinding (A*)

        private List<Vector2> FindPath(Vector2 start, Vector2 goal)
        {
            var map = MapMasterScript.activeMap;
            var hero = GameMasterScript.heroPCActor;

            var openSet = new PriorityQueue<Vector2, float>();
            var cameFrom = new Dictionary<Vector2, Vector2>();
            var gScore = new Dictionary<Vector2, float>();

            openSet.Enqueue(start, 0);
            gScore[start] = 0;

            float goalDist = _arriveAdjacent ? 1.5f : 0.5f;
            int iterations = 0;
            const int MAX_ITERATIONS = 2000;

            while (openSet.Count > 0 && iterations < MAX_ITERATIONS)
            {
                iterations++;
                Vector2 current = openSet.Dequeue();

                if (Vector2.Distance(current, goal) < goalDist)
                {
                    return ReconstructPath(cameFrom, current);
                }

                foreach (Vector2 neighbor in GetNeighbors(current))
                {
                    var tile = map.GetTile(neighbor);
                    if (tile == null) continue;

                    // Avoid walls and solid terrain
                    if (tile.tileType == TileTypes.WALL || tile.tileType == TileTypes.NOTHING
                        || tile.tileType == TileTypes.MAPEDGE || tile.CheckTag(LocationTags.SOLIDTERRAIN))
                        continue;

                    // Avoid tiles we already bumped into (NPCs)
                    if (_blockedTiles.Contains(neighbor)) continue;

                    // Avoid tiles with collidable actors (NPCs, friendly monsters) — but not the goal tile
                    if (Vector2.Distance(neighbor, goal) > 0.5f && tile.IsCollidableActorInTile(hero))
                        continue;

                    // Penalize hazard tiles so pathfinder prefers safe routes
                    float tileCost = 1f;
                    if (tile.CheckTag(LocationTags.LAVA) || tile.CheckTag(LocationTags.ELECTRIC))
                        tileCost = 20f;
                    else if (tile.CheckTag(LocationTags.MUD))
                        tileCost = 5f;
                    else if (tile.CheckTag(LocationTags.WATER) || tile.CheckTag(LocationTags.ISLANDSWATER))
                        tileCost = 3f;

                    // Check for traps on the tile
                    foreach (var actor in tile.GetAllActors())
                    {
                        if (actor is Destructible dt && dt.destroyOnStep && !dt.destroyed)
                        {
                            tileCost = 15f;
                            break;
                        }
                    }

                    float tentativeG = gScore[current] + tileCost;
                    if (!gScore.ContainsKey(neighbor) || tentativeG < gScore[neighbor])
                    {
                        cameFrom[neighbor] = current;
                        gScore[neighbor] = tentativeG;
                        openSet.Enqueue(neighbor, tentativeG + Vector2.Distance(neighbor, goal));
                    }
                }
            }

            return new List<Vector2>();
        }

        private IEnumerable<Vector2> GetNeighbors(Vector2 pos)
        {
            yield return new Vector2(pos.x + 1, pos.y);
            yield return new Vector2(pos.x - 1, pos.y);
            yield return new Vector2(pos.x, pos.y + 1);
            yield return new Vector2(pos.x, pos.y - 1);
            yield return new Vector2(pos.x + 1, pos.y + 1);
            yield return new Vector2(pos.x - 1, pos.y + 1);
            yield return new Vector2(pos.x + 1, pos.y - 1);
            yield return new Vector2(pos.x - 1, pos.y - 1);
        }

        private List<Vector2> ReconstructPath(Dictionary<Vector2, Vector2> cameFrom, Vector2 current)
        {
            var path = new List<Vector2> { current };
            while (cameFrom.ContainsKey(current))
            {
                current = cameFrom[current];
                path.Add(current);
            }
            path.Reverse();
            return path;
        }

        #endregion
    }

    /// <summary>
    /// Simple Priority Queue for A*.
    /// </summary>
    public class PriorityQueue<TElement, TPriority> where TPriority : System.IComparable<TPriority>
    {
        private List<(TElement Element, TPriority Priority)> _elements = new List<(TElement, TPriority)>();

        public int Count => _elements.Count;

        public void Enqueue(TElement element, TPriority priority)
        {
            _elements.Add((element, priority));
        }

        public TElement Dequeue()
        {
            int bestIndex = 0;
            for (int i = 1; i < _elements.Count; i++)
            {
                if (_elements[i].Priority.CompareTo(_elements[bestIndex].Priority) < 0)
                    bestIndex = i;
            }
            TElement element = _elements[bestIndex].Element;
            _elements.RemoveAt(bestIndex);
            return element;
        }
    }
}
