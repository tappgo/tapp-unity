#if UNITY_ANDROID && !UNITY_EDITOR
using System;
using TappGo.Modules;

namespace TappGo.Internal
{
    /// <summary>
    /// The Android implementation of core's calls: <c>com.tappgo.sdk.bridge.TappBridge</c> through JNI.
    /// </summary>
    /// <remarks>
    /// Same members, same signatures and same envelopes as the iOS body and the Editor stub. The JNI rules every
    /// call obeys live in <see cref="TappNative"/>, which the product packages share.
    /// </remarks>
    internal static class NativeBridge
    {
        private const string BridgeClass = "com.tappgo.sdk.bridge.TappBridge";

        internal static string SdkVersion()
        {
            return TappNative.Call(BridgeClass, "sdkVersion");
        }

        internal static string Configure(string json)
        {
            try
            {
                using (var context = TappNative.ApplicationContext())
                {
                    return TappNative.Call(BridgeClass, "configure", context, json);
                }
            }
            catch (Exception exception)
            {
                return TappNative.Failed(exception);
            }
        }

        internal static string SetUserId(string json)
        {
            return TappNative.Call(BridgeClass, "setUserID", json);
        }

        /// <summary>
        /// Android has no URL entry point: a widget's link arrives through the Widgets package's pending link,
        /// and any other URL is the host's own. "Not mine" is the honest and the safe answer.
        /// </summary>
        internal static string HandleUrl(string json)
        {
            return "{\"ok\":true,\"value\":{\"handled\":false}}";
        }

        internal static void Logout(Action<string> completion)
        {
            TappNative.CallAsync(BridgeClass, "logout", completion);
        }
    }
}
#endif
