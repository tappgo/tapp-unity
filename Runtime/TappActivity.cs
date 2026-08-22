using System;

namespace TappGo
{
    /// <summary>
    /// What to show when starting or updating a Live Activity.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Both <see cref="Id"/> and <see cref="EntryId"/> are configured server-side — this package names them,
    /// and the SDK resolves the content. You never build or hold the activity's content yourself, which is
    /// what lets the design change without a new app build.
    /// </para>
    /// </remarks>
    public struct TappActivityRequest
    {
        /// <summary>
        /// The Tapp live activity id, as configured server-side.
        /// </summary>
        /// <remarks>
        /// Half of a card's address — <see cref="EntryId"/> is the other half — and it never changes, so updates
        /// and ends use the same pair, and you never have to persist an identifier you didn't choose.
        /// </remarks>
        public string Id;

        /// <summary>The server-configured screen to show, for example <c>"screen-1"</c>.</summary>
        /// <remarks>
        /// <para>
        /// <b>A card is the pair <see cref="Id"/> + <see cref="EntryId"/>.</b> Starting the same pair twice fails
        /// with <see cref="TappErrorCode.LiveActivityAlreadyRunning"/>; starting a <i>different</i> screen of the
        /// same id puts a second card up alongside the first, each with its own clock, content and end.
        /// </para>
        /// <para>
        /// Case is ignored — <c>"Screen-1"</c> and <c>"SCREEN-1"</c> reach the same screen, whatever casing the
        /// back office authored.
        /// </para>
        /// </remarks>
        public string EntryId;

        /// <summary>
        /// How long the activity's countdowns run, in <b>whole seconds from the moment it starts</b>, or
        /// <c>null</c> to let the design's own durations stand.
        /// </summary>
        /// <remarks>
        /// <para>
        /// A duration, not a point in time. Every countdown in the design runs for this instead of the length
        /// it declares, and the content is marked stale when it runs out — measured from the start, so
        /// re-rendering (a push update, expanding the Dynamic Island, a lock and unlock) never restarts the
        /// clock. Passing an epoch timestamp here produces a countdown decades long.
        /// </para>
        /// <para>
        /// It does <b>not</b> end the activity — use <see cref="Tapp.EndLiveActivity"/> for that. Zero or a
        /// negative value is accepted: the countdowns render at zero and the content is stale at once.
        /// </para>
        /// </remarks>
        public int? Seconds;
    }

    /// <summary>
    /// A Live Activity card the system currently reports, as returned by
    /// <see cref="Tapp.ActiveLiveActivities"/>.
    /// </summary>
    public readonly struct TappActivityInfo
    {
        /// <summary>The Tapp live activity id it was started with.</summary>
        public string Id { get; }

        /// <summary>The system's own identifier for the activity.</summary>
        public string ActivityId { get; }

        /// <summary>
        /// The screen this card is showing — the other half of its address, and what
        /// <see cref="Tapp.UpdateLiveActivity"/> and <see cref="Tapp.EndLiveActivity"/> need to reach it.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <see cref="Id"/> alone does not name a card: one id runs one card per screen, so two cards of a
        /// campaign come back as two entries with the same <see cref="Id"/> and different screens. Fixed for the
        /// card's whole life — nothing moves a card to another screen — so a value read here stays valid for as
        /// long as the card exists, including across a relaunch.
        /// </para>
        /// <para>
        /// <c>null</c> only for a card this device cannot address: a push-to-start that named no screen at all.
        /// Nothing here can drive one; the SDK ends it if it turns out to be rendering nothing.
        /// </para>
        /// </remarks>
        public string EntryId { get; }

        /// <summary>Where the activity is in its lifecycle.</summary>
        public TappActivityState State { get; }

        /// <summary>
        /// The APNs update token addressing this card, as lowercase hex — <b>reported only by a Debug-built
        /// native framework</b>, and always <c>null</c> otherwise.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Always <c>null</c> in a shipped build.</b> A push token is observability, not API, so the native
        /// SDK carries it in <c>DEBUG</c> only — the field is not merely empty in a Release
        /// <c>TappGo.xcframework</c>, it does not exist there, and the reply simply never contains it. This
        /// property stays on the C# side regardless, because a wrapper cannot know which framework it was built
        /// against. It is here for a debug console driving a Debug framework, and as the starting point for a
        /// hand-made server push while a back-office setup is being proved out. <b>Do not build a feature on
        /// it.</b> Pushes are unaffected: the SDK registers every token with the Tapp back office itself, in
        /// every configuration, and nothing has to be forwarded.
        /// </para>
        /// <para>
        /// Even against a Debug framework <c>null</c> is ordinary. iOS issues the token a moment <i>after</i> the
        /// activity starts, so a call made right after <see cref="Tapp.StartLiveActivity"/> completes usually
        /// sees <c>null</c> and the next one does not. A card that has <b>ended</b> has had its token dropped
        /// while the system goes on listing it, and a push-to-start card has none until iOS issues it one.
        /// </para>
        /// <para>
        /// The value <b>rotates</b> — iOS reissues it whenever it likes, and only the newest can push. Read it
        /// when you need it rather than storing it.
        /// </para>
        /// </remarks>
        public string UpdateToken { get; }

        internal TappActivityInfo(
            string id, string activityId, string entryId, TappActivityState state, string updateToken)
        {
            Id = id;
            ActivityId = activityId;
            EntryId = entryId;
            State = state;
            UpdateToken = updateToken;
        }

        /// <summary>A one-line form for logging.</summary>
        public override string ToString() =>
            string.IsNullOrEmpty(EntryId) ? $"{Id} ({State})" : $"{Id} / {EntryId} ({State})";
    }

