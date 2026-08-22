#if UNITY_IOS && !UNITY_EDITOR
using System;
using System.Runtime.InteropServices;
using AOT;
using UnityEngine;

namespace TappGo.Internal
{
    /// <summary>
    /// The device implementation: calls the C shim in <c>Runtime/Plugins/iOS/TappGoShim.swift</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Same members, same signatures and same envelopes as the Editor stub next door. Which file compiles is
    /// decided entirely by the <c>#if</c> at the top of each, so nothing above this layer knows there are two.
    /// </para>
    /// <para>
    /// <b>Three ownership rules, and every call obeys all three.</b> A returned <c>char *</c> is ours to free,
    /// so it is copied and released in a <c>finally</c>. A callback's <c>char *</c> is valid only inside the
    /// call, so it is copied before returning. A pinned completion is freed by exactly the one reply it belongs
    /// to. Break any of them and you get a leak or a use-after-free that only reproduces on a device.
    /// </para>
    /// <para>
    /// <b>Replies arrive on whatever thread iOS finished on.</b> Nothing here touches a Unity API beyond
    /// <c>Debug.Log</c>, which is thread-safe; hopping to the main thread happens one layer up, before anything
    /// is parsed.
    /// </para>
    /// </remarks>
    internal static class NativeBridge
    {
        // MARK: - Synchronous

        internal static string SdkVersion()
        {
            return Take(TappGo_SDKVersion());
        }

        internal static string Configure(string json)
        {
            return Take(TappGo_Configure(Utf8.Encode(json)));
        }

        internal static string SetUserId(string json)
        {
            return Take(TappGo_SetUserID(Utf8.Encode(json)));
        }

        internal static string HandleUrl(string json)
        {
            return Take(TappGo_HandleURL(Utf8.Encode(json)));
        }

        internal static string ActiveLiveActivities()
        {
            return Take(TappGo_ActiveLiveActivities());
        }

        // MARK: - Asynchronous

        internal static void Logout(Action<string> completion)
        {
            TappGo_Logout(Pin(completion), Reply);
        }

        internal static void StartLiveActivity(string json, Action<string> completion)
        {
            TappGo_StartLiveActivity(Utf8.Encode(json), Pin(completion), Reply);
        }

        internal static void UpdateLiveActivity(string json, Action<string> completion)
        {
            TappGo_UpdateLiveActivity(Utf8.Encode(json), Pin(completion), Reply);
        }

        internal static void EndLiveActivity(string json, Action<string> completion)
        {
            TappGo_EndLiveActivity(Utf8.Encode(json), Pin(completion), Reply);
        }

        // MARK: - Marshalling

        private delegate void ReplyDelegate(IntPtr context, IntPtr json);

        /// <summary>
        /// The one instance of the callback native code is ever given.
        /// </summary>
        /// <remarks>
        /// A delegate created per call is collectable the moment the call returns, while the native side still
        /// holds its function pointer — a crash on reply, and only for slow calls, which is exactly the timing
        /// that survives testing. One static instance lives as long as the domain.
        /// </remarks>
        private static readonly ReplyDelegate Reply = OnReply;

        /// <summary>
        /// Hands a completion to native code as an opaque token.
        /// </summary>
        /// <remarks>
        /// A managed object cannot be handed across the ABI, and a static dictionary keyed by an id would need
        /// its own locking and its own leak. A <see cref="GCHandle"/> is the runtime's own answer: it pins the
        /// delegate against collection and converts to an <see cref="IntPtr"/> the shim carries back untouched.
        /// </remarks>
        private static IntPtr Pin(Action<string> completion)
        {
            return GCHandle.ToIntPtr(GCHandle.Alloc(completion));
        }

        /// <summary>
        /// The single reply entry point for every asynchronous call.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Must not throw.</b> An exception crossing back into Swift is undefined behaviour, not a caught
        /// error, so the body is wrapped whole. Note what the wrap is <i>not</i> guarding: the completion
        /// invoked here is always this package's own, and all it does is queue the reply, so the host's code
        /// cannot throw inside this frame — it runs later, on the main thread, where the pump catches it.
        /// </para>
        /// <para>
        /// The handle is freed here and only here, which is what makes one reply per call the invariant. The
        /// shim calls back exactly once, and never after.
        /// </para>
        /// </remarks>
        [MonoPInvokeCallback(typeof(ReplyDelegate))]
        private static void OnReply(IntPtr context, IntPtr json)
        {
            try
            {
                // Copied first: the pointer dies when this returns, and everything below can take its time.
                var envelope = Utf8.Decode(json) ?? Unallocated;

                if (context == IntPtr.Zero)
                {
                    // No token means no completion to answer, so the reply has nowhere to go. It cannot be
                    // recovered, and a host would see a hang, so say so loudly rather than returning quietly.
                    Debug.LogError($"[Tapp] Native reply with no callback token, dropped: {envelope}");
                    return;
                }

                var handle = GCHandle.FromIntPtr(context);
                var completion = handle.Target as Action<string>;
                handle.Free();

                completion?.Invoke(envelope);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        /// <summary>
        /// Copies a returned envelope and releases the native allocation.
        /// </summary>
        private static string Take(IntPtr reply)
        {
            if (reply == IntPtr.Zero)
            {
                return Unallocated;
            }

            try
            {
                return Utf8.Decode(reply);
            }
            finally
            {
                TappGo_FreeString(reply);
            }
        }

        /// <summary>
        /// Stands in for the envelope that could not be allocated.
        /// </summary>
        /// <remarks>
        /// The shim returns a null pointer only when <c>strdup</c> failed, which is out of memory — the one
        /// case where the native side genuinely cannot produce an envelope. Manufacturing one here keeps the
        /// promise that every call answers with a readable result, at the layer where a host can see it.
        /// </remarks>
        private const string Unallocated =
            "{\"ok\":false,\"code\":\"unexpected\",\"message\":\"The native reply could not be allocated.\"}";

        // MARK: - The shim
        //
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
        private static extern IntPtr TappGo_ActiveLiveActivities();

        [DllImport("__Internal")]
        private static extern void TappGo_Logout(IntPtr context, ReplyDelegate reply);

        [DllImport("__Internal")]
        private static extern void TappGo_StartLiveActivity(byte[] json, IntPtr context, ReplyDelegate reply);

        [DllImport("__Internal")]
        private static extern void TappGo_UpdateLiveActivity(byte[] json, IntPtr context, ReplyDelegate reply);

        [DllImport("__Internal")]
        private static extern void TappGo_EndLiveActivity(byte[] json, IntPtr context, ReplyDelegate reply);

        [DllImport("__Internal")]
        private static extern void TappGo_FreeString(IntPtr value);
    }
}
#endif
