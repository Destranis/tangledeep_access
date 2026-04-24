using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using System;

namespace TangledeepAccess
{
    /// <summary>
    /// Radar system with category-based navigation.
    /// F3: Scan surroundings.
    /// Shift+Left / Shift+Right: Cycle categories.
    /// Shift+Up / Shift+Down: Cycle items within current category.
    /// F4: Repeat current target details.
    /// Escape: Close radar.
    /// </summary>
    public class RadarHandler
    {
        public class RadarEntry
        {
            public string Name;
            public Vector2 Position;
            public Actor GameActor;
        }

        private enum RadarCategory
        {
            Enemies,
            NPCs,
            Items,
            Stairs,
            Destructibles,
            COUNT
        }

        private const float SCAN_RANGE = 999f;
        private Dictionary<RadarCategory, List<RadarEntry>> _categories = new Dictionary<RadarCategory, List<RadarEntry>>();
        private RadarCategory _currentCategory;
        private int _currentItemIndex = -1;
        private bool _radarActive = false;
        private RadarEntry _currentTarget = null;

        /// <summary>
        /// Returns the currently tracked radar target, or null if none.
        /// </summary>
        public RadarEntry CurrentTarget => _radarActive ? _currentTarget : null;

        private float _lastBeaconTime = 0f;
        private float _beaconInterval = 1.0f;
        private AudioSource _beaconSource;
        private AudioClip _toneClip;

        public void Initialize()
        {
            // Create a dedicated AudioSource for panned beacon sounds
            var go = new GameObject("RadarBeacon");
            GameObject.DontDestroyOnLoad(go);
            _beaconSource = go.AddComponent<AudioSource>();
            _beaconSource.spatialBlend = 0f; // 2D sound so panStereo works
            _beaconSource.volume = 0.6f;
            _beaconSource.playOnAwake = false;

            // Generate a sine wave tone clip (A4 = 440Hz, 0.12 seconds)
            _toneClip = GenerateTone(440f, 0.12f);
        }

        /// <summary>
        /// Generates a short sine wave AudioClip at the given frequency.
        /// Pitch shifting this clip produces clearly audible note changes.
        /// </summary>
        private AudioClip GenerateTone(float frequency, float duration)
        {
            int sampleRate = 44100;
            int sampleCount = (int)(sampleRate * duration);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                // Sine wave
                samples[i] = Mathf.Sin(2f * Mathf.PI * frequency * t);
                // Fade in/out to avoid clicks (10ms fade)
                int fadeSamples = (int)(sampleRate * 0.01f);
                if (i < fadeSamples)
                    samples[i] *= (float)i / fadeSamples;
                else if (i > sampleCount - fadeSamples)
                    samples[i] *= (float)(sampleCount - i) / fadeSamples;
            }

