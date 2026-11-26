using System;
using UnityEngine;

namespace Game.Core.Analytics
{
    /// <summary>Where remote values come from (Firebase Remote Config, set up by Game.Analytics).</summary>
    public interface IRemoteValues
    {
        bool TryGetString(string key, out string value);
    }

    /// <summary>
    /// Tuning values the team can change without a store update (plan section 16: remote config). Code asks for a
    /// key with the value it uses today; until the service has fetched (or offline, or in tests) that default is
    /// what comes back, so nothing ever depends on the network.
    /// </summary>
    public static class RemoteTuning
    {
        public static IRemoteValues Provider { get; set; }

        /// <summary>Raised when fresh values are active.</summary>
        public static event Action Updated;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Provider = null; Updated = null; }

        public static void RaiseUpdated() => Updated?.Invoke();

        public static string GetString(string key, string fallback) =>
            Provider != null && Provider.TryGetString(key, out var v) && !string.IsNullOrEmpty(v) ? v : fallback;

        public static float GetFloat(string key, float fallback) =>
            float.TryParse(GetString(key, null), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var f) ? f : fallback;

        public static int GetInt(string key, int fallback) =>
            int.TryParse(GetString(key, null), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var i) ? i : fallback;

        public static bool GetBool(string key, bool fallback)
        {
            var s = GetString(key, null);
            if (s == null) return fallback;
            if (s == "1" || s.Equals("true", StringComparison.OrdinalIgnoreCase)) return true;
            if (s == "0" || s.Equals("false", StringComparison.OrdinalIgnoreCase)) return false;
            return fallback;
        }
    }
}
