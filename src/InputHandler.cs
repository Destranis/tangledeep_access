using UnityEngine;
using HarmonyLib;
using Rewired;

namespace TangledeepAccess
{
    /// <summary>
    /// Handler for keyboard movement enhancements and gameplay shortcuts.
    /// Implements Smart Diagonals, Status Check (F2), and common action keys.
    /// </summary>
    public class InputHandler
    {
        private static float _lastArrowPressTime = 0f;
        private static float _diagonalBufferTime = 0.05f;
        private static Directions _bufferedDirection = Directions.NEUTRAL;

        public void Update()
        {
            if (GameMasterScript.gmsSingleton == null || !GameMasterScript.gameLoadSequenceCompleted) return;

            // Wait Turn: Dot (.)
            if (Input.GetKeyDown(KeyCode.Period))
            {
                ExecuteWaitTurn();
            }

            // Use Healing Flask: F
            // F is also "Fire Ranged Weapon" in Rewired — the game may enter targeting first.
            // We undo that and use the flask instead.
            if (Input.GetKeyDown(KeyCode.F)
                && !Input.GetKey(KeyCode.LeftShift) && !Input.GetKey(KeyCode.RightShift)
                && !Input.GetKey(KeyCode.LeftControl) && !Input.GetKey(KeyCode.RightControl)
                && !UIManagerScript.AnyInteractableWindowOpen()
                && !GameMasterScript.gmsSingleton.turnExecuting
                && !GameMasterScript.playerMovingAnimation
                && !GameMasterScript.IsGameInCutsceneOrDialog())
            {
                // If Rewired entered targeting from "Fire Ranged Weapon", cancel it
                if (UIManagerScript.singletonUIMS.CheckTargeting())
                {
                    UIManagerScript.singletonUIMS.ExitTargeting();
                    GameMasterScript.heroPCActor.SetActorData("fireranged", 0);
                }
                DebugLogger.LogInput("F", "Use Flask");
                GameMasterScript.gmsSingleton.UseAbilityRegenFlask();
            }

            // --- Name Entry: R to Randomize ---
            if (Input.GetKeyDown(KeyCode.R) && IsOnNameEntryScreen())
            {
                RandomizeName();
            }
        }

        private bool IsOnNameEntryScreen()
        {
            if (CharCreation.singleton == null) return false;
            var state = CharCreation.NameEntryScreenState;
            if (state != ENameEntryScreenState.deciding_on_name) return false;
            bool inputActive = (bool)AccessTools.Field(typeof(CharCreation), "nameInputIsActive").GetValue(null);
            return !inputActive;
        }

        private void RandomizeName()
        {
            if (CharCreation.singleton == null) return;
            CharCreation.singleton.GenerateRandomNameAndFillField();
            string name = CharCreation.nameInputTextBox?.text ?? "";
            ScreenReader.Say(name);
        }

        private void ExecuteWaitTurn()
        {
            if (TDInputHandler.player == null || GameMasterScript.gmsSingleton == null) return;

            if (Time.time - TDInputHandler.timeSinceLastActionInput >= GameMasterScript.gmsSingleton.playerMoveSpeed * 1.25f)
            {
                DebugLogger.LogInput(".", "Wait Turn");
                
                TurnData turnData = new TurnData();
                turnData.actorThatInitiatedTurn = GameMasterScript.heroPCActor;
                turnData.SetTurnType(TurnTypes.PASS);
                
                GameMasterScript.gmsSingleton.TryNextTurn(turnData, true);
                TDInputHandler.timeSinceLastActionInput = Time.time;
                GameMasterScript.heroPCActor.myMovable.Jab(Directions.NORTH);
                
                ScreenReader.Say("Wait");
            }
        }

        /// <summary>
        /// Harmony patch to intercept directional input and implement smart diagonal buffering.
        /// Also suppresses movement when Shift is held (radar category navigation).
        /// Adds arrow key fallback for WASD mode where arrows aren't mapped in Rewired.
        /// </summary>
        [HarmonyPatch(typeof(TDInputHandler), nameof(TDInputHandler.GetDirectionalInput))]
        public static class Patch_TDInputHandler_GetDirectionalInput
        {
            public static void Postfix(ref Directions __result)
            {
                // Suppress movement when Shift is held (used for radar navigation)
                if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                {
                    __result = Directions.NEUTRAL;
                    return;
                }

                // Arrow key fallback: In WASD mode, Rewired doesn't map arrow keys
                // to movement actions. Inject arrow key input so menus and gameplay
                // always respond to arrow keys regardless of layout.
                if (__result == Directions.NEUTRAL)
                {
                    bool up = Input.GetKey(KeyCode.UpArrow);
                    bool down = Input.GetKey(KeyCode.DownArrow);
                    bool left = Input.GetKey(KeyCode.LeftArrow);
                    bool right = Input.GetKey(KeyCode.RightArrow);

                    if (up && left) __result = Directions.NORTHWEST;
                    else if (up && right) __result = Directions.NORTHEAST;
                    else if (down && left) __result = Directions.SOUTHWEST;
                    else if (down && right) __result = Directions.SOUTHEAST;
                    else if (up) __result = Directions.NORTH;
                    else if (down) __result = Directions.SOUTH;
                    else if (left) __result = Directions.WEST;
                    else if (right) __result = Directions.EAST;
                }

                if (IsDiagonal(__result))
                {
                    AnnounceDirection(__result);
                    return;
                }

                if (IsCardinal(__result))
                {
                    float currentTime = Time.unscaledTime;

                    if (currentTime - _lastArrowPressTime < _diagonalBufferTime)
                    {
                        Directions combined = CombineDirections(_bufferedDirection, __result);
                        if (IsDiagonal(combined))
                        {
                            __result = combined;
                            AnnounceDirection(__result);
                            return;
                        }
                    }

                    _lastArrowPressTime = currentTime;
                    _bufferedDirection = __result;
                }
            }
        }

        private static bool IsCardinal(Directions dir)
        {
            return dir == Directions.NORTH || dir == Directions.SOUTH || 
                   dir == Directions.EAST || dir == Directions.WEST;
        }

        private static bool IsDiagonal(Directions dir)
        {
            return dir == Directions.NORTHEAST || dir == Directions.NORTHWEST || 
                   dir == Directions.SOUTHEAST || dir == Directions.SOUTHWEST;
        }

        private static Directions CombineDirections(Directions d1, Directions d2)
        {
            if ((d1 == Directions.NORTH && d2 == Directions.WEST) || (d1 == Directions.WEST && d2 == Directions.NORTH))
                return Directions.NORTHWEST;
            if ((d1 == Directions.NORTH && d2 == Directions.EAST) || (d1 == Directions.EAST && d2 == Directions.NORTH))
                return Directions.NORTHEAST;
            if ((d1 == Directions.SOUTH && d2 == Directions.WEST) || (d1 == Directions.WEST && d2 == Directions.SOUTH))
                return Directions.SOUTHWEST;
            if ((d1 == Directions.SOUTH && d2 == Directions.EAST) || (d1 == Directions.EAST && d2 == Directions.SOUTH))
                return Directions.SOUTHEAST;
            
            return d2;
        }

        private static void AnnounceDirection(Directions dir)
        {
            if (Main.DebugMode)
            {
                DebugLogger.Log(LogCategory.Input, $"Movement: {dir}");
            }
        }
    }
}
