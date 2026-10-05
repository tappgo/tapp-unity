#if UNITY_EDITOR || (!UNITY_IOS && !UNITY_ANDROID)
using System;
using TappGo.Modules;

namespace TappGo.Internal
{
    /// <summary>
    /// Core's calls in the Editor and on every platform with no native SDK behind it: logs what would have been
    /// sent, and answers. See <see cref="TappNative"/> for what a stub may and may not claim.
    /// </summary>
    internal static class NativeBridge
    {
        internal static string SdkVersion()
        {
            TappNative.Log("SdkVersion");
            return $"{{\"ok\":true,\"value\":{{\"version\":\"{TappNative.StubValue}\"}}}}";
        }

        internal static string Configure(string json)
        {
            TappNative.Log("Configure", json);
            return TappNative.Ok;
        }

        internal static string SetUserId(string json)
        {
            TappNative.Log("SetUserId", json);
            return TappNative.Ok;
        }

        internal static void Logout(Action<string> completion)
        {
            TappNative.Log("Logout");
            completion?.Invoke(TappNative.Ok);
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
            TappNative.Log("HandleUrl", json);
            return "{\"ok\":true,\"value\":{\"handled\":false}}";
        }
    }
}
#endif
