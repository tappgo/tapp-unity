using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor.Build;
using UnityEditor.PackageManager;

namespace TappGo.Editor
{
    /// <summary>
    /// Copies the Live Activity extension's sources out of the package and into the Xcode build.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The template lives in <c>Runtime/iOSExtensionTemplate~/</c>. The trailing <c>~</c> hides it from
    /// Unity's asset pipeline — which is the point: those files must never be compiled into the player, they
    /// are iOS extension sources. <c>npm pack</c> still ships the folder and <c>System.IO</c> still reads it.
    /// That is what lets the bundle own a real <c>@main</c> instead of a commented-out one a script
    /// un-comments.
    /// </para>
    /// <para>
    /// <b>Staging, never editing in place.</b> An installed package resolves into Unity's read-only global
    /// cache, so the template is a source to copy from. It is also re-staged on every build, because Unity
    /// regenerates the Xcode project on every Replace build and anything hand-edited there is already gone.
    /// </para>
    /// </remarks>
    internal static class ExtensionStaging
    {
        /// <summary>The template's path inside the package.</summary>
        private const string TemplatePath = "Runtime/iOSExtensionTemplate~";

        /// <summary>Marks a placeholder, and makes an unsubstituted one greppable.</summary>
        private const string TokenPrefix = "__TAPP_";

        /// <summary>
        /// Extensions whose contents are rewritten. Anything else is copied byte for byte.
        /// </summary>
        /// <remarks>
        /// An allow-list rather than "substitute everything": reading a binary — an asset catalog, a font —
        /// as text and writing it back would corrupt it silently, and the corruption would only show up as a
        /// missing image at render time.
        /// </remarks>
        private static readonly HashSet<string> Substitutable =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".swift", ".plist", ".entitlements", ".h", ".m" };

        /// <summary>
        /// Locates the template inside the installed package.
        /// </summary>
        /// <remarks>
        /// Resolved through <see cref="PackageInfo"/> rather than a literal path, because a package installed
        /// from a registry lives in Unity's global cache under a versioned folder name that nothing should
        /// hardcode.
        /// </remarks>
        /// <exception cref="BuildFailedException">If the package or the template cannot be found.</exception>
        internal static string TemplateRoot()
        {
            var package = PackageInfo.FindForAssembly(Assembly.GetExecutingAssembly());
            if (package == null)
            {
                throw new BuildFailedException(
                    "[Tapp] Could not resolve the Tapp package. Reinstall it through the Package Manager " +
                    "— a loose copy of the sources under Assets/ is not supported.");
            }

            var root = Path.Combine(package.resolvedPath, TemplatePath);
            if (!Directory.Exists(root))
            {
                throw new BuildFailedException(
                    $"[Tapp] The extension template is missing from the package at {root}. This is a " +
                    "packaging defect — report it rather than working around it.");
            }

            return root;
        }

        /// <summary>
        /// Writes the template into <paramref name="destination"/> with <paramref name="tokens"/> substituted.
        /// </summary>
        /// <remarks>
        /// <paramref name="destination"/> is emptied first. A file dropped from a newer version of the package
        /// would otherwise linger in a Replace build, and a stale Swift file carrying <c>@main</c> is a
        /// duplicate-entry-point error with no obvious cause.
        /// </remarks>
        /// <param name="templateRoot">The template folder to copy from.</param>
        /// <param name="destination">Where to write, inside the Xcode build output.</param>
        /// <param name="tokens">Placeholder to replacement, e.g. <c>__TAPP_APP_GROUP__</c>.</param>
        /// <returns>The staged file paths, relative to <paramref name="destination"/>.</returns>
        /// <exception cref="BuildFailedException">
        /// If a <c>__TAPP_…__</c> placeholder survives. Left in place it would reach the compiler as a
        /// literal, and an App Group of <c>"__TAPP_APP_GROUP__"</c> fails at runtime on a device with an
        /// error that names neither this file nor the setting behind it.
        /// </exception>
        internal static List<string> Stage(
            string templateRoot,
            string destination,
            IReadOnlyDictionary<string, string> tokens)
        {
            if (Directory.Exists(destination))
            {
                Directory.Delete(destination, true);
            }

            Directory.CreateDirectory(destination);

            var staged = new List<string>();
            foreach (var source in Directory.GetFiles(templateRoot, "*", SearchOption.AllDirectories))
            {
                var relative = Relative(templateRoot, source);
                var target = Path.Combine(destination, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(target) ?? destination);

                if (Substitutable.Contains(Path.GetExtension(source)))
                {
                    File.WriteAllText(target, Substitute(File.ReadAllText(source), tokens, relative));
                }
                else
                {
                    File.Copy(source, target, true);
                }

                staged.Add(relative);
            }

            staged.Sort(StringComparer.Ordinal);
            return staged;
        }

        /// <summary>Replaces every token, then proves none is left.</summary>
        private static string Substitute(
            string content,
            IReadOnlyDictionary<string, string> tokens,
            string fileName)
        {
            foreach (var token in tokens)
            {
                content = content.Replace(token.Key, token.Value);
            }

            var leftover = content.IndexOf(TokenPrefix, StringComparison.Ordinal);
            if (leftover >= 0)
            {
                var end = content.IndexOf("__", leftover + TokenPrefix.Length, StringComparison.Ordinal);
                var name = end < 0 ? TokenPrefix : content.Substring(leftover, end + 2 - leftover);
                throw new BuildFailedException(
                    $"[Tapp] {name} was not substituted in {fileName}. The package's template and its build " +
                    "hook are out of step — reinstall the package rather than editing the staged copy.");
            }

            return content;
        }

        /// <summary>A path relative to <paramref name="root"/>, with forward slashes.</summary>
        private static string Relative(string root, string path)
        {
            var trimmed = path.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar, '/');
            return trimmed.Replace(Path.DirectorySeparatorChar, '/');
        }
    }
}
