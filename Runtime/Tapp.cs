using System;
using TappGo.Internal;
using TappGo.Modules;
using UnityEngine;

namespace TappGo
{
    /// <summary>
    /// Tapp's core Unity API: configuration and the player's identity.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Each Tapp product is a package of its own</b> with a class of its own — <c>TappLiveActivities</c> in
    /// <c>com.tapp.go.liveactivities</c>, <c>TappWidgets</c> in <c>com.tapp.go.widgets</c>. Install the ones
    /// you use; this package is what they all share. A product that is not installed is not in your project
    /// at all, so calling it is a compile error rather than something to find out on a device.
    /// </para>
    /// <para>
    /// <b>In the Editor every call is a no-op that logs.</b> There is no native SDK to talk to, so Play mode
    /// never throws — but it also never renders. Tapp surfaces are verified on a device.
    /// </para>
    /// <para>
    /// <b>Callbacks arrive on the main thread</b>, so it is safe to touch scene objects from them.
    /// </para>
    /// </remarks>
    public static class Tapp
    {
        /// <summary>
        /// The version of the native Tapp SDK the app was built with.
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
        /// Configures the SDK. Call once at startup — or after your own login flow, if Tapp should wait for it.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Nothing of Tapp's runs in the game until you call this</b>, exactly as on native iOS and Android:
        /// no native call, no network, no session.
        /// </para>
        /// <para>
        /// A <b>local</b> operation: it records your settings into the shared App Group and asks iOS to reload
        /// the extension. No network happens here. Content is fetched where it is rendered, so nothing is
        /// requested until the user actually has a Tapp surface on screen.
        /// </para>
        /// <para>
        /// The first call that passes validation logs the native version, and warns when it is not the one this
        /// package was built for — see <see cref="SdkVersion"/>.
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

            var platform = TappNative.Platform;
            var invalid = settings.Validate(platform);
            if (invalid != null)
            {
                return Local(TappErrorCode.InvalidConfiguration, invalid);
            }

            ReportNativeVersionOnce();
            MainThread.Boot();

            var configured = BridgeReply.Parse(NativeBridge.Configure(ConfigureJson(settings, platform)));
            if (!configured.Ok)
            {
                return configured;
            }

            foreach (var module in TappModules.All)
            {
                var after = module.AfterConfigure(settings, platform);
                if (after.HasValue && !after.Value.Ok)
                {
                    return after.Value;
                }
            }

            return configured;
        }

        /// <summary>
        /// Whether this process has logged the native version yet. Internal so a test can put it back.
        /// </summary>
        internal static bool NativeVersionReported;

#if UNITY_EDITOR
        /// <summary>
        /// Forgets the report each time the Editor enters Play Mode, so every Play session logs the version.
        /// </summary>
        /// <remarks>
        /// A player is a fresh process on every launch and needs nothing. The Editor is not: with domain reload
        /// turned off in Enter Play Mode Options, statics survive from one Play session to the next, and the line
        /// support asks for first would appear only in the first session after the Editor opened. Editor-only, so
        /// it is not something a player runs at launch.
        /// </remarks>
        [UnityEditor.InitializeOnEnterPlayMode]
        private static void ForgetNativeVersionReport()
        {
            NativeVersionReported = false;
        }
#endif

        /// <summary>
        /// Logs which native binary is loaded, once per process, and warns when it is not the one this package
        /// pins.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The first thing support asks for, and the one question a host cannot answer from its own logs. It
        /// needs no configuration and cannot fail for anything the host did, so it separates "the integration is
        /// wrong" from "the framework never loaded" — and in the Editor it says <c>editor-stub</c>, which is the
        /// answer to "why is nothing rendering".
        /// </para>
        /// <para>
        /// <b>Here, not at launch</b>, because Tapp does nothing until the host configures it: as far as Tapp is
        /// concerned, the first <see cref="Configure"/> is the launch. Once, because a game that reconfigures
        /// after its login flow would repeat a line that cannot have changed. After validation, so a refused
        /// configuration stays one error rather than an error beside a version.
        /// </para>
        /// </remarks>
        private static void ReportNativeVersionOnce()
        {
            // Nothing to report where there is no native SDK. A shipped desktop player would otherwise log a
            // native version for a platform this package does not support. The Editor keeps it: there, the
            // line is the answer to why nothing renders in Play mode.
#if UNITY_IOS || UNITY_ANDROID || UNITY_EDITOR
            if (NativeVersionReported)
            {
                return;
            }

            NativeVersionReported = true;

            var version = SdkVersion();
            Debug.Log(version.Ok
                ? $"[Tapp] native {version.Value}"
                : $"[Tapp] native version unavailable: {version.Code}");

            // The binary is resolved at build time from a version this package pins, so the two agree unless
            // something between them — a host's own dependency on the same package, a Gradle conflict — chose
            // differently. A warning, not a failure: the contract is additive, so a newer binary answers every
            // call this package makes. It just isn't the one this package was tested against.
#if !UNITY_EDITOR
            var expected = NativePin.Expected(TappNative.Platform);
            if (version.Ok && version.Value != expected)
            {
                Debug.LogWarning(
                    $"[Tapp] This package was built for native {expected} and the app resolved {version.Value}. " +
                    "Another dependency on the Tapp SDK is choosing the version; align it, or expect behaviour " +
                    "this package was not tested with.");
            }
#endif
#endif
        }

        /// <summary>
        /// The <c>configure</c> payload for one platform: core's own keys, and each installed product's.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Documented in <c>docs/NATIVE-CONTRACT.md</c>. Core names the App Group on iOS and the log switch on
        /// Android; everything else belongs to a product — an app id included, which only Live Activities have. Each native surface configures itself
        /// from its own keys, and a key for a product that is not installed would be read by nobody — so it
        /// is never written, rather than sent to be ignored.
        /// </para>
        /// <para>
        /// The products' keys follow the App Group and precede the log switch, in the order of their package
        /// names. JSON does not care, and a payload that reads the same on every machine is one a test can pin.
        /// </para>
        /// </remarks>
        internal static string ConfigureJson(TappGoSettings settings, TappPlatform platform)
        {
            var json = new JsonBuilder();
            if (platform == TappPlatform.iOS)
            {
                json.Add("appGroupIdentifier", settings.appGroupIdentifier);
            }

            foreach (var module in TappModules.All)
            {
                module.ContributeConfiguration(json, settings, platform);
            }

            return json.Build();
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
            NativeBridge.Logout(reply => TappEnvelope.Deliver(completion, reply, BridgeReply.Parse));
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

        private static TappResult Local(string code, string message)
        {
            return TappEnvelope.Rejected(code, message);
        }

        private static TappResult<TValue> Local<TValue>(string code, string message)
        {
            return TappEnvelope.Rejected<TValue>(code, message);
        }
    }
}