            var clip = AudioClip.Create("RadarTone", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        public void Update()
        {
            if (!GameMasterScript.gameLoadSequenceCompleted) return;

            if (Input.GetKeyDown(KeyCode.F3))
            {
                if (_radarActive)
                    DeactivateRadar();
                else
                    PerformScan();
                return;
            }

            if (!_radarActive) return;

            if (Input.GetKeyDown(KeyCode.F4)) RepeatCurrentTarget();

            // Shift+Left / Shift+Right: Cycle categories
            // Shift+Up / Shift+Down: Cycle items within category
            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            if (shift && Input.GetKeyDown(KeyCode.LeftArrow)) CycleCategory(-1);
            else if (shift && Input.GetKeyDown(KeyCode.RightArrow)) CycleCategory(1);
            else if (shift && Input.GetKeyDown(KeyCode.UpArrow)) CycleItem(-1);
            else if (shift && Input.GetKeyDown(KeyCode.DownArrow)) CycleItem(1);

            // Escape: Close radar only if no menu is open
            if (Input.GetKeyDown(KeyCode.Escape) && !UIManagerScript.AnyInteractableWindowOpen())
            {
                DeactivateRadar();
                return;
            }

            // Audio Beacon
            if (_currentTarget != null)
            {
                // Check if current target is still valid
                if (_currentTarget.GameActor != null && _currentTarget.GameActor.destroyed)
                {
                    _currentTarget = null;
                    return;
                }

                float dist = Vector2.Distance(GameMasterScript.heroPCActor.GetPos(), _currentTarget.Position);
                if (dist < 0.5f) return;

                _beaconInterval = Mathf.Lerp(0.3f, 1.8f, Mathf.Clamp01(dist / 30f));
                if (Time.time - _lastBeaconTime >= _beaconInterval)
                {
                    PlayBeaconForCurrentTarget();
                }
            }
        }

        private void PerformScan()
        {
            var hero = GameMasterScript.heroPCActor;
            if (hero == null || MapMasterScript.activeMap == null) return;

            _categories.Clear();
            for (int i = 0; i < (int)RadarCategory.COUNT; i++)
                _categories[(RadarCategory)i] = new List<RadarEntry>();

            Vector2 heroPos = hero.GetPos();

            foreach (var actor in MapMasterScript.activeMap.actorsInMap)
            {
                if (actor == hero) continue;
                if (actor.destroyed) continue;
                float dist = Vector2.Distance(heroPos, actor.GetPos());
                if (dist > SCAN_RANGE) continue;

                RadarCategory cat;
                string name = actor.displayName;
                var type = actor.GetActorType();

                if (type == ActorTypes.MONSTER)
                {
                    if (actor.actorfaction == Faction.ENEMY)
                        cat = RadarCategory.Enemies;
                    else
                        cat = RadarCategory.NPCs;
                }
                else if (type == ActorTypes.NPC)
                {
                    cat = RadarCategory.NPCs;
                }
                else if (type == ActorTypes.STAIRS)
                {
                    cat = RadarCategory.Stairs;
                    var stairs = actor as Stairs;
                    if (stairs != null)
                    {
                        string dir = stairs.stairsUp ? Loc.Get("stairs_up") : Loc.Get("stairs_down");
                        string dest = stairs.NewLocation != null ? stairs.NewLocation.GetName() : "";
                        name = !string.IsNullOrEmpty(dest) ? dir + ", " + dest : dir;
                    }
                    else
                    {
                        name = Loc.Get("radar_cat_stairs");
                    }
                }
                else if (type == ActorTypes.ITEM)
                {
                    cat = RadarCategory.Items;
                }
                else if (type == ActorTypes.DESTRUCTIBLE)
                {
                    var dest = actor as Destructible;
                    if (dest == null) continue;
                    // Skip pure terrain tiles (water, lava, mud, etc.)
                    if (IsTerrainDestructible(dest)) continue;

                    cat = RadarCategory.Destructibles;
                    // Label hazardous destructibles
                    if (IsHazardousDestructible(actor))
                        name = Loc.Get("hazard_warning", name);
                }
                else continue;

                _categories[cat].Add(new RadarEntry { Name = name, Position = actor.GetPos(), GameActor = actor });
            }

            // Terrain category removed - only track interactable/harmful objects

            // Sort each category by distance
            foreach (var key in _categories.Keys.ToList())
                _categories[key] = _categories[key].OrderBy(e => Vector2.Distance(heroPos, e.Position)).ToList();

            int totalCount = _categories.Values.Sum(l => l.Count);
            if (totalCount == 0)
            {
                ScreenReader.Say(Loc.Get("radar_nothing"));
                _radarActive = false;
                return;
            }

            _radarActive = true;

            // Build scan summary
            var summary = new List<string>();
            for (int i = 0; i < (int)RadarCategory.COUNT; i++)
            {
                var cat = (RadarCategory)i;
                if (_categories[cat].Count > 0)
                    summary.Add(Loc.Get("radar_cat_count", GetCategoryName(cat), _categories[cat].Count));
            }
            ScreenReader.Say(string.Join(", ", summary));

            // Select first non-empty category
            _currentCategory = FindNextNonEmptyCategory(0, 1);
            _currentItemIndex = 0;
            _currentTarget = _categories[_currentCategory][0];
            AnnounceCategoryAndItem();
            PlayBeaconForCurrentTarget();
        }

        private void CycleCategory(int direction)
        {
            foreach (var cat in _categories.Keys.ToList())
                PruneStaleEntries(_categories[cat]);

            int start = ((int)_currentCategory + direction + (int)RadarCategory.COUNT) % (int)RadarCategory.COUNT;
            var next = FindNextNonEmptyCategory(start, direction);
            if (next == _currentCategory && _categories[_currentCategory].Count == 0) return;

            _currentCategory = next;
            _currentItemIndex = 0;
            _currentTarget = _categories[_currentCategory][0];
            AnnounceCategoryAndItem();
            PlayBeaconForCurrentTarget();
        }

        private void CycleItem(int direction)
        {
            var list = _categories[_currentCategory];
            PruneStaleEntries(list);
            if (list.Count == 0)
            {
                _currentTarget = null;
                ScreenReader.Say(Loc.Get("radar_cat_empty", GetCategoryName(_currentCategory)));
                return;
            }
            _currentItemIndex = (_currentItemIndex + direction + list.Count) % list.Count;
            _currentTarget = list[_currentItemIndex];
            AnnounceCurrentItem();
            PlayBeaconForCurrentTarget();
        }

        private void PruneStaleEntries(List<RadarEntry> list)
        {
            list.RemoveAll(e => e.GameActor == null || e.GameActor.destroyed);
        }

        private void RepeatCurrentTarget()
        {
            if (_currentTarget == null)
            {
                ScreenReader.Say(Loc.Get("radar_nothing_tracked"));
                return;
            }
            string announcement = FormatItemAnnouncement();
            if (_currentTarget.GameActor is Fighter fighter)
            {
                string details = GetFighterDetails(fighter);
                if (!string.IsNullOrEmpty(details))
                    announcement += ". " + details;
            }
            ScreenReader.Say(announcement);
            PlayBeaconForCurrentTarget();
        }

        private string GetFighterDetails(Fighter fighter)
        {
            if (fighter?.myStats == null) return "";
            Vector2 heroPos = GameMasterScript.heroPCActor.GetPos();
            return EnemyInfoHelper.GetDetailedInfo(fighter, heroPos);
        }

        private void AnnounceCategoryAndItem()
        {
            string catName = GetCategoryName(_currentCategory);
            int count = _categories[_currentCategory].Count;
            string catAnnounce = Loc.Get("radar_category", catName, count);

            if (_currentTarget != null)
            {
                string itemAnnounce = FormatItemAnnouncement();
                ScreenReader.Say(catAnnounce + " " + itemAnnounce);
            }
            else
            {
                ScreenReader.Say(catAnnounce);
            }
        }

        private void AnnounceCurrentItem()
        {
            if (_currentTarget == null) return;
            ScreenReader.Say(FormatItemAnnouncement());
        }

        private string FormatItemAnnouncement()
        {
            Vector2 heroPos = GameMasterScript.heroPCActor.GetPos();
            float dist = Vector2.Distance(heroPos, _currentTarget.Position);
            string dir = Main.World.GetDirectionName(_currentTarget.Position - heroPos);
            return Loc.Get("radar_item_simple", _currentTarget.Name, (int)dist, dir);
        }

        private void PlayBeaconForCurrentTarget()
        {
            if (_currentTarget == null || _beaconSource == null || _toneClip == null) return;
            _lastBeaconTime = Time.time;

            // Calculate stereo pan (left/right) and pitch (north/south) from direction
            Vector2 heroPos = GameMasterScript.heroPCActor.GetPos();
            Vector2 delta = _currentTarget.Position - heroPos;
            float pan = 0f;
            float pitch = 1f;
            if (delta.magnitude > 0.1f)
            {
                Vector2 dir = delta.normalized;
                // Pan: left ear (-1) to right ear (+1)
                pan = Mathf.Clamp(dir.x, -1f, 1f);
                // Pitch: north = high note (1.8), south = low note (0.5), level = middle (1.15)
                pitch = 1.15f + dir.y * 0.65f;
            }

            _beaconSource.panStereo = pan;
            _beaconSource.pitch = pitch;
            _beaconSource.PlayOneShot(_toneClip, 0.6f);
        }

        private void DeactivateRadar()
        {
            DeactivateRadar(true);
        }

        /// <summary>
        /// Turns radar off. Pass announce=false to suppress the "Radar off" message.
        /// </summary>
        public void DeactivateRadar(bool announce)
        {
            _radarActive = false;
            _currentTarget = null;
            _categories.Clear();
            if (announce) ScreenReader.Say(Loc.Get("radar_off"));
        }

        private RadarCategory FindNextNonEmptyCategory(int startIndex, int direction)
        {
            for (int i = 0; i < (int)RadarCategory.COUNT; i++)
            {
                int idx = (startIndex + i * (direction >= 0 ? 1 : -1) + (int)RadarCategory.COUNT) % (int)RadarCategory.COUNT;
                if (_categories[(RadarCategory)idx].Count > 0)
                    return (RadarCategory)idx;
            }
            return (RadarCategory)startIndex;
        }

        private string GetCategoryName(RadarCategory cat)
        {
            switch (cat)
            {
                case RadarCategory.Enemies: return Loc.Get("radar_cat_enemies");
                case RadarCategory.NPCs: return Loc.Get("radar_cat_npcs");
                case RadarCategory.Items: return Loc.Get("radar_cat_items");
                case RadarCategory.Stairs: return Loc.Get("radar_cat_stairs");
                case RadarCategory.Destructibles: return Loc.Get("radar_cat_destructibles");
                default: return "";
            }
        }

        /// <summary>
        /// Returns true if a destructible is a terrain effect (water, lava, mud, etc.)
        /// rather than something the player can meaningfully interact with.
        /// </summary>
        private static bool IsTerrainDestructible(Destructible dest)
        {
            switch (dest.mapObjType)
            {
                case SpecialMapObject.WATER:
                case SpecialMapObject.MUD:
                case SpecialMapObject.LAVA:
                case SpecialMapObject.ELECTRIC:
                case SpecialMapObject.ISLANDSWATER:
                case SpecialMapObject.OILSLICK:
                case SpecialMapObject.LAVA_LIKE_HAZARD:
                case SpecialMapObject.MONSTERSPAWNER:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Returns true if a destructible actor is hazardous (damages on step).
        /// </summary>
        private static bool IsHazardousDestructible(Actor actor)
        {
            var dest = actor as Destructible;
            if (dest == null) return false;
            // Ice shards and similar hazards that damage on step
            if (dest.actorRefName.Contains("iceshard")) return true;
            if (dest.actorRefName.Contains("fireshard")) return true;
            if (dest.actorRefName.Contains("trap")) return true;
            // Destructibles with a status effect that triggers when stepped on
            if (dest.dtStatusEffect != null && dest.destroyOnStep) return true;
            return false;
        }
    }
}
