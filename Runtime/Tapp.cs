using System;
using TappGo.Internal;
using UnityEngine;

namespace TappGo
{
    /// <summary>
    /// Tapp's Unity API. Everything a game calls lives here.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Live Activities are rendered by a native iOS extension that this package adds to your Xcode project at
    /// build time. Nothing here draws anything — you name an activity and a screen, both configured
    /// server-side, and the SDK resolves the content. That is what lets the design change without a new build.
    /// </para>
    /// <para>
    /// <b>In the Editor every call is a no-op that logs.</b> There is no native framework to talk to, so Play
    /// mode never throws — but it also never renders. Live Activities are verified on a device.
    /// </para>
    /// <para>
    /// <b>Callbacks arrive on the main thread</b>, so it is safe to touch scene objects from them.
    /// </para>
    /// </remarks>
    public static class Tapp
    {
        /// <summary>
        /// The version of the native Tapp framework this package carries.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>A probe, not a feature.</b> Needs no configuration and cannot fail because of anything you did,
        /// which is exactly what makes it worth calling: if it answers on a device, the native framework is
        /// linked, embedded and reachable, so a Live Activity that still does not appear is a configuration
        /// problem rather than a loading one. Those two have identical symptoms — nothing renders — and
        /// completely different fixes, and this is the only call that tells them apart. It is the first thing
        /// to check when an integration isn't working, and the first thing to put in a bug report.
        /// </para>
        /// <para>
        /// <b>Do not branch on the value.</b> It is a string to read, log and report — not a feature gate. The
        /// native version moves independently of this package's own, so code that compares it will break on an
        /// upgrade that was meant to change nothing for you. If some behaviour you need depends on which
        /// native version is present, that is a bug to report rather than a version to test for.
        /// </para>
        /// <para>
        /// In the Editor it reports <c>"editor-stub"</c>, which is the answer to "why is nothing rendering".
        /// </para>
        /// </remarks>
        public static TappResult<string> SdkVersion()
        {
            return BridgeReply.ParseVersion(NativeBridge.SdkVersion());
        }

        /// <summary>
        /// Configures the SDK. Call once, early — or leave <c>Auto configure on launch</c> on and never call it.
        /// </summary>
        /// <remarks>
        /// <para>
        /// A <b>local</b> operation: it records your settings into the shared App Group and asks iOS to reload
        /// the extension. No network happens here. Content is fetched where it is rendered, so nothing is
        /// requested until the user actually has a Live Activity on screen.
        /// </para>
        /// </remarks>
        /// <param name="settings">
        /// The settings to use, or <c>null</c> (the default) to load the asset from <c>Resources</c>.
        /// </param>
        public static TappResult Configure(TappGoSettings settings = null)
        {
            settings = settings ?? TappGoSettings.Load();
            if (settings == null)
            {
                return Local(TappErrorCode.InvalidConfiguration,
                    "No TappGoSettings asset. Open Project Settings → Tapp to create one.");
            }

            var invalid = settings.Validate();
            if (invalid != null)
            {
                return Local(TappErrorCode.InvalidConfiguration, invalid);
            }

            MainThread.Boot();

            var json = new JsonBuilder()
                .Add("appGroupIdentifier", settings.appGroupIdentifier)
                .AddObject("liveActivities", new JsonBuilder()
                    .Add("appID", settings.liveActivityAppId)
                    .Add("pushEnabled", settings.pushEnabled))
                .Build();

            return BridgeReply.Parse(NativeBridge.Configure(json));
        }

        /// <summary>
        /// Records who the current player is.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Returns as soon as the id is written; the session settles in the background. Do not present this as
        /// "sign-in complete" — Tapp surfaces may briefly show guest content while it does.
        /// </para>
        /// <para>
        /// Safe to call on every launch. Passing the id that is already recorded does nothing; passing a
        /// <i>different</i> one ends the previous player's session first.
        /// </para>
        /// </remarks>
        /// <param name="userId">Your own identifier for the player.</param>
        public static TappResult SetUserId(string userId)
        {
            if (string.IsNullOrEmpty(userId))
            {
                return Local(TappErrorCode.InvalidRequest, "userId was null or empty.");
            }

            var json = new JsonBuilder().Add("userID", userId).Build();
            return BridgeReply.Parse(NativeBridge.SetUserId(json));
        }

        /// <summary>
        /// Ends the current player's session and returns the SDK to a guest one.
        /// </summary>
        /// <remarks>
        /// Best-effort remotely, guaranteed locally: logging out with no network still logs out. Idempotent.
        /// </remarks>
        /// <param name="completion">Called on the main thread when the local teardown is done.</param>
        public static void Logout(Action<TappResult> completion = null)
        {
            NativeBridge.Logout(reply => Deliver(completion, reply, BridgeReply.Parse));
        }

