using System;
using System.Text;

namespace TappGo.Modules
{
    /// <summary>
    /// Builds a bridge request payload.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Hand-rolled rather than <c>JsonUtility</c> for one reason: <b>omission</b>. The bridge treats a missing
    /// key as "not specified" and maps it to the native parameter's default, but
    /// <c>JsonUtility.ToJson</c> always writes every field — so an unset <c>seconds</c> would go out as
    /// <c>"seconds":0</c>, and zero is a value the bridge acts on rather than a way of saying nothing:
    /// countdowns already over on a start, dismiss immediately on an end. Reading replies has no such problem,
    /// so that side does use <c>JsonUtility</c> (see <see cref="BridgeReply"/>).
    /// </para>
    /// <para>
    /// Not a general JSON writer, and shouldn't become one: it emits the handful of shapes in
    /// <c>docs/NATIVE-CONTRACT.md</c> and nothing else. A third-party JSON dependency would be a package the
    /// host has to resolve — and a version conflict with whatever they already use.
    /// </para>
    /// </remarks>
    public sealed class JsonBuilder
    {
        private readonly StringBuilder _out = new StringBuilder("{");

        /// <summary>Adds a string, or nothing at all if it is null.</summary>
        public JsonBuilder Add(string key, string value)
        {
            if (value == null)
            {
                return this;
            }

            Separate();
            _out.Append(Quote(key)).Append(':').Append(Quote(value));
            return this;
        }

        /// <summary>Adds a bool.</summary>
        public JsonBuilder Add(string key, bool value)
        {
            Separate();
            _out.Append(Quote(key)).Append(':').Append(value ? "true" : "false");
            return this;
        }

        /// <summary>
        /// Adds a whole number, or nothing at all if it has no value.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Absence is the only way to say "not specified" for a duration: <c>0</c> is a value the bridge
        /// accepts and acts on — countdowns at zero and content stale at once on a start, dismissed at once on
        /// an end — so writing one for an unset field would silently change the call.
        /// </para>
        /// <para>
        /// Named rather than an <c>Add</c> overload because <c>Add(key, null)</c> would be ambiguous between a
        /// null string and an absent number, and the compiler couldn't pick.
        /// </para>
        /// </remarks>
        public JsonBuilder AddInt(string key, int? value)
        {
            if (!value.HasValue)
            {
                return this;
            }

            Separate();
            _out.Append(Quote(key)).Append(':')
                .Append(value.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
            return this;
        }

        /// <summary>
        /// Adds an array of strings, or nothing at all if it is null or empty.
        /// </summary>
        /// <remarks>
        /// Empty is omitted for the same reason a null string is: the bridge reads a missing key as "not
        /// specified" and applies the native default, which for every list on this wire is the empty list. A
        /// null element inside the array is skipped rather than written as <c>null</c>.
        /// </remarks>
        public JsonBuilder AddStrings(string key, System.Collections.Generic.IEnumerable<string> values)
        {
            if (values == null)
            {
                return this;
            }

            var array = new StringBuilder();
            foreach (var value in values)
            {
                if (value == null)
                {
                    continue;
                }

                if (array.Length > 0)
                {
                    array.Append(',');
                }

                array.Append(Quote(value));
            }

            if (array.Length == 0)
            {
                return this;
            }

            Separate();
            _out.Append(Quote(key)).Append(":[").Append(array).Append(']');
            return this;
        }

        /// <summary>Nests another builder's object under <paramref name="key"/>.</summary>
        public JsonBuilder AddObject(string key, JsonBuilder value)
        {
            if (value == null)
            {
                return this;
            }

            Separate();
            _out.Append(Quote(key)).Append(':').Append(value.Build());
            return this;
        }

        /// <summary>The finished object. Safe to call more than once.</summary>
        public string Build()
        {
            return _out.ToString() + "}";
        }

        private void Separate()
        {
            if (_out.Length > 1)
            {
                _out.Append(',');
            }
        }

        /// <summary>
        /// Quotes and escapes a string per RFC 8259.
        /// </summary>
        /// <remarks>
        /// Worth doing properly even though most values here are ids: an App Group or a player id containing a
        /// quote or a backslash would otherwise produce a payload the bridge rejects as
        /// <c>invalidRequest</c>, which points the blame at the wrong side.
        /// </remarks>
        private static string Quote(string value)
        {
            var quoted = new StringBuilder(value.Length + 2);
            quoted.Append('"');

            foreach (var character in value)
            {
                switch (character)
                {
                    case '"':  quoted.Append("\\\""); break;
                    case '\\': quoted.Append("\\\\"); break;
                    case '\n': quoted.Append("\\n"); break;
                    case '\r': quoted.Append("\\r"); break;
                    case '\t': quoted.Append("\\t"); break;
                    case '\b': quoted.Append("\\b"); break;
                    case '\f': quoted.Append("\\f"); break;
                    default:
                        if (character < ' ')
                        {
                            quoted.Append("\\u").Append(((int)character).ToString("x4"));
                        }
                        else
                        {
                            quoted.Append(character);
                        }
                        break;
                }
            }

            quoted.Append('"');
            return quoted.ToString();
        }
    }
}