    /// <summary>
    /// Where a Live Activity is in its lifecycle.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>An open set, not an enum.</b> The states belong to Apple, which has added them before, so this wraps
    /// the raw string and keeps a value this build doesn't recognise instead of rejecting it. An
    /// <c>enum</c> here would either throw on a newer state or — worse — silently report it as the zero value.
    /// </para>
    /// <para>
    /// Compare against the constants, and treat anything unrecognised as still running, which is what
    /// <see cref="IsFinished"/> does.
    /// </para>
    /// </remarks>
    public readonly struct TappActivityState : IEquatable<TappActivityState>
    {
        private readonly string _rawValue;

        /// <summary>
        /// The raw state as the system reported it. Never <c>null</c>.
        /// </summary>
        /// <remarks>
        /// Read through the field rather than stored coerced, because a struct's default value skips every
        /// constructor: coercing only in the constructor left <c>default(TappActivityState)</c> holding
        /// <c>null</c> while <c>new TappActivityState("")</c> held <c>""</c>, so the two compared unequal and
        /// a caller could get a <c>null</c> out of a public property that promised otherwise.
        /// </remarks>
        public string RawValue => _rawValue ?? "";

        /// <summary>Wraps a raw state string, including one this build doesn't recognise.</summary>
        public TappActivityState(string rawValue) => _rawValue = rawValue;

        /// <summary>Running, on screen, and updatable.</summary>
        public static readonly TappActivityState Active = new TappActivityState("active");

        /// <summary>
        /// Over, but possibly still on screen — the system keeps an ended activity listed until its dismissal
        /// date passes, up to four hours.
        /// </summary>
        public static readonly TappActivityState Ended = new TappActivityState("ended");

        /// <summary>Off screen for good, by the user's swipe or by the dismissal date passing.</summary>
        public static readonly TappActivityState Dismissed = new TappActivityState("dismissed");

        /// <summary>On screen and live, but showing content past its declared freshness. Not an end.</summary>
        public static readonly TappActivityState Stale = new TappActivityState("stale");

        /// <summary>Requested by a push but not yet running. Not an end — it may still become active.</summary>
        public static readonly TappActivityState Pending = new TappActivityState("pending");

        /// <summary>
        /// Whether the activity is over.
        /// </summary>
        /// <remarks>
        /// A state this build doesn't recognise counts as <b>not</b> finished. The two mistakes aren't
        /// symmetric: treating a live activity as finished stops you updating something the user is still
        /// looking at, while the reverse costs one wasted update.
        /// </remarks>
        public bool IsFinished => Equals(Ended) || Equals(Dismissed);

        /// <inheritdoc/>
        public bool Equals(TappActivityState other) => RawValue == other.RawValue;

        /// <inheritdoc/>
        public override bool Equals(object obj) => obj is TappActivityState other && Equals(other);

        /// <inheritdoc/>
        public override int GetHashCode() => RawValue.GetHashCode();

        /// <inheritdoc/>
        public override string ToString() => RawValue;

        /// <summary>Whether two states are the same.</summary>
        public static bool operator ==(TappActivityState left, TappActivityState right) => left.Equals(right);

        /// <summary>Whether two states differ.</summary>
        public static bool operator !=(TappActivityState left, TappActivityState right) => !left.Equals(right);
    }
}
