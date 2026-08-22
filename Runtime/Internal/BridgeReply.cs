using System;
using UnityEngine;

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
            var envelope = Decode<Envelope>(json);
            return envelope == null
                ? Unreadable(json)
                : new TappResult(envelope.ok, envelope.code, envelope.message);
        }

        /// <summary><c>{"value":{"version":"1.0.0"}}</c>.</summary>
        internal static TappResult<string> ParseVersion(string json)
        {
            var envelope = Decode<VersionEnvelope>(json);
            if (envelope == null)
            {
                return Unreadable<string>(json);
            }

            return envelope.ok
                ? new TappResult<string>(true, envelope.value?.version, null, null)
                : new TappResult<string>(false, null, envelope.code, envelope.message);
        }

        /// <summary><c>{"value":{"handled":true}}</c>.</summary>
        internal static TappResult<bool> ParseHandled(string json)
        {
            var envelope = Decode<HandledEnvelope>(json);
            if (envelope == null)
            {
                return Unreadable<bool>(json);
            }

            return envelope.ok
                ? new TappResult<bool>(true, envelope.value != null && envelope.value.handled, null, null)
                : new TappResult<bool>(false, false, envelope.code, envelope.message);
        }

        /// <summary><c>{"value":{"activityID":"…"}}</c>.</summary>
        internal static TappResult<string> ParseActivityId(string json)
        {
            var envelope = Decode<ActivityStartedEnvelope>(json);
            if (envelope == null)
            {
                return Unreadable<string>(json);
            }

            return envelope.ok
                ? new TappResult<string>(true, envelope.value?.activityID, null, null)
                : new TappResult<string>(false, null, envelope.code, envelope.message);
        }

        /// <summary>
        /// <c>{"value":{"activities":[{"id":…,"activityID":…,"entryID":…,"state":…,"updateToken":…}]}}</c>.
        /// </summary>
        internal static TappResult<TappActivityInfo[]> ParseActivities(string json)
        {
            var envelope = Decode<ActivitiesEnvelope>(json);
            if (envelope == null)
            {
                return Unreadable<TappActivityInfo[]>(json);
            }

            if (!envelope.ok)
            {
                return new TappResult<TappActivityInfo[]>(false, null, envelope.code, envelope.message);
            }

            var reported = envelope.value?.activities ?? Array.Empty<ActivityDto>();
            var activities = new TappActivityInfo[reported.Length];
            for (var index = 0; index < reported.Length; index++)
            {
                var dto = reported[index];
                // `state` is kept as whatever the system said, including a value this build has never heard
                // of — see TappActivityState. Mapping it to an enum here is what would lose it.
                //
                // `entryID` is absent for a card that names no screen, and JsonUtility leaves a missing string
                // as "" rather than null — normalised here, because the difference is "unaddressable card"
                // versus "screen the empty string", and every caller would otherwise have to test for both.
                activities[index] = new TappActivityInfo(
                    dto.id,
                    dto.activityID,
                    string.IsNullOrEmpty(dto.entryID) ? null : dto.entryID,
                    new TappActivityState(dto.state),
                    // Normalised for the same reason as `entryID`: absent means the SDK holds no token for this
                    // card — the ordinary state in the second after a start, and permanently once it has ended —
                    // and every caller would otherwise have to test for both null and "".
                    string.IsNullOrEmpty(dto.updateToken) ? null : dto.updateToken);
            }

            return new TappResult<TappActivityInfo[]>(true, activities, null, null);
        }

        // MARK: - Internals

        private static TEnvelope Decode<TEnvelope>(string json) where TEnvelope : class
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
        private static TappResult Unreadable(string json)
        {
            Debug.LogError($"[Tapp] Unreadable native reply: {json}");
            return new TappResult(false, TappErrorCode.Unexpected, "The native reply could not be read.");
        }

        private static TappResult<TValue> Unreadable<TValue>(string json)
        {
            Debug.LogError($"[Tapp] Unreadable native reply: {json}");
            return new TappResult<TValue>(false, default, TappErrorCode.Unexpected,
                "The native reply could not be read.");
        }

        // Wire shapes. Field names match docs/NATIVE-CONTRACT.md exactly and are never renamed; `Serializable`
        // classes rather than structs because JsonUtility leaves a missing nested object as null, which is how
        // a malformed `value` is detected instead of silently reading as empty.
        [Serializable]
        private class Envelope
        {
            public bool ok;
            public string code;
            public string message;
        }

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
        private class ActivityStartedEnvelope
        {
            public bool ok;
            public string code;
            public string message;
            public ActivityStartedDto value;
        }

        [Serializable]
        private class ActivitiesEnvelope
        {
            public bool ok;
            public string code;
            public string message;
            public ActivitiesDto value;
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

        [Serializable]
        private class ActivityStartedDto
        {
            public string activityID;
        }

        /// <summary>
        /// The list reply's <c>value</c> is an <b>object wrapping</b> the array, not the array itself.
        /// </summary>
        /// <remarks>
        /// Deliberate on the native side — a count, a <c>truncated</c> flag or a cursor can be added beside
        /// <c>activities</c> later, where a bare array at the top of <c>value</c> could only grow by breaking
        /// every wrapper reading it. Getting this wrong is silent: <c>JsonUtility</c> handed an object where
        /// it expects an array leaves the field <c>null</c> without an error, so the reply stays
        /// <c>ok</c> and every running activity disappears.
        /// </remarks>
        [Serializable]
        private class ActivitiesDto
        {
            public ActivityDto[] activities;
        }

        [Serializable]
        private class ActivityDto
        {
            public string id;
            public string activityID;
            public string entryID;
            public string state;
            public string updateToken;
        }

    }
}
