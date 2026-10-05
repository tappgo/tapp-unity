#if UNITY_IOS && !UNITY_EDITOR
using System;
using System.Runtime.InteropServices;
using AOT;
using TappGo.Internal;
using UnityEngine;

namespace TappGo.Modules
{
    /// <summary>
    /// The marshalling every call into the iOS shim shares.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>For Tapp's own packages, not for host code.</b> A product declares its own
    /// <c>DllImport("__Internal")</c> entry points — they resolve against its own Swift shim — and borrows
    /// the ownership rules from here, so they are written once.
    /// </para>
    /// <para>
    /// <b>Three ownership rules, and every call obeys all three.</b> A returned <c>char *</c> is ours to free,
    /// so <see cref="Take"/> copies it and releases it in a <c>finally</c>. A callback's <c>char *</c> is valid
    /// only inside the call, so it is copied before returning. A pinned completion is freed by exactly the one
    /// reply it belongs to. Break any of them and you get a leak or a use-after-free that only reproduces on a
    /// device.
    /// </para>
    /// <para>
    /// <b>Replies arrive on whatever thread iOS finished on.</b> Nothing here touches a Unity API beyond
    /// <c>Debug.Log</c>, which is thread-safe; hopping to the main thread is
    /// <see cref="TappEnvelope.Deliver{TResult}"/>'s job, before anything is parsed.
    /// </para>
    /// </remarks>
    public static class TappNative
    {
        /// <summary>The platform this build calls into.</summary>
        public const TappPlatform Platform = TappPlatform.iOS;

        /// <summary>The C signature of the shim's reply callback: <c>void (*)(void *context, const char *json)</c>.</summary>
        public delegate void ReplyCallback(IntPtr context, IntPtr json);

        /// <summary>
        /// The one instance of the callback native code is ever given.
        /// </summary>
        /// <remarks>
        /// A delegate created per call is collectable the moment the call returns, while the native side still
        /// holds its function pointer — a crash on reply, and only for slow calls, which is exactly the timing
        /// that survives testing. One static instance lives as long as the domain.
        /// </remarks>
        public static readonly ReplyCallback Reply = OnReply;

        /// <summary>A request payload as the UTF-8 bytes the shim expects.</summary>
        public static byte[] Encode(string json)
        {
            return Utf8.Encode(json);
        }

        /// <summary>
        /// Hands a completion to native code as an opaque token.
        /// </summary>
        /// <remarks>
        /// A managed object cannot be handed across the ABI, and a static dictionary keyed by an id would need
        /// its own locking and its own leak. A <see cref="GCHandle"/> is the runtime's own answer: it pins the
        /// delegate against collection and converts to an <see cref="IntPtr"/> the shim carries back untouched.
        /// </remarks>
        public static IntPtr Pin(Action<string> completion)
        {
            return GCHandle.ToIntPtr(GCHandle.Alloc(completion));
        }

        /// <summary>
        /// Copies a returned envelope and releases the native allocation.
        /// </summary>
        public static string Take(IntPtr reply)
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
        /// The single reply entry point for every asynchronous call.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Must not throw.</b> An exception crossing back into Swift is undefined behaviour, not a caught
        /// error, so the body is wrapped whole. Note what the wrap is <i>not</i> guarding: the completion
        /// invoked here is always a Tapp package's own, and all it does is queue the reply, so the host's code
        /// cannot throw inside this frame — it runs later, on the main thread, where the pump catches it.
        /// </para>
        /// <para>
        /// The handle is freed here and only here, which is what makes one reply per call the invariant. The
        /// shim calls back exactly once, and never after.
        /// </para>
        /// </remarks>
        [MonoPInvokeCallback(typeof(ReplyCallback))]
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
        /// Stands in for the envelope that could not be allocated.
        /// </summary>
        /// <remarks>
        /// The shim returns a null pointer only when <c>strdup</c> failed, which is out of memory — the one
        /// case where the native side genuinely cannot produce an envelope. Manufacturing one here keeps the
        /// promise that every call answers with a readable result, at the layer where a host can see it.
        /// </remarks>
        private const string Unallocated =
            "{\"ok\":false,\"code\":\"unexpected\",\"message\":\"The native reply could not be allocated.\"}";

        [DllImport("__Internal")]
        private static extern void TappGo_FreeString(IntPtr value);
    }
}
#endif
