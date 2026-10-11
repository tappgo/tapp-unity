#if UNITY_IOS && !UNITY_EDITOR
using System;
using System.Runtime.InteropServices;
using TappGo.Modules;

namespace TappGo.Internal
{
    /// <summary>
    /// The device implementation of core's calls: the C shim in <c>Runtime/Plugins/iOS/TappGoShim.swift</c>.
    /// </summary>
    /// <remarks>
    /// Same members, same signatures and same envelopes as the Editor stub next door. Which file compiles is
    /// decided entirely by the <c>#if</c> at the top of each, so nothing above this layer knows there are two.
    /// The ownership rules every call obeys live in <see cref="TappNative"/>, which the product packages share.
    /// </remarks>
    internal static class NativeBridge
    {
        internal static string SdkVersion()
        {
            return TappNative.Take(TappGo_SDKVersion());
        }

        internal static string Configure(string json)
        {
            return TappNative.Take(TappGo_Configure(TappNative.Encode(json)));
        }

        internal static string SetUserId(string json)
        {
            return TappNative.Take(TappGo_SetUserID(TappNative.Encode(json)));
        }

        internal static string HandleUrl(string json)
        {
            return TappNative.Take(TappGo_HandleURL(TappNative.Encode(json)));
        }

        internal static void Logout(Action<string> completion)
        {
            TappGo_Logout(TappNative.Pin(completion), TappNative.Reply);
        }

        // Names match the C symbols exactly, so none of these needs an EntryPoint and a typo cannot silently
        // bind to the wrong function. `byte[]` rather than `string` because the encoding is UTF-8 by contract,
        // not by whatever the runtime's default happens to be — see Utf8.

        [DllImport("__Internal")]
        private static extern IntPtr TappGo_SDKVersion();

        [DllImport("__Internal")]
        private static extern IntPtr TappGo_Configure(byte[] json);

        [DllImport("__Internal")]
        private static extern IntPtr TappGo_SetUserID(byte[] json);

        [DllImport("__Internal")]
        private static extern IntPtr TappGo_HandleURL(byte[] json);

        [DllImport("__Internal")]
        private static extern void TappGo_Logout(IntPtr context, TappNative.ReplyCallback reply);
    }
}
#endif
