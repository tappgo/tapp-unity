using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor.Build;

namespace TappGo.Editor
{
    /// <summary>
    /// Copies a host's own font files — frame-font animations (e.g. <c>SlotFont_0.ttf</c>,
    /// <c>SlotFont_1.ttf</c>, …) or the app's own text fonts — out of the project and into the extension build.
    /// </summary>
    /// <remarks>
    /// <para>
    /// These are content, not SDK behavior — a slot-machine animation or a house typeface belongs to the
    /// host, not to Tapp (see CLAUDE.md §1). So unlike <see cref="ExtensionStaging"/>, which stages a fixed
    /// template out of this package, this stages a variable set of files out of wherever the host put them in
    /// their own project — <see cref="TappGoSettings.fontAnimationsFolder"/> or
    /// <see cref="TappGoSettings.fontsFolder"/>, called once per folder.
    /// </para>
    /// <para>
    /// The mask font every frame animation shares (<c>TappFrameAnimationMask.otf</c>) needs none of this — it
    /// ships inside <c>TappGo.xcframework</c> itself and is resolved by the native SDK through
    /// <c>Bundle(for:)</c>, not <c>Bundle.main</c>, so it is already available to the extension the moment the
    /// framework is linked. Nothing here or in the build hook re-stages it.
    /// </para>
    /// </remarks>
    internal static class FontFileStaging
    {
        private static readonly HashSet<string> FontExtensions =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".ttf", ".otf" };

        /// <summary>
        /// Copies every <c>.ttf</c>/<c>.otf</c> found anywhere under <paramref name="sourceFolder"/> into
        /// <paramref name="destination"/>, flat. Returns the copied file names — what the extension's
        /// <c>Info.plist</c> must list under <c>UIAppFonts</c>, since Xcode bundles individual file references
        /// at the bundle root regardless of the source's subfolder layout.
        /// </summary>
        /// <remarks>
        /// A blank <paramref name="sourceFolder"/> means the host hasn't set the setting named by
        /// <paramref name="settingLabel"/> — every folder this stages from is optional, so this is a silent
        /// no-op, not a failure. <paramref name="settingLabel"/> only names that setting in the exception
        /// below.
        /// </remarks>
        /// <exception cref="BuildFailedException">
        /// If a non-blank <paramref name="sourceFolder"/> does not exist, or two files anywhere under it share
        /// a name — which would collide once Xcode flattens them into one bundle.
        /// </exception>
        internal static List<string> Stage(string sourceFolder, string destination, string settingLabel)
        {
            if (string.IsNullOrWhiteSpace(sourceFolder))
            {
                return new List<string>();
            }

            if (!Directory.Exists(sourceFolder))
            {
                throw new BuildFailedException(
                    $"[Tapp] {settingLabel} \"{sourceFolder}\" (Project Settings → Tapp) does not exist.");
            }

            Directory.CreateDirectory(destination);

            var staged = new List<string>();
            var sourceByName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var source in Directory
                         .GetFiles(sourceFolder, "*", SearchOption.AllDirectories)
                         .Where(path => FontExtensions.Contains(Path.GetExtension(path)))
                         .OrderBy(path => path, StringComparer.Ordinal))
            {
                var name = Path.GetFileName(source);
                if (sourceByName.TryGetValue(name, out var earlier))
                {
                    throw new BuildFailedException(
                        $"[Tapp] Two font files are both named \"{name}\" under \"{sourceFolder}\" ({earlier} " +
                        $"and {source}) — Xcode bundles them flat, so one would silently replace the other. " +
                        "Rename one.");
                }

                sourceByName[name] = source;
                File.Copy(source, Path.Combine(destination, name), true);
                staged.Add(name);
            }

            return staged;
        }
    }
}
