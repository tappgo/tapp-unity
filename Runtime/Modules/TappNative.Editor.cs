#if UNITY_EDITOR || (!UNITY_IOS && !UNITY_ANDROID)
using UnityEngine;

namespace TappGo.Modules
{
    /// <summary>
    /// What a Tapp package has in place of a native SDK in the Editor, and on every platform without one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>For Tapp's own packages, not for host code.</b> There is no native framework outside a device build,
    /// so every product ships a stub beside its device bodies — a designer pressing Play gets console lines,
    /// not a <c>DllNotFoundException</c>.
    /// </para>
    /// <para>
    /// <b>A no-op with a log line, not a simulation.</b> A stub never invents data that could be mistaken for
    /// the real thing: what it returns is spelled <see cref="StubValue"/>, so nobody reads a console line and
    /// believes a Live Activity started. It does return <i>well-formed envelopes</i>, though, so the JSON a
    /// package builds and the parsing it does are both exercised in the Editor and in CI.
    /// </para>
    /// </remarks>
    public static class TappNative
    {
        /// <summary>The Editor validates and builds payloads as iOS, the platform every setting applies to.</summary>
        public const TappPlatform Platform = TappPlatform.iOS;

        /// <summary>The reply to a call with nothing to return.</summary>
        public const string Ok = "{\"ok\":true}";

        /// <summary>
        /// What a stub reports in place of any real identifier or version.
        /// </summary>
        /// <remarks>
        /// Varies with where it runs: a host that logs <see cref="Tapp.SdkVersion"/> on a desktop player should
        /// not read <c>editor-stub</c> where there is no editor. Either value is unmistakably not a version
        /// number, which is the property that matters — the answer to "which native binary is loaded" is
        /// "none".
        /// </remarks>
#if UNITY_EDITOR
        public const string StubValue = "editor-stub";
#else
        public const string StubValue = "unsupported-platform";
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
        /// <b>In a shipped player they are silence.</b> A desktop or WebGL player that logs a line per call is
        /// describing work it did not do, in someone else's product, at their runtime cost. A platform that
        /// does nothing should say nothing.
        /// </para>
        /// </remarks>
        public static void Log(string call, string json = null)
        {
#if UNITY_EDITOR
            Debug.Log(json == null
                ? $"[Tapp] editor stub: {call}"
                : $"[Tapp] editor stub: {call} {json}");
#endif
        }
    }
}
#endif
