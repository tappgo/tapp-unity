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
        /// The payload this package sent did not match what the native binary expects — a version mismatch
        /// between the package and the framework it carries. Not something a host can cause; report it.
        /// </summary>
        public const string InvalidRequest = "invalidRequest";

        /// <summary>A defect inside Tapp, not in your integration and not on the device. Report it.</summary>
        public const string Unexpected = "unexpected";
    }
}