        /// <summary>
        /// Offers a URL the app was opened with to the SDK, and says whether Tapp took it.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Call it from <c>Application.deepLinkActivated</c>, and once at startup with
        /// <c>Application.absoluteURL</c> — the first launch of a cold app delivers the URL there, not through
        /// the event.
        /// </para>
        /// <para>
        /// <b>A <c>false</c> value is a success</b>, not a failure: it means the URL isn't Tapp's and you should
        /// route it yourself. Only branch on <see cref="TappResult{TValue}.Ok"/> for actual errors.
        /// </para>
        /// <para>
        /// In the Editor nothing is ever handled, so your own routing is what you test in Play mode.
        /// </para>
        /// </remarks>
        /// <param name="url">The URL, exactly as the OS delivered it.</param>
        public static TappResult<bool> HandleUrl(string url)
        {
            if (string.IsNullOrEmpty(url))
            {
                return Local<bool>(TappErrorCode.InvalidRequest, "url was null or empty.");
            }

            var json = new JsonBuilder().Add("url", url).Build();
            return BridgeReply.ParseHandled(NativeBridge.HandleUrl(json));
        }

        /// <summary>
        /// Starts a Live Activity.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Requires the player to have Live Activities enabled; if they don't, this fails with
        /// <see cref="TappErrorCode.LiveActivitiesUnavailable"/> — expected, not exceptional, so don't surface
        /// it as an error.
        /// </para>
        /// <para>
        /// <b>A card is an id and a screen.</b> Starting that same pair again fails with
        /// <see cref="TappErrorCode.LiveActivityAlreadyRunning"/>, but naming a <i>different</i>
        /// <see cref="TappActivityRequest.EntryId"/> puts a second card up alongside the first — one campaign
        /// can have several of its screens running at once, each with its own clock, content and end. A card
        /// never changes screen: to show another, start it.
        /// </para>
        /// </remarks>
        /// <param name="request">Which activity and which screen.</param>
        /// <param name="completion">
        /// Called on the main thread. On success the value is the system's activity identifier — useful for
        /// logs, but you don't need to keep it: later calls address the activity by its Tapp id.
        /// </param>
        public static void StartLiveActivity(TappActivityRequest request, Action<TappResult<string>> completion = null)
        {
            if (string.IsNullOrEmpty(request.Id) || string.IsNullOrEmpty(request.EntryId))
            {
                DeliverLocal(completion, Local<string>(TappErrorCode.InvalidRequest, "Id and EntryId are both required."));
                return;
            }

            var json = ScreenJson(request.Id, request.EntryId, request.Seconds);
            NativeBridge.StartLiveActivity(json, reply => Deliver(completion, reply, BridgeReply.ParseActivityId));
        }

        /// <summary>
        /// Reloads the card <paramref name="id"/>/<paramref name="entryId"/> names, and optionally restarts its
        /// clock.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>The screen addresses the card; it does not move it.</b> A card's screen is fixed for its whole
        /// life, so this names <i>which</i> card to reload rather than somewhere to send one. To show a
        /// different screen, <see cref="StartLiveActivity"/> it — that puts a second card up alongside this one.
        /// </para>
        /// <para>
        /// <b>A card that isn't running succeeds and does nothing</b>, whether nothing is up for that id or
        /// nothing is on that screen. The player can dismiss a card at any moment, including between your
        /// deciding to update and this call — that race is ordinary, so it is absorbed here. Don't guard against
        /// it with <see cref="ActiveLiveActivities"/>.
        /// </para>
        /// </remarks>
        /// <param name="id">The activity's Tapp id, as started.</param>
        /// <param name="entryId">The screen naming which card to reload, as started. Case is ignored.</param>
        /// <param name="seconds">
        /// A new duration for the card's countdowns, measured from <b>now</b> — so <c>300</c> is "five more
        /// minutes" whenever it arrives, not five minutes from when the card started. <c>null</c> leaves
        /// whatever clock is running exactly as it is, so reloading content never disturbs a countdown.
        /// </param>
        /// <param name="completion">Called on the main thread.</param>
        public static void UpdateLiveActivity(
            string id,
            string entryId,
            int? seconds = null,
            Action<TappResult> completion = null)
        {
            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(entryId))
            {
                DeliverLocal(completion, Local(TappErrorCode.InvalidRequest, "id and entryId are both required."));
                return;
            }

            var json = ScreenJson(id, entryId, seconds);
            NativeBridge.UpdateLiveActivity(json, reply => Deliver(completion, reply, BridgeReply.Parse));
        }

