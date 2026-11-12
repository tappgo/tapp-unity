using System;

namespace TappGo
{
    /// <summary>
    /// The outcome of a Tapp call: either it worked, or it carries a stable <see cref="Code"/> to branch on.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Branch on <see cref="Code"/>, never on <see cref="Message"/> — the message is for your logs and may be
    /// reworded in any release. The full set of codes is small and additive; see
    /// <see cref="TappErrorCode"/>.
    /// </para>
    /// <para>
    /// A struct, so a per-call result costs no allocation on a frame where one might be running.
    /// </para>
    /// </remarks>
    public readonly struct TappResult
    {
        /// <summary>Whether the call succeeded.</summary>
        public bool Ok { get; }

        /// <summary>A stable identifier for the failure, or <c>null</c> when <see cref="Ok"/> is true.</summary>
        public string Code { get; }

        /// <summary>A human-readable explanation for logs, or <c>null</c> when <see cref="Ok"/> is true.</summary>
        public string Message { get; }

        internal TappResult(bool ok, string code, string message)
        {
            Ok = ok;
            Code = code;
            Message = message;
        }

        /// <summary>A one-line form for logging.</summary>
        public override string ToString() => Ok ? "ok" : $"{Code}: {Message}";
    }

    /// <summary>
    /// The outcome of a Tapp call that returns something.
    /// </summary>
    /// <typeparam name="TValue">What the call returns on success.</typeparam>
    /// <remarks>
    /// <see cref="Value"/> is only meaningful when <see cref="Ok"/> is true; on failure it is
    /// <c>default</c>. Check <see cref="Ok"/> first.
    /// </remarks>
    public readonly struct TappResult<TValue>
    {
        /// <summary>Whether the call succeeded.</summary>
        public bool Ok { get; }

        /// <summary>What the call returned, or <c>default</c> when <see cref="Ok"/> is false.</summary>
        public TValue Value { get; }

        /// <summary>A stable identifier for the failure, or <c>null</c> when <see cref="Ok"/> is true.</summary>
        public string Code { get; }

        /// <summary>A human-readable explanation for logs, or <c>null</c> when <see cref="Ok"/> is true.</summary>
        public string Message { get; }

        internal TappResult(bool ok, TValue value, string code, string message)
        {
            Ok = ok;
            Value = value;
            Code = code;
            Message = message;
        }

        /// <summary>Drops <see cref="Value"/>, for callers that only care whether it worked.</summary>
        public TappResult AsResult() => new TappResult(Ok, Code, Message);

        /// <summary>A one-line form for logging.</summary>
        public override string ToString() => Ok ? $"ok: {Value}" : $"{Code}: {Message}";
    }

    /// <summary>
    /// Every <see cref="TappResult.Code"/> the SDK can report.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Constants rather than an <c>enum</c>, on purpose. A newer native binary may report a code this build has
    /// never heard of, and <c>Enum.Parse</c> would throw on it while <c>Enum.TryParse</c> would silently
    /// collapse it to the zero value. Compare against these and treat anything unrecognised as an
    /// unhandled failure — log it, don't crash on it.
    /// </para>
    /// </remarks>
    public static class TappErrorCode
    {
        /// <summary>The configuration or its App Group was invalid; the message says what.</summary>
        public const string InvalidConfiguration = "invalidConfiguration";

        /// <summary>A call was made before <see cref="Tapp.Configure"/> succeeded.</summary>
        public const string NotConfigured = "notConfigured";

        /// <summary>
        /// Live Activities are unavailable — the user turned them off, or the app is missing
        /// <c>NSSupportsLiveActivities</c>. The build hook adds that key, so in a Tapp-built Xcode project
        /// this means the user's setting.
        /// </summary>
        public const string LiveActivitiesUnavailable = "liveActivitiesUnavailable";

        /// <summary>iOS refused to start the activity; the message carries its reason.</summary>
        public const string LiveActivityStartFailed = "liveActivityStartFailed";

        /// <summary>
        /// That id already has a card up on that screen. Start a different screen to add a second card, update
        /// this one, or end it first.
        /// </summary>
        public const string LiveActivityAlreadyRunning = "liveActivityAlreadyRunning";

        /// <summary>
        /// The call belongs to a native module the app does not link. The message names the module. Answered
        /// before <see cref="NotConfigured"/>, because no configuration would change it.
        /// </summary>
        /// <remarks>
        /// Installing a Tapp product package is what links its native module, so a Unity build does not
        /// normally see this: a product that is not installed has no C# API to call. It means the Xcode
        /// project was edited after export, or an append build kept an older one.
        /// </remarks>
        public const string SurfaceNotLinked = "surfaceNotLinked";

        /// <summary>
        /// This device or platform cannot present Picture in Picture at all — an Android player, an iPad
        /// without the capability, or a build whose native SDK predates it. Expected, not exceptional.
        /// </summary>
        public const string PictureInPictureUnsupported = "pictureInPictureUnsupported";

        /// <summary>
        /// The app's <c>Info.plist</c> does not list <c>audio</c> under <c>UIBackgroundModes</c>. The build hook
        /// adds it when Picture in Picture is switched on in Project Settings → Tapp, so this means it was off.
        /// </summary>
        public const string PictureInPictureBackgroundAudioMissing = "pictureInPictureBackgroundAudioMissing";

        /// <summary>The video could not be loaded; the message carries the reason.</summary>
        public const string PictureInPictureVideoUnavailable = "pictureInPictureVideoUnavailable";

        /// <summary>
        /// Picture in Picture did not start: no active window scene, another start already in flight, a video that
        /// never started playing, or a start abandoned before it finished — the user came back to the app, or the
        /// window was stopped or failed. The message carries the reason.
        /// </summary>
        public const string PictureInPictureStartFailed = "pictureInPictureStartFailed";

        /// <summary>
        /// The payload this package sent did not match what the native binary expects — a version mismatch
        /// between the package and the framework it carries. Not something a host can cause; report it.
        /// </summary>
        public const string InvalidRequest = "invalidRequest";

        /// <summary>A defect inside Tapp, not in your integration and not on the device. Report it.</summary>
        public const string Unexpected = "unexpected";
    }
}
