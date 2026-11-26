using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Firebase;
using Firebase.Analytics;
using Firebase.Crashlytics;
using Firebase.Extensions;
using Firebase.RemoteConfig;
using Game.Core.Analytics;
using Game.Core.Settings;
using UnityEngine;

namespace Game.Analytics
{
    /// <summary>
    /// Starts Firebase before the first scene (plan section 17: analytics, crash reports and remote config from the
    /// first prototype) and plugs it into the game's own interfaces, so gameplay code never touches Firebase:
    /// AnalyticsEvents goes to Firebase Analytics, uncaught exceptions to Crashlytics, and RemoteTuning reads
    /// Remote Config. Events logged while Firebase starts up are queued and sent once it is ready. If Firebase can
    /// not start (Google Play services missing or out of date), the game runs on without it.
    /// </summary>
    public static class FirebaseServices
    {
        const int MaxQueued = 200;
        static readonly List<(string name, (string key, object value)[] parameters)> queue = new List<(string, (string, object)[])>();
        static bool ready, failed;

        public static bool Ready => ready;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Start()
        {
            ready = failed = false;
            queue.Clear();
            AnalyticsEvents.Sink = new QueueSink();
            try
            {
                FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
                {
                    if (task.IsFaulted || task.Result != DependencyStatus.Available)
                    {
                        Debug.LogWarning($"[firebase] not available ({(task.IsFaulted ? task.Exception?.GetBaseException().Message : task.Result.ToString())}); analytics off this session");
                        Fail();
                        return;
                    }
                    OnReady();
                });
            }
            catch (Exception e)
            {
                Debug.LogWarning("[firebase] could not start: " + e.Message);
                Fail();
            }
        }

        static void Fail()
        {
            failed = true;
            queue.Clear();
            if (AnalyticsEvents.Sink is QueueSink) AnalyticsEvents.Sink = null;
        }

        static void OnReady()
        {
            ready = true;
            Crashlytics.ReportUncaughtExceptionsAsFatal = true;
            FirebaseAnalytics.SetAnalyticsCollectionEnabled(true);

            var sink = new FirebaseSink();
            AnalyticsEvents.Sink = sink;
            foreach (var (name, parameters) in queue) sink.Send(name, parameters);
            queue.Clear();

            SetTierProperty(QualityTiers.Current);
            QualityTiers.Applied += SetTierProperty;
            StartRemoteConfig();
        }

        static void SetTierProperty(QualityTier tier)
        {
            if (!ready) return;
            FirebaseAnalytics.SetUserProperty("graphics_tier", tier.ToString().ToLowerInvariant());
            FirebaseAnalytics.SetUserProperty("graphics_auto", GameSettings.QualityTierChoice == GameSettings.AutoTier ? "true" : "false");
        }

        // ---------- remote config ----------

        static void StartRemoteConfig()
        {
            var rc = FirebaseRemoteConfig.DefaultInstance;
            RemoteTuning.Provider = new RemoteConfigValues(rc);
            rc.FetchAndActivateAsync().ContinueWithOnMainThread(t =>
            {
                if (t.IsFaulted) { Debug.LogWarning("[firebase] remote config fetch failed: " + t.Exception?.GetBaseException().Message); return; }
                RemoteTuning.RaiseUpdated();
            });
        }

        sealed class RemoteConfigValues : IRemoteValues
        {
            readonly FirebaseRemoteConfig rc;
            public RemoteConfigValues(FirebaseRemoteConfig rc) { this.rc = rc; }

            public bool TryGetString(string key, out string value)
            {
                value = null;
                try
                {
                    var v = rc.GetValue(key);
                    if (v.Source != ValueSource.RemoteValue) return false;   // our own defaults live in code, not in Firebase
                    value = v.StringValue;
                    return true;
                }
                catch (Exception) { return false; }
            }
        }

        // ---------- analytics ----------

        // holds events until Firebase is ready (a few hundred milliseconds at start-up)
        sealed class QueueSink : IAnalyticsSink
        {
            public void Send(string eventName, (string key, object value)[] parameters)
            {
                if (failed) return;
                if (queue.Count < MaxQueued) queue.Add((eventName, parameters));
            }
        }

        sealed class FirebaseSink : IAnalyticsSink
        {
            public void Send(string eventName, (string key, object value)[] parameters)
            {
                try
                {
                    var list = new List<Parameter>(Mathf.Min(parameters.Length, 25));
                    for (int i = 0; i < parameters.Length && list.Count < 25; i++)
                    {
                        var p = ToParameter(Name(parameters[i].key), parameters[i].value);
                        if (p != null) list.Add(p);
                    }
                    FirebaseAnalytics.LogEvent(Name(eventName), list.ToArray());
                }
                catch (Exception e) { Debug.LogWarning($"[firebase] event '{eventName}' not sent: {e.Message}"); }
            }

            static Parameter ToParameter(string key, object value) => value switch
            {
                null => null,
                bool b => new Parameter(key, b ? "true" : "false"),
                int n => new Parameter(key, n),
                long n => new Parameter(key, n),
                short n => new Parameter(key, n),
                byte n => new Parameter(key, n),
                float f => new Parameter(key, Math.Round(f, 3)),
                double d => new Parameter(key, Math.Round(d, 3)),
                Enum e => new Parameter(key, Snake(e.ToString())),
                IFormattable f => new Parameter(key, Clip(f.ToString(null, CultureInfo.InvariantCulture))),
                _ => new Parameter(key, Clip(value.ToString())),
            };

            static string Clip(string s) => s.Length <= 100 ? s : s.Substring(0, 100);
        }

        /// <summary>Firebase names: letters, digits and underscores, starting with a letter, at most 40 characters.</summary>
        static string Name(string raw)
        {
            var sb = new StringBuilder(raw.Length);
            foreach (char c in raw) sb.Append(char.IsLetterOrDigit(c) && c < 128 ? char.ToLowerInvariant(c) : '_');
            if (sb.Length == 0 || !char.IsLetter(sb[0])) sb.Insert(0, 'e');
            return sb.Length <= 40 ? sb.ToString() : sb.ToString(0, 40);
        }

        /// <summary>"FollowBlood" becomes "follow_blood", so enum values read the same as the event names.</summary>
        static string Snake(string s)
        {
            var sb = new StringBuilder(s.Length + 4);
            for (int i = 0; i < s.Length; i++)
            {
                if (char.IsUpper(s[i]) && i > 0) sb.Append('_');
                sb.Append(char.ToLowerInvariant(s[i]));
            }
            return sb.ToString();
        }
    }
}
