using System;
using System.Runtime.InteropServices;
using System.Text;

namespace TappGo.Internal
{
    /// <summary>
    /// Converts between managed strings and the NUL-terminated UTF-8 the native ABI speaks.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Hand-rolled rather than using <c>Marshal.PtrToStringUTF8</c>, which is <b>absent from Unity's
    /// <i>.NET Framework</i> API compatibility level</b>. That would compile here — this repo's host project
    /// uses .NET Standard 2.1 — and then fail to compile in a customer's project that chose the other profile,
    /// which is the worst possible place to find out. One package serves both.
    /// </para>
    /// <para>
    /// The same reasoning rules out relying on default <c>string</c> marshalling: P/Invoke defaults to ANSI,
    /// whose meaning depends on the runtime. The encoding at this boundary is part of the contract, so it is
    /// spelled out rather than inherited.
    /// </para>
    /// </remarks>
    internal static class Utf8
    {
        /// <summary>
        /// Encodes <paramref name="value"/> as NUL-terminated UTF-8 bytes for a <c>const char *</c> parameter.
        /// </summary>
        /// <remarks>
        /// A <c>null</c> string encodes as an empty payload rather than a null pointer: the native side reads
        /// <c>""</c>, fails to decode it, and answers with an <c>invalidRequest</c> envelope naming the field.
        /// A null pointer would have to be special-cased on both sides to say the same thing.
        /// </remarks>
        internal static byte[] Encode(string value)
        {
            var text = value ?? string.Empty;
            var length = Encoding.UTF8.GetByteCount(text);

            // One byte longer than the text, and left at zero — that is the terminator.
            var bytes = new byte[length + 1];
            Encoding.UTF8.GetBytes(text, 0, text.Length, bytes, 0);
            return bytes;
        }

        /// <summary>
        /// Copies a NUL-terminated UTF-8 C string into a managed string.
        /// </summary>
        /// <remarks>
        /// <b>Copies</b>, deliberately: a callback's <c>char *</c> is only valid for the duration of the call,
        /// and a returned one is freed immediately afterwards. Nothing may hold the pointer.
        /// </remarks>
        /// <param name="pointer">The C string, or <c>IntPtr.Zero</c>.</param>
        /// <returns>The decoded string, or <c>null</c> when <paramref name="pointer"/> was zero.</returns>
        internal static string Decode(IntPtr pointer)
        {
            if (pointer == IntPtr.Zero)
            {
                return null;
            }

            var length = 0;
            while (Marshal.ReadByte(pointer, length) != 0)
            {
                length++;
            }

            if (length == 0)
            {
                return string.Empty;
            }

            var bytes = new byte[length];
            Marshal.Copy(pointer, bytes, 0, length);
            return Encoding.UTF8.GetString(bytes);
        }
    }
}
