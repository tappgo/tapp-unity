using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace TappGo.Editor
{
    /// <summary>
    /// Reads a set of font file names the way the native SDK will once they are bundled: groups them into
    /// animations by the name their frames share, and reports the files that will never take part in one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The Font Animations Folder is a <b>base</b> folder — one subfolder per animation is the convention it
    /// is documented with, and how <c>TestProject~</c> is laid out:
    /// </para>
    /// <code>
    /// Assets/TappAssets/FontAnimations/SlotFont/SlotFont_0.ttf …
    /// Assets/TappAssets/FontAnimations/CoinSpin/CoinSpin_0.ttf …
    /// </code>
    /// <para>
    /// Those subfolders are for the host's own tidiness and nothing else. <b>An animation is identified by the
    /// name its frame files share, never by the folder holding them</b> — Xcode flattens every folder into the
    /// bundle root, so by the time the extension runs there are no folders left to group by, and
    /// <c>BundledFrameFonts</c> in the native SDK matches <c>&lt;name&gt;_&lt;n&gt;</c> against file names.
    /// This exists so that is visible in <b>Project Settings → Tapp</b> before a build rather than discovered
    /// from a Live Activity that renders a still frame.
    /// </para>
    /// <para>
    /// The matching here mirrors that native rule deliberately, underscore-optional included. If the two ever
    /// disagree, this one is wrong: what the device does is the answer.
    /// </para>
    /// </remarks>
    internal static class FontAnimationInventory
    {
        /// <summary>
        /// One animation's frames — the file name before the number, and the frames carrying it.
        /// </summary>
        internal sealed class FontAnimation
        {
            internal FontAnimation(
                string name,
                IReadOnlyList<string> frames,
                IReadOnlyList<string> shadowed,
                IReadOnlyList<int> missingNumbers)
            {
                Name = name;
                Frames = frames;
                Shadowed = shadowed;
                MissingNumbers = missingNumbers;
            }

            /// <summary>
            /// The shared name its files start with — <c>"SlotFont"</c> for <c>SlotFont_0.ttf</c>. This is the
            /// name a Tapp design refers to the animation by, which is why the settings page prints it.
            /// </summary>
            internal string Name { get; }

            /// <summary>The frame file names, ordered by frame number.</summary>
            internal IReadOnlyList<string> Frames { get; }

            /// <summary>
            /// Frame files a second file already claims the number of. The native SDK keeps one of them
            /// deterministically, so these are bundled, registered, and never drawn.
            /// </summary>
            internal IReadOnlyList<string> Shadowed { get; }

            /// <summary>
            /// Numbers with no file, between the lowest and highest that do have one.
            /// </summary>
            /// <remarks>
            /// Only the holes in between: an animation numbered from 1, or from 100, made that choice, and
            /// only a set that starts somewhere and then skips looks like files that went missing. Nothing
            /// fails either way — the native SDK renders the frames it finds in order, which is why an export
            /// that dropped four of them shortens the animation and says nothing at all.
            /// </remarks>
            internal IReadOnlyList<int> MissingNumbers { get; }
        }

        /// <summary>What a folder of font files amounts to once the extension is looking at it.</summary>
        internal sealed class Report
        {
            internal Report(IReadOnlyList<FontAnimation> animations, IReadOnlyList<string> unnumbered)
            {
                Animations = animations;
                Unnumbered = unnumbered;
            }

            /// <summary>The animations found, by name.</summary>
            internal IReadOnlyList<FontAnimation> Animations { get; }

            /// <summary>
            /// Font files whose name is not <c>&lt;name&gt;_&lt;n&gt;</c>. They are still bundled and still
            /// registered — they are simply ordinary fonts, and no animation will ever include them.
            /// </summary>
            internal IReadOnlyList<string> Unnumbered { get; }

            internal bool IsEmpty => Animations.Count == 0 && Unnumbered.Count == 0;
        }

        /// <summary>
        /// Splits a name into the part before the trailing number and the number itself. The underscore is
        /// optional and the name is non-greedy, both matching <c>BundledFrameFonts</c>: <c>SlotFont0</c> works,
        /// and <c>SlotFont_10</c> is frame 10 of <c>SlotFont</c> rather than frame 0 of <c>SlotFont_1</c>.
        /// </summary>
        private static readonly Regex FrameName = new Regex(@"^(.+?)_?([0-9]+)$", RegexOptions.Compiled);

        private static readonly HashSet<string> FontExtensions =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".ttf", ".otf" };

        /// <summary>
        /// Inspects every <c>.ttf</c>/<c>.otf</c> under <paramref name="folder"/>, at any depth — the same set
        /// <see cref="FontFileStaging"/> would stage from it. An unset or missing folder reports nothing, since
        /// the setting is optional and the build hook is what refuses a path that was typed and is wrong.
        /// </summary>
        internal static Report InspectFolder(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            {
                return Inspect(Array.Empty<string>());
            }

            return Inspect(Directory
                .GetFiles(folder, "*", SearchOption.AllDirectories)
                .Select(Path.GetFileName)
                .Where(name => FontExtensions.Contains(Path.GetExtension(name))));
        }

        /// <summary>
        /// Groups font file names into animations. Takes names rather than paths because names are all that
        /// survives into the bundle — the build hook passes the files it has already staged, which is exactly
        /// what the extension will see.
        /// </summary>
        internal static Report Inspect(IEnumerable<string> fontFileNames)
        {
            var framesByName = new Dictionary<string, List<(int Index, string File)>>(StringComparer.Ordinal);
            var unnumbered = new List<string>();

            foreach (var file in fontFileNames)
            {
                var stem = Path.GetFileNameWithoutExtension(file);
                var match = FrameName.Match(stem);
                if (!match.Success || !int.TryParse(match.Groups[2].Value, out var index))
                {
                    unnumbered.Add(file);
                    continue;
                }

                var name = match.Groups[1].Value;
                if (!framesByName.TryGetValue(name, out var frames))
                {
                    frames = new List<(int, string)>();
                    framesByName[name] = frames;
                }

                frames.Add((index, file));
            }

            var animations = new List<FontAnimation>();
            foreach (var pair in framesByName.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                var kept = new List<string>();
                var shadowed = new List<string>();
                var claimed = new HashSet<int>();

                // Ordered by number, then by file name — and one file per number, the tie broken the way
                // BundledFrameFonts breaks it, so what is listed here is what will actually be drawn. Ordinal
                // is the point of the second sort: it is what Swift's own String comparison does to the file
                // names, and it puts SlotFont0 ahead of SlotFont_0 ('0' is 48, '_' is 95).
                foreach (var frame in pair.Value
                             .OrderBy(frame => frame.Index)
                             .ThenBy(frame => frame.File, StringComparer.Ordinal))
                {
                    (claimed.Add(frame.Index) ? kept : shadowed).Add(frame.File);
                }

                var missing = claimed.Count == 0
                    ? new List<int>()
                    : Enumerable.Range(claimed.Min(), claimed.Max() - claimed.Min() + 1)
                        .Where(number => !claimed.Contains(number))
                        .ToList();

                animations.Add(new FontAnimation(pair.Key, kept, shadowed, missing));
            }

            unnumbered.Sort(StringComparer.Ordinal);
            return new Report(animations, unnumbered);
        }

        /// <summary>
        /// Ascending numbers as reading text, runs collapsed: <c>6, 7, 8, 9, 12</c> becomes <c>6–9, 12</c>.
        /// </summary>
        /// <remarks>
        /// A frame animation is thirty-odd files, so a gap in it is usually a run — and a message that lists
        /// twenty numbers is one nobody finishes reading.
        /// </remarks>
        internal static string Describe(IReadOnlyList<int> numbers)
        {
            var runs = new List<string>();
            for (var start = 0; start < numbers.Count;)
            {
                var end = start;
                while (end + 1 < numbers.Count && numbers[end + 1] == numbers[end] + 1)
                {
                    end++;
                }

                // An en dash, not a hyphen: these sit next to negative-looking things often enough.
                runs.Add(end > start ? $"{numbers[start]}–{numbers[end]}" : numbers[start].ToString());
                start = end + 1;
            }

            return string.Join(", ", runs);
        }
    }
}
