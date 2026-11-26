using System;
using System.Text;
using UnityEngine;

namespace Game.Core.Analytics
{
    /// <summary>Where analytics events go: Firebase Analytics (Game.Analytics sets it at start-up).</summary>
    public interface IAnalyticsSink
    {
        void Send(string eventName, (string key, object value)[] parameters);
    }

    /// <summary>
    /// The game's analytics events (plan section 16: hunt funnel, clue use, shots, results, abandon rate). Code
    /// calls Log with an event name and a few parameters; a sink sends them on. Editor and development builds also
    /// write them to the console so the funnel can be checked by hand.
    /// </summary>
    public static class AnalyticsEvents
    {
        public static IAnalyticsSink Sink { get; set; }

        /// <summary>Raised for every event (tests and the dev overlay).</summary>
        public static event Action<string> Logged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Sink = null; Logged = null; }

        public static void Log(string eventName, params (string key, object value)[] parameters)
        {
            Sink?.Send(eventName, parameters);
            if (Application.isEditor || Debug.isDebugBuild)   // also with Firebase on: the funnel stays checkable by hand
            {
                var sb = new StringBuilder("[analytics] ").Append(eventName);
                foreach (var (key, value) in parameters) sb.Append(' ').Append(key).Append('=').Append(value);
                Debug.Log(sb.ToString());
            }
            Logged?.Invoke(eventName);
        }
    }
}
