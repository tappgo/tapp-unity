#if UNITY_ANDROID && !UNITY_EDITOR
using System;
using UnityEngine;

namespace TappGo.Modules
{
    /// <summary>
    /// The marshalling every call into a Tapp Android bridge class shares.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>For Tapp's own packages, not for host code.</b> Each native module has a bridge class of its own —
    /// <c>com.tappgo.sdk.bridge.TappBridge</c> in core, one more per product — and all of them are
    /// <c>@JvmStatic</c> JSON-in, JSON-out: Unity's <c>AndroidJavaClass</c> carries strings and primitives and
    /// nothing else, and that is the whole reason the Kotlin side looks the way it does.
    /// </para>
    /// <para>
    /// <b>Synchronous members answer on the calling thread; callback members answer on the SDK's own IO
    /// thread</b>, never Unity's. The hop to the main thread is <see cref="TappEnvelope.Deliver{TResult}"/>'s
    /// job, before anything is parsed — the same rule as iOS.
    /// </para>
    /// <para>
    /// <b>Every call runs on Unity's main thread</b>, which is what makes <c>AndroidJavaClass</c> usable without
    /// attaching the thread.
    /// </para>
    /// </remarks>
    public static class TappNative
    {
        /// <summary>The platform this build calls into.</summary>
        public const TappPlatform Platform = TappPlatform.Android;

        private const string CallbackInterface = "com.tappgo.sdk.bridge.TappBridgeCallback";

        /// <summary>
        /// Runs one synchronous bridge call, and turns a JNI failure into an envelope.
        /// </summary>
        /// <remarks>
        /// A native bridge never throws — every member answers with an envelope — so an exception here means
        /// the class was not found at all: the AAR is missing from the build, or R8 stripped what the build
        /// hook's dependency line was meant to keep. That is a packaging failure, reported as
        /// <c>unexpected</c> with the exception's text so the log names it.
        /// </remarks>
        public static string Call(string bridgeClass, string method, params object[] arguments)
        {
            try
            {
                using (var bridge = new AndroidJavaClass(bridgeClass))
                {
                    return bridge.CallStatic<string>(method, arguments) ?? Unanswered;
                }
            }
            catch (Exception exception)
            {
                return Failed(exception);
            }
        }

        /// <summary>
        /// Runs one callback bridge call. The completion is answered exactly once — by the SDK, or by the
        /// failure to reach it. The callback is passed as the member's last argument.
        /// </summary>
        public static void CallAsync(string bridgeClass, string method, Action<string> completion, params object[] arguments)
        {
            try
            {
                var all = new object[arguments.Length + 1];
                Array.Copy(arguments, all, arguments.Length);
                all[arguments.Length] = new BridgeCallback(completion);

                using (var bridge = new AndroidJavaClass(bridgeClass))
                {
                    bridge.CallStatic(method, all);
                }
            }
            catch (Exception exception)
            {
                completion?.Invoke(Failed(exception));
            }
        }

        /// <summary>
        /// The application context, never the activity — for a bridge member that retains what it is given.
        /// </summary>
        /// <remarks>
        /// The SDK retains its context for the life of the process. Unity's activity is recreated on a
        /// configuration change the host did not opt out of, and a retained activity is a leak of the whole
        /// view hierarchy behind it. <c>getApplicationContext()</c> is what the native integration guide passes
        /// too. The caller disposes it.
        /// </remarks>
        public static AndroidJavaObject ApplicationContext()
        {
            using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
            {
                return activity.Call<AndroidJavaObject>("getApplicationContext");
            }
        }

        /// <summary>The envelope for a bridge class or member that could not be reached.</summary>
        public static string Failed(Exception exception)
        {
            var message = exception.Message.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", " ");
            return $"{{\"ok\":false,\"code\":\"unexpected\",\"message\":\"The Tapp Android SDK could not be reached: {message}\"}}";
        }

        private const string Unanswered =
            "{\"ok\":false,\"code\":\"unexpected\",\"message\":\"The native reply was null.\"}";

        /// <summary>
        /// The <c>TappBridgeCallback</c> the SDK calls back on, once.
        /// </summary>
        /// <remarks>
        /// <para>
        /// One instance per call rather than one static proxy: a Java proxy is identity for the SDK's
        /// <c>fun interface</c>, and the completion it carries is the one thing that differs between calls.
        /// The SDK holds a strong reference until it answers, so nothing here can be collected early — the
        /// hazard the iOS body's static delegate guards against is a C function pointer, which JNI does not
        /// hand out.
        /// </para>
        /// <para>
        /// <b>Called on the SDK's IO thread.</b> Nothing here touches a Unity API beyond
        /// <c>Debug.LogException</c>, which is thread-safe; the completion only queues the string for the pump.
        /// </para>
        /// </remarks>
        private sealed class BridgeCallback : AndroidJavaProxy
        {
            private readonly Action<string> _completion;

            internal BridgeCallback(Action<string> completion) : base(CallbackInterface)
            {
                _completion = completion;
            }

            // The Java signature is `void onReply(String envelope)`; the name must match exactly.
            // ReSharper disable once InconsistentNaming
            public void onReply(string envelope)
            {
                try
                {
                    _completion?.Invoke(envelope ?? Unanswered);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }
        }
    }
}
#endif