        /// <summary>
        /// Ends the card <paramref name="id"/>/<paramref name="entryId"/> names. Does nothing if there is none.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>The screen is always named</b>, exactly as it is when starting: a campaign can legitimately have
        /// two of its screens up at once, so an "end everything" would silently take down a card you meant to
        /// leave running. To end all of them, call this once per screen — each is independent, so there is no
        /// ordering to get right, and <see cref="ActiveLiveActivities"/> reports the screens to pass.
        /// </para>
        /// <para>
        /// <b>Ending and dismissing are two different moments.</b> The activity stops being live immediately —
        /// nothing can reach it afterwards — while <paramref name="seconds"/> only governs how much longer it
        /// stays on the Lock Screen.
        /// </para>
        /// <para>
        /// <b>That delay is one-shot.</b> iOS caps it at four hours and reports nothing when it does, so an
        /// over-long value silently becomes four hours and <i>cannot be revised</i> — not by you and not from
        /// the server. The only ways back are the player swiping the activity away, or calling this again with
        /// no delay.
        /// </para>
        /// </remarks>
        /// <param name="id">The activity's Tapp id, as started.</param>
        /// <param name="entryId">The screen whose card to end, as started. Case is ignored.</param>
        /// <param name="seconds">
        /// How much longer the ended card stays on screen, in <b>whole seconds from the end</b>, or <c>null</c>
        /// to remove it at once. Zero or a negative value dismisses at once too.
        /// <para>
        /// <b>Not the same <c>seconds</c> as <see cref="TappActivityRequest.Seconds"/></b>, which runs the
        /// design's countdowns and marks content stale while ending nothing. This one only defers the
        /// disappearance of a card that has already ended. They share a name because the native API does.
        /// </para>
        /// </param>
        /// <param name="completion">Called on the main thread.</param>
        public static void EndLiveActivity(
            string id,
            string entryId,
            int? seconds = null,
            Action<TappResult> completion = null)
        {
            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(entryId))
            {
                DeliverLocal(completion, Local(TappErrorCode.InvalidRequest, "id and entryId are both required."));
                return;
            }

            var json = new JsonBuilder()
                .Add("id", id)
                .Add("entryID", entryId)
                .AddInt("seconds", seconds)
                .Build();

            NativeBridge.EndLiveActivity(json, reply => Deliver(completion, reply, BridgeReply.Parse));
        }

        /// <summary>
        /// Every Tapp Live Activity card iOS currently reports.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Read from the system, not from anything this SDK remembers, so it is correct after a relaunch —
        /// activities outlive your process. Use it to reconcile on startup, or to check whether a card is
        /// already up before starting it again.
        /// </para>
        /// <para>
        /// Each entry carries the screen it is on, which is what makes it drivable: that
        /// <see cref="TappActivityInfo.EntryId"/> is what <see cref="UpdateLiveActivity"/> and
        /// <see cref="EndLiveActivity"/> take, so nothing here has to be remembered between launches.
        /// </para>
        /// </remarks>
        public static TappResult<TappActivityInfo[]> ActiveLiveActivities()
        {
            return BridgeReply.ParseActivities(NativeBridge.ActiveLiveActivities());
        }

        // MARK: - Internals

        /// <summary>The request shape a start and an update share — they're addressed identically by design.</summary>
        private static string ScreenJson(string id, string entryId, int? seconds)
        {
            return new JsonBuilder()
                .Add("id", id)
                .Add("entryID", entryId)
                .AddInt("seconds", seconds)
                .Build();
        }

        /// <summary>
        /// A failure decided here rather than natively — a bad argument, or no settings at all.
        /// </summary>
        /// <remarks>
        /// Deliberately carries the same codes as a native failure so a caller has one thing to branch on. It
        /// is also logged, because these are integration mistakes and a silent one gets shipped.
        /// </remarks>
        private static TappResult Local(string code, string message)
        {
            Debug.LogError($"[Tapp] {code}: {message}");
            return new TappResult(false, code, message);
        }

        private static TappResult<TValue> Local<TValue>(string code, string message)
        {
            Debug.LogError($"[Tapp] {code}: {message}");
            return new TappResult<TValue>(false, default, code, message);
        }

        /// <summary>
        /// Hops to the main thread, <i>then</i> reads the reply and hands it to the caller.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Native replies arrive on whatever thread iOS finished the work on, so a callback that touches a
        /// <c>GameObject</c> would be undefined behaviour without this hop. Doing it here rather than asking
        /// every caller to remember is the whole point of a wrapper.
        /// </para>
        /// <para>
        /// <b>Parsing happens after the hop, not before.</b> <see cref="BridgeReply"/> uses
        /// <c>JsonUtility</c> — a <c>UnityEngine</c> API, and not one to call from a thread the engine
        /// doesn't own. Passing the raw envelope through the queue and reading it on the other side costs
        /// nothing and removes the question entirely.
        /// </para>
        /// <para>
        /// The reply is read even when nobody asked for it, so an unreadable one is still logged. A caller
        /// who passed no completion is the case where that log is the only evidence there was — which is why
        /// the parse is its own statement rather than an argument to <c>completion?.Invoke</c>. A
        /// null-conditional call does not evaluate its arguments, so writing it that way skipped the parse
        /// for exactly the callers this paragraph is about.
        /// </para>
        /// </remarks>
        private static void Deliver<TResult>(Action<TResult> completion, string reply, Func<string, TResult> parse)
        {
            MainThread.Post(() =>
            {
                var result = parse(reply);
                completion?.Invoke(result);
            });
        }

        /// <summary>
        /// Hands the caller a result this layer decided, with the same threading as a native one.
        /// </summary>
        /// <remarks>
        /// A rejected argument answers on a later frame rather than inside the call, deliberately: a callback
        /// that sometimes runs before the calling method returns is how re-entrancy bugs get written.
        /// </remarks>
        private static void DeliverLocal<TResult>(Action<TResult> completion, TResult result)
        {
            MainThread.Post(() => completion?.Invoke(result));
        }
    }
}
