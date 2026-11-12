using System;
using TappGo.Modules;

namespace TappGo.Internal
{
    /// <summary>
    /// Turns a bridge envelope into a <see cref="TappResult"/>.
    /// </summary>
    /// <remarks>
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
    internal static class BridgeReply
    {
        /// <summary>A reply with nothing to return.</summary>
        internal static TappResult Parse(string json)
        {
            return TappEnvelope.Parse(json);
        }

        /// <summary><c>{"value":{"version":"1.0.0"}}</c>.</summary>
        internal static TappResult<string> ParseVersion(string json)
        {
            var envelope = TappEnvelope.Decode<VersionEnvelope>(json);
            if (envelope == null)
            {
                return TappEnvelope.Unreadable<string>(json);
            }

            return envelope.ok
                ? new TappResult<string>(true, envelope.value?.version, null, null)
                : new TappResult<string>(false, null, envelope.code, envelope.message);
        }

        /// <summary><c>{"value":{"handled":true}}</c>.</summary>
        internal static TappResult<bool> ParseHandled(string json)
        {
            var envelope = TappEnvelope.Decode<HandledEnvelope>(json);
            if (envelope == null)
            {
                return TappEnvelope.Unreadable<bool>(json);
            }

            return envelope.ok
                ? new TappResult<bool>(true, envelope.value != null && envelope.value.handled, null, null)
                : new TappResult<bool>(false, false, envelope.code, envelope.message);
        }

        // Wire shapes. Field names match docs/NATIVE-CONTRACT.md exactly and are never renamed; `Serializable`
        // classes rather than structs because JsonUtility leaves a missing nested object as null, which is how
        // a malformed `value` is detected instead of silently reading as empty.

        [Serializable]
        private class VersionEnvelope
        {
            public bool ok;
            public string code;
            public string message;
            public VersionDto value;
        }

        [Serializable]
        private class HandledEnvelope
        {
            public bool ok;
            public string code;
            public string message;
            public HandledDto value;
        }

        [Serializable]
        private class VersionDto
        {
            public string version;
        }

        [Serializable]
        private class HandledDto
        {
            public bool handled;
        }
    }
}
