using System;
using TappGo.Internal;
using UnityEngine;

namespace TappGo.Modules
{
    /// <summary>
    /// Reads a bridge envelope into a <see cref="TappResult"/>, and delivers it the way every Tapp call does.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>For Tapp's own packages, not for host code.</b> Every product answers through the same
    /// <c>{ok, value}</c> / <c>{ok, code, message}</c> envelope, so the reading of it lives once, here.
    /// </para>
    /// <para>
    /// Reading uses <c>JsonUtility</c>, unlike writing (see <see cref="JsonBuilder"/>): a missing key leaves
    /// the field at its default and an unknown key is ignored, which is exactly the tolerance the
    /// additive-only contract needs. A newer binary that adds a field to a reply must not break this build.
    /// </para>
    /// <para>
    /// <b>An unparseable reply is still a result</b>, never an exception. Whatever went wrong, the caller's
    /// callback fires — a silent drop would look like a hang, which is far harder to diagnose.
    /// </para>
    /// </remarks>
    public static class TappEnvelope
    {
        /// <summary>A reply with nothing to return.</summary>
        public static TappResult Parse(string json)
        {
            var envelope = Decode<Envelope>(json);
            return envelope == null
                ? Unreadable(json)
                : new TappResult(envelope.ok, envelope.code, envelope.message);
        }

        /// <summary>
        /// Reads <paramref name="json"/> as <typeparamref name="TEnvelope"/>, or <c>null</c> when it can't be.
        /// </summary>
        /// <remarks>
        /// The type is a <c>[Serializable]</c> class whose field names match <c>docs/NATIVE-CONTRACT.md</c>
        /// exactly. A class rather than a struct, because <c>JsonUtility</c> leaves a missing nested object as
        /// null, which is how a malformed <c>value</c> is detected instead of silently reading as empty.
        /// </remarks>
        public static TEnvelope Decode<TEnvelope>(string json) where TEnvelope : class
        {
            if (string.IsNullOrEmpty(json))
            {
                return null;
            }

            try
            {
                return JsonUtility.FromJson<TEnvelope>(json);
            }
            catch (Exception)
            {
                // Silent here, logged once by Unreadable with the raw reply attached. Two log lines for one
                // failure is noise, and "Invalid value" says far less than the payload that caused it.
                return null;
            }
        }

        /// <summary>
        /// The answer when the native side said something this build can't read.
        /// </summary>
        /// <remarks>
        /// Reported as <see cref="TappErrorCode.Unexpected"/> because that is what it is — a defect in Tapp,
        /// not in the host's integration. The raw reply goes to the log, since it is the only evidence of what
        /// actually came back.
        /// </remarks>
        public static TappResult Unreadable(string json)
        {
            Debug.LogError($"[Tapp] Unreadable native reply: {json}");
            return new TappResult(false, TappErrorCode.Unexpected, "The native reply could not be read.");
        }

        /// <inheritdoc cref="Unreadable(string)"/>
        public static TappResult<TValue> Unreadable<TValue>(string json)
        {
            Debug.LogError($"[Tapp] Unreadable native reply: {json}");
            return new TappResult<TValue>(false, default, TappErrorCode.Unexpected,
                "The native reply could not be read.");
        }

        /// <summary>A success carrying <paramref name="value"/>.</summary>
        public static TappResult<TValue> Success<TValue>(TValue value)
        {
            return new TappResult<TValue>(true, value, null, null);
        }

        /// <summary>A failure exactly as the native side reported it.</summary>
        public static TappResult Failure(string code, string message)
        {
            return new TappResult(false, code, message);
        }

        /// <inheritdoc cref="Failure(string, string)"/>
        public static TappResult<TValue> Failure<TValue>(string code, string message)
        {
            return new TappResult<TValue>(false, default, code, message);
        }

        /// <summary>A yes-or-no answer: the flag on success, <c>false</c> with the code on failure.</summary>
        public static TappResult<bool> Flag(bool ok, bool value, string code, string message)
        {
            return ok ? Success(value) : Failure<bool>(code, message);
        }

        /// <summary>
        /// A failure decided in C# rather than natively — a bad argument, or no settings at all.
        /// </summary>
        /// <remarks>
        /// Deliberately carries the same codes as a native failure so a caller has one thing to branch on. It
        /// is also logged, because these are integration mistakes and a silent one gets shipped.
        /// </remarks>
        public static TappResult Rejected(string code, string message)
        {
            Debug.LogError($"[Tapp] {code}: {message}");
            return new TappResult(false, code, message);
        }

        /// <inheritdoc cref="Rejected(string, string)"/>
        public static TappResult<TValue> Rejected<TValue>(string code, string message)
        {
            Debug.LogError($"[Tapp] {code}: {message}");
            return new TappResult<TValue>(false, default, code, message);
        }

        /// <summary>
        /// Makes sure the main-thread pump <see cref="Deliver{TResult}"/> posts to exists.
        /// </summary>
        /// <remarks>
        /// It is created before the first scene loads, so a game never needs this. A play-mode test does: the
        /// test runner swaps scenes underneath it. Main thread only, and a no-op once the pump is there.
        /// </remarks>
        public static void StartPump()
        {
            MainThread.Boot();
        }

        /// <summary>
        /// Hops to the main thread, <i>then</i> reads the reply and hands it to the caller.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Native replies arrive on whatever thread the OS finished the work on, so a callback that touches a
        /// <c>GameObject</c> would be undefined behaviour without this hop. Doing it here rather than asking
        /// every caller to remember is the whole point of a wrapper.
        /// </para>
        /// <para>
        /// <b>Parsing happens after the hop, not before.</b> Reading uses <c>JsonUtility</c> — a
        /// <c>UnityEngine</c> API, and not one to call from a thread the engine doesn't own. Passing the raw
        /// envelope through the queue and reading it on the other side costs nothing and removes the question
        /// entirely.
        /// </para>
        /// <para>
        /// The reply is read even when nobody asked for it, so an unreadable one is still logged. A caller
        /// who passed no completion is the case where that log is the only evidence there was — which is why
        /// the parse is its own statement rather than an argument to <c>completion?.Invoke</c>. A
        /// null-conditional call does not evaluate its arguments, so writing it that way skipped the parse
        /// for exactly the callers this paragraph is about.
        /// </para>
        /// </remarks>
        public static void Deliver<TResult>(Action<TResult> completion, string reply, Func<string, TResult> parse)
        {
            MainThread.Post(() =>
            {
                var result = parse(reply);
                completion?.Invoke(result);
            });
        }

        /// <summary>
        /// Hands the caller a result decided in C#, with the same threading as a native one.
        /// </summary>
        /// <remarks>
        /// A rejected argument answers on a later frame rather than inside the call, deliberately: a callback
        /// that sometimes runs before the calling method returns is how re-entrancy bugs get written.
        /// </remarks>
        public static void DeliverLocal<TResult>(Action<TResult> completion, TResult result)
        {
            MainThread.Post(() => completion?.Invoke(result));
        }

        [Serializable]
        private class Envelope
        {
            public bool ok;
            public string code;
            public string message;
        }
    }
}
