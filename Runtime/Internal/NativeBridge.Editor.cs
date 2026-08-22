#if !UNITY_IOS || UNITY_EDITOR
using System;
using UnityEngine;

namespace TappGo.Internal
{
    /// <summary>
    /// The implementation for the Editor and for every platform with no native SDK behind it: logs what would
    /// have been sent, and answers.
    /// </summary>
    /// <remarks>
    /// <para>
    /// There is no native framework outside a device build, so this exists so Play mode works — a designer
    /// pressing Play gets console lines, not a <c>DllNotFoundException</c>.
    /// </para>
    /// <para>
    /// <b>A no-op with a log line, not a simulation.</b> It never invents data that could be mistaken for the
    /// real thing: what it returns is spelled <c>editor-stub</c>, so nobody reads a console line and believes a
    /// Live Activity started. Live Activities are verified on a device, and nothing here is evidence about
    /// them.
    /// </para>
    /// <para>
    /// It does return <i>well-formed envelopes</i>, though, so the JSON the package builds and the parsing it
    /// does are both exercised in the Editor and in CI. Those are the two halves most likely to break silently.
    /// </para>
    /// </remarks>
    internal static class NativeBridge
    {
        /// <summary>
        /// What the stub reports in place of any real identifier or version.
        /// </summary>
        /// <remarks>
        /// Varies with where it runs, for the same reason <see cref="Prefix"/> does: a host that logs
        /// <see cref="Tapp.SdkVersion"/> on Android should not read <c>editor-stub</c> on a device that has no
        /// editor. Either value is unmistakably not a version number, which is the property that matters — the
        /// answer to "which native binary is loaded" is "none".
        /// </remarks>
#if UNITY_EDITOR
        internal const string StubValue = "editor-stub";
#else
        internal const string StubValue = "ios-only";
#endif

        internal static string SdkVersion()
        {
            Log("SdkVersion");
            return $"{{\"ok\":true,\"value\":{{\"version\":\"{StubValue}\"}}}}";
        }

        internal static string Configure(string json)
        {
            Log("Configure", json);
            return Ok;
        }

        internal static string SetUserId(string json)
        {
            Log("SetUserId", json);
            return Ok;
        }

        internal static void Logout(Action<string> completion)
        {
            Log("Logout");
            completion?.Invoke(Ok);
        }

        /// <summary>
        /// Always reports the URL as unhandled.
        /// </summary>
        /// <remarks>
        /// Whether a URL belongs to Tapp is decided by configuration the Editor has never fetched, so there is
        /// no honest answer other than "not mine, you take it" — which is also the safe one for a host testing
        /// its own deep-link routing in Play mode.
        /// </remarks>
        internal static string HandleUrl(string json)
        {
            Log("HandleUrl", json);
            return "{\"ok\":true,\"value\":{\"handled\":false}}";
        }

        internal static void StartLiveActivity(string json, Action<string> completion)
        {
            Log("StartLiveActivity", json);
            completion?.Invoke($"{{\"ok\":true,\"value\":{{\"activityID\":\"{StubValue}\"}}}}");
        }

        internal static void UpdateLiveActivity(string json, Action<string> completion)
        {
            Log("UpdateLiveActivity", json);
            completion?.Invoke(Ok);
        }

        internal static void EndLiveActivity(string json, Action<string> completion)
        {
            Log("EndLiveActivity", json);
            completion?.Invoke(Ok);
        }

        /// <summary>
        /// Always empty.
        /// </summary>
        /// <remarks>
        /// Returning a fabricated running activity would let reconciliation code appear to work in the Editor
        /// and then behave differently on a device. Empty is the honest answer: nothing is running, because
        /// nothing can be.
        /// </remarks>
        internal static string ActiveLiveActivities()
        {
            Log("ActiveLiveActivities");
            return "{\"ok\":true,\"value\":{\"activities\":[]}}";
        }

        private const string Ok = "{\"ok\":true}";

        /// <summary>
        /// Names this implementation in a log line and in <see cref="SdkVersion"/>.
        /// </summary>
        /// <remarks>
        /// This file compiles for the Editor <i>and</i> for every non-iOS player, and the two want different
        /// words: <c>editor-stub</c> answers "why is nothing rendering in Play mode", while on an Android
        /// build it would name an editor that isn't there and read as a bug in Tapp rather than as the
        /// platform not being supported yet. Compile-time, so the Editor's own wording stays a constant the
        /// test suites can assert on.
        /// </remarks>
#if UNITY_EDITOR
        private const string Prefix = "editor stub";
#else
        private const string Prefix = "iOS only, no-op";
#endif

        /// <summary>
        /// Logs what would have been sent — <b>in the Editor only</b>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// In the Editor these lines are the whole point: they are how a designer sees that a call happened at
        /// all when nothing renders.
        /// </para>
        /// <para>
        /// <b>In a shipped non-iOS player they are silence.</b> Android is not part of this version, and a
        /// player that logs a line per call — and, through <see cref="Bootstrap"/>, once announced a native
        /// version and "Configured on launch" — is describing work it did not do, in someone else's product,
        /// at their runtime cost. A platform that does nothing should say nothing.
        /// </para>
        /// </remarks>
        private static void Log(string call, string json = null)
        {
#if UNITY_EDITOR
            Debug.Log(json == null
                ? $"[Tapp] {Prefix}: {call}"
                : $"[Tapp] {Prefix}: {call} {json}");
#endif
        }
    }
}
#endif
