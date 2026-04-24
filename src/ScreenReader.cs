using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace TangledeepAccess
{
    public static class ScreenReader
    {
        [DllImport("Tolk.dll")] private static extern void Tolk_Load();
        [DllImport("Tolk.dll")] private static extern void Tolk_Unload();
        [DllImport("Tolk.dll")] private static extern bool Tolk_IsLoaded();
        [DllImport("Tolk.dll")] private static extern bool Tolk_HasSpeech();
        [DllImport("Tolk.dll", CharSet = CharSet.Unicode)] private static extern bool Tolk_Output(string text, bool interrupt);
        [DllImport("Tolk.dll")] private static extern bool Tolk_Silence();

        private static bool _available = false;

        private const int MaxLogEntries = 30;
        private static readonly LinkedList<string> _messageLog = new LinkedList<string>();

        public static void Initialize()
        {
            try
            {
                Tolk_Load();
                _available = Tolk_IsLoaded();
                if (_available)
                {
                    Main.Log.LogInfo("ScreenReader: Tolk loaded successfully.");
                    Tolk_Output("Tangledeep Access initialized", true);
                }
                else
                {
                    Main.Log.LogWarning("ScreenReader: Tolk failed to load.");
                }
            }
            catch (Exception ex)
            {
                Main.Log.LogError($"ScreenReader: Initialization crash: {ex.Message}");
                _available = false;
            }
        }

        public static void Say(string text, bool interrupt = true)
        {
            if (string.IsNullOrEmpty(text)) return;
            AddToLog(text);
            if (!_available)
            {
                Main.Log.LogDebug($"[NO SPEECH] {text}");
                return;
            }
            Tolk_Output(text, interrupt);
        }

        private static void AddToLog(string text)
        {
            _messageLog.AddLast(text);
            while (_messageLog.Count > MaxLogEntries)
                _messageLog.RemoveFirst();
        }

        /// <summary>
        /// Returns the last N messages from the log, newest first.
        /// </summary>
        public static List<string> GetRecentMessages(int count)
        {
            var result = new List<string>();
            var node = _messageLog.Last;
            while (node != null && result.Count < count)
            {
                result.Add(node.Value);
                node = node.Previous;
            }
            return result;
        }

        public static void Stop() { if (_available) Tolk_Silence(); }
        public static void Shutdown() { if (_available) Tolk_Unload(); }
    }
}
