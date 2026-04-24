using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace TangledeepAccess
{
    /// <summary>
    /// Handler for world awareness: movement announcements and tile detection.
    /// </summary>
    public class WorldHandler
    {
        private static Vector2 _lastPos = Vector2.zero;
        private static Directions _lastInputDirection = Directions.NEUTRAL;

        public void Update()
        {
            if (!GameMasterScript.gameLoadSequenceCompleted) return;

            // Wall bump detection
            CheckWallBump();

            // Look Around (Key: L) — only when no modifier keys are held
            bool anyModifier = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)
                || Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)
                || Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
            if (Input.GetKeyDown(KeyCode.L) && !anyModifier && !UIManagerScript.AnyInteractableWindowOpen())
            {
                LookAround();
            }

            var hero = GameMasterScript.heroPCActor;
            if (hero != null)
            {
                Vector2 currentPos = hero.GetPos();
                if (currentPos != _lastPos)
                {
                    OnPositionChanged(currentPos);
                    _lastPos = currentPos;
                }
            }
        }

        private void CheckWallBump()
        {
            if (GameMasterScript.gmsSingleton.turnExecuting || GameMasterScript.playerMovingAnimation) return;
            if (UIManagerScript.AnyInteractableWindowOpen()) return;

            Directions dir = TDInputHandler.GetDirectionalInput();
            if (dir != Directions.NEUTRAL && dir != Directions.COUNT)
            {
                if (dir != _lastInputDirection)
                {
                    Vector2 targetPos = GameMasterScript.heroPCActor.GetPos() + MapMasterScript.xDirections[(int)dir];
                    var tile = MapMasterScript.activeMap.GetTile(targetPos);
                    
                    // CRITICAL FIX: Only say 'Wall' if the tile is actually a wall or solid obstacle.
                    // We also check if hero is NOT moving despite input.
                    if (tile != null && tile.tileType == TileTypes.WALL)
                    {
                        UIManagerScript.PlayCursorSound("Error");
                        ScreenReader.Say(Loc.Get("world_wall"));
                    }
                    _lastInputDirection = dir;
                }
            }
            else
            {
                _lastInputDirection = Directions.NEUTRAL;
            }
        }

        private void OnPositionChanged(Vector2 newPos)
        {
            var activeMap = MapMasterScript.activeMap;
            if (activeMap == null) return;

            var tile = activeMap.GetTile(newPos);

            // Check for items or stairs on the current tile - announce these prominently
            var items = activeMap.GetItemsInTile(newPos);
            var actor = activeMap.GetTargetableAtLocation(newPos);
            bool hasStairs = actor != null && actor.GetActorType() == ActorTypes.STAIRS;

            // Only announce tile type if it's something notable (not plain ground)
            string tileType = GetTileTypeName(tile);
            bool isNotableGround = tileType != Loc.Get("tile_ground");

            // Build announcement: notable tile first, then items/stairs
            var parts = new List<string>();
            if (isNotableGround)
                parts.Add(tileType);
            if (items != null && items.Count > 0)
                parts.Add(Loc.Get("world_items_here") + " " + string.Join(", ", items.Select(i => i.displayName)));
            if (hasStairs)
                parts.Add(Loc.Get("world_stairs_here"));

            if (parts.Count > 0)
            {
                // Never interrupt - let loot pickups, combat messages, etc. finish
                ScreenReader.Say(string.Join(". ", parts), false);
            }
        }

        private void LookAround()
        {
            var hero = GameMasterScript.heroPCActor;
            if (hero == null) return;

            Vector2 myPos = hero.GetPos();
            var activeMap = MapMasterScript.activeMap;
            
            ScreenReader.Say(Loc.Get("world_current_tile") + ": " + GetTileDescription(activeMap.GetTile(myPos)));

            var neighborParts = new List<string>();
            for (int i = 0; i < 8; i++)
            {
                Vector2 targetPos = myPos + MapMasterScript.xDirections[i];
                var tile = activeMap.GetTile(targetPos);
                string tileDesc = GetShortTileDescription(tile);
                if (tileDesc != Loc.Get("tile_ground"))
                {
                    neighborParts.Add(GetDirectionName((Directions)i) + ": " + tileDesc);
                }
            }

            if (neighborParts.Count > 0) ScreenReader.Say(string.Join(", ", neighborParts));
            else ScreenReader.Say(Loc.Get("world_around_empty"));
        }

        public string GetShortTileDescription(MapTileData tile)
        {
            if (tile == null) return Loc.Get("world_unknown");
            if (tile.tileType == TileTypes.WALL) return Loc.Get("world_wall");

            var actor = MapMasterScript.activeMap.GetTargetableAtLocation(tile.pos);
            if (actor != null)
            {
                if (actor.GetActorType() == ActorTypes.MONSTER) return actor.displayName;
                if (actor.GetActorType() == ActorTypes.STAIRS) return Loc.Get("radar_cat_stairs");
                if (actor.GetActorType() == ActorTypes.NPC) return actor.displayName;
            }

            var items = MapMasterScript.activeMap.GetItemsInTile(tile.pos);
            if (items != null && items.Count > 0) return items[0].displayName;

            return GetTileTypeName(tile);
        }

        private string GetTileDescription(MapTileData tile)
        {
            if (tile == null) return Loc.Get("world_unknown");
            string desc = GetTileTypeName(tile);
            if (tile.tileType == TileTypes.WALL) desc = Loc.Get("world_wall");
            return desc;
        }

        private string GetTileTypeName(MapTileData tile)
        {
            if (tile.tileType == TileTypes.WALL) return Loc.Get("world_wall");
            if (tile.CheckTag(LocationTags.WATER)) return Loc.Get("tile_water");
            if (tile.CheckTag(LocationTags.GRASS)) return Loc.Get("tile_grass");
            if (tile.CheckTag(LocationTags.LAVA)) return Loc.Get("tile_lava");
            if (tile.CheckTag(LocationTags.MUD)) return Loc.Get("tile_mud");
            if (tile.CheckTag(LocationTags.ELECTRIC)) return Loc.Get("tile_electric");
            return Loc.Get("tile_ground");
        }

        public string GetDirectionName(Vector2 offset)
        {
            if (offset.magnitude < 0.5f) return Loc.Get("dir_here");
            float angle = CombatManagerScript.GetAngleBetweenPoints(Vector2.zero, offset);
            return GetDirectionName(MapMasterScript.GetDirectionFromAngle(angle));
        }

        public string GetDirectionName(Directions dir)
        {
            switch (dir)
            {
                case Directions.NORTH: return Loc.Get("dir_north");
                case Directions.NORTHEAST: return Loc.Get("dir_northeast");
                case Directions.EAST: return Loc.Get("dir_east");
                case Directions.SOUTHEAST: return Loc.Get("dir_southeast");
                case Directions.SOUTH: return Loc.Get("dir_south");
                case Directions.SOUTHWEST: return Loc.Get("dir_southwest");
                case Directions.WEST: return Loc.Get("dir_west");
                case Directions.NORTHWEST: return Loc.Get("dir_northwest");
                default: return "";
            }
        }
    }
}
