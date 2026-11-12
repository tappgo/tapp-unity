using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace TappGo.Editor
{
    /// <summary>
    /// Reads the <b>PostScript name</b> out of a <c>.ttf</c>/<c>.otf</c> file — the name a Tapp design has to
    /// declare for a bundled font to render, which is very often not the file name.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A bundled font reaches the extension through <c>UIAppFonts</c>, and iOS registers it under the name
    /// stored inside the file. The design's declared font name is then handed to <c>Font.custom(_:)</c>
    /// unchanged — the remap in the native <c>FontNameResolver</c> only exists for fonts the SDK downloaded
    /// and registered itself, and a bundled font is never downloaded. So <c>Inter-Regular.ttf</c> whose
    /// PostScript name is <c>Inter-Regular</c> renders for a design that says <c>Inter-Regular</c> and not for
    /// one that says <c>Inter</c>, with no error either way: SwiftUI falls back to the system font.
    /// </para>
    /// <para>
    /// That is the whole reason this parser exists rather than the settings page listing file names. The file
    /// name is the one thing about a bundled font that doesn't matter.
    /// </para>
    /// <para>
    /// Only the <c>name</c> table is read, and only far enough to find name ID 6. Anything malformed,
    /// truncated or simply not a font reads as "no name" rather than throwing — this runs over whatever a host
    /// dropped in a folder, and a stray file is not an exception.
    /// </para>
    /// </remarks>
    internal static class BundledFontNames
    {
        /// <summary>Name ID 6 in the <c>name</c> table: the PostScript name.</summary>
        private const int PostScriptNameId = 6;

        /// <summary>A font file and the name it will register under.</summary>
        internal readonly struct NamedFont
        {
            internal NamedFont(string fileName, string postScriptName)
            {
                FileName = fileName;
                PostScriptName = postScriptName;
            }

            internal string FileName { get; }

            /// <summary>The PostScript name, or <c>null</c> if the file has none that could be read.</summary>
            internal string PostScriptName { get; }
        }

        /// <summary>
        /// Every <c>.ttf</c>/<c>.otf</c> under <paramref name="folder"/>, at any depth, paired with its
        /// PostScript name, ordered by file name. An unset or missing folder reads as empty.
        /// </summary>
        internal static List<NamedFont> InspectFolder(string folder)
        {
            var found = new List<NamedFont>();
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            {
                return found;
            }

            foreach (var path in Directory.GetFiles(folder, "*", SearchOption.AllDirectories))
            {
                var extension = Path.GetExtension(path);
                if (!extension.Equals(".ttf", StringComparison.OrdinalIgnoreCase) &&
                    !extension.Equals(".otf", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                found.Add(new NamedFont(Path.GetFileName(path), PostScriptNameOf(path)));
            }

            found.Sort((left, right) => string.CompareOrdinal(left.FileName, right.FileName));
            return found;
        }

        /// <summary>
        /// The PostScript name inside the font file at <paramref name="path"/>, or <c>null</c> if it has none
        /// — including when the file is unreadable or isn't a font at all.
        /// </summary>
        internal static string PostScriptNameOf(string path)
        {
            try
            {
                return PostScriptNameIn(File.ReadAllBytes(path));
            }
            catch (IOException)
            {
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
        }

        /// <summary>
        /// The PostScript name in an sfnt blob, or <c>null</c>.
        /// </summary>
        /// <remarks>
        /// Separate from the file read so it can be tested against bytes built in memory. A font fixture
        /// committed to the repo would be a large binary whose contents nobody could review.
        /// </remarks>
        internal static string PostScriptNameIn(byte[] font)
        {
            // A TrueType collection ('ttcf') holds several fonts; the first one's header sits at the first
            // offset in its table. Rare in a game's assets, but cheap to follow rather than report as unnamed.
            var sfnt = 0;
            if (Tag(font, 0) == "ttcf")
            {
                if (!TryUInt32(font, 12, out var first))
                {
                    return null;
                }

                sfnt = (int)first;
            }

            if (!TryUInt16(font, sfnt + 4, out var tableCount))
            {
                return null;
            }

            var records = sfnt + 12;
            for (var index = 0; index < tableCount; index++)
            {
                var record = records + index * 16;
                if (Tag(font, record) != "name")
                {
                    continue;
                }

                return TryUInt32(font, record + 8, out var offset) ? PostScriptNameInTable(font, (int)offset) : null;
            }

            return null;
        }

        /// <summary>
        /// Scans one <c>name</c> table for name ID 6, preferring the platform whose encoding is least
        /// ambiguous.
        /// </summary>
        private static string PostScriptNameInTable(byte[] font, int table)
        {
            if (!TryUInt16(font, table + 2, out var count) || !TryUInt16(font, table + 4, out var storage))
            {
                return null;
            }

            string best = null;
            var bestPlatform = -1;

            for (var index = 0; index < count; index++)
            {
                var record = table + 6 + index * 12;
                if (!TryUInt16(font, record, out var platform) ||
                    !TryUInt16(font, record + 6, out var nameId) ||
                    !TryUInt16(font, record + 8, out var length) ||
                    !TryUInt16(font, record + 10, out var offset) ||
                    nameId != PostScriptNameId)
                {
                    continue;
                }

                var start = table + storage + offset;
                if (start < 0 || length == 0 || start + length > font.Length)
                {
                    continue;
                }

                // Platform 3 (Windows) and 0 (Unicode) store UTF-16BE; platform 1 (Macintosh) stores one byte
                // per character. A PostScript name is ASCII by specification, so either decodes exactly — the
                // preference below is only about picking one deterministically when a font carries both.
                var text = (platform == 3 || platform == 0
                        ? Encoding.BigEndianUnicode.GetString(font, start, length)
                        : Encoding.ASCII.GetString(font, start, length))
                    .Trim();

                var rank = platform == 3 ? 2 : platform == 0 ? 1 : 0;
                if (text.Length > 0 && rank > bestPlatform)
                {
                    best = text;
                    bestPlatform = rank;
                }
            }

            return best;
        }

        /// <summary>The four-byte tag at <paramref name="offset"/>, or <c>null</c> past the end.</summary>
        private static string Tag(byte[] font, int offset) =>
            offset < 0 || offset + 4 > font.Length ? null : Encoding.ASCII.GetString(font, offset, 4);

        private static bool TryUInt16(byte[] font, int offset, out int value)
        {
            value = 0;
            if (offset < 0 || offset + 2 > font.Length)
            {
                return false;
            }

            value = (font[offset] << 8) | font[offset + 1];
            return true;
        }

        private static bool TryUInt32(byte[] font, int offset, out uint value)
        {
            value = 0;
            if (offset < 0 || offset + 4 > font.Length)
            {
                return false;
            }

            value = ((uint)font[offset] << 24) | ((uint)font[offset + 1] << 16)
                | ((uint)font[offset + 2] << 8) | font[offset + 3];
            return true;
        }
    }
}
