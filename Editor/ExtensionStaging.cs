using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TappGo.Editor.Modules;
using TappGo.Internal;
using UnityEditor.Build;
using UnityEditor.PackageManager;
using UnityEngine;

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
        /// <summary>The extension template's path inside the package.</summary>
        internal const string ExtensionTemplatePath = "Runtime/iOSExtensionTemplate~";

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
            return TemplateRoot(ExtensionTemplatePath);
        }

        /// <summary>Locates one of <b>this</b> package's <c>~</c> template folders by its package-relative path.</summary>
        /// <exception cref="BuildFailedException">If the package cannot be resolved or the folder is absent.</exception>
        internal static string TemplateRoot(string templatePath)
        {
            return TappEditorModules.PackageFolder(typeof(ExtensionStaging).Assembly, templatePath);
        }

        /// <summary>
        /// The Swift package products this build links: core's, then every installed product's.
        /// </summary>
        /// <remarks>
        /// Core is always first and always present — the whole bridge facade lives in it, and it is the one
        /// module the C shims import. Each product package names its own; a package that is not installed
        /// contributes nothing, so its framework is not downloaded, linked or embedded.
        /// </remarks>
        internal static List<string> NativeProducts(IEnumerable<TappIosContribution> contributions)
        {
            var products = new List<string> { NativePin.IosCoreProductName };
            foreach (var contribution in contributions)
            {
                foreach (var product in contribution.SwiftProducts)
                {
                    if (!products.Contains(product))
                    {
                        products.Add(product);
                    }
                }
            }

            return products;
        }

        /// <summary>
        /// The Swift package products the <i>extension</i> links: core's, then those of each product that
        /// renders something in it.
        /// </summary>
        /// <remarks>
        /// Narrower than <see cref="NativeProducts"/> on purpose. A product can be installed and render
        /// nothing — Widgets with no widget ids, kept for Picture in Picture — and its library then belongs to
        /// the app alone. Linking it here anyway would load a framework the extension never enters, and make
        /// the generated bundle's "imports exactly what this target links" untrue.
        /// </remarks>
        internal static List<string> ExtensionProducts(IEnumerable<TappIosContribution> contributions)
        {
            var products = new List<string> { NativePin.IosCoreProductName };
            foreach (var contribution in Rendering(contributions))
            {
                foreach (var product in contribution.SwiftProducts)
                {
                    if (!products.Contains(product))
                    {
                        products.Add(product);
                    }
                }
            }

            return products;
        }

        /// <summary>
        /// The contributions that render in the extension — the only ones anything extension-side is taken
        /// from.
        /// </summary>
        /// <remarks>
        /// A product that renders nothing has nothing to say about a target it never enters: its imports would
        /// name a module the target does not link, which does not compile, and its frameworks and Keychain
        /// request would be for code that is not there. Asked in this one place — by the linked products, the
        /// generated bundle, the target's frameworks and its entitlements — so "imports exactly what this
        /// target links" holds whatever a product hands over, not only while each product takes care.
        /// </remarks>
        internal static List<TappIosContribution> Rendering(IEnumerable<TappIosContribution> contributions)
        {
            var rendering = new List<TappIosContribution>();
            foreach (var contribution in contributions)
            {
                if (contribution.BundleBody.Count > 0)
                {
                    rendering.Add(contribution);
                }
            }

            return rendering;
        }

        /// <summary>What every installed product package asks of an iOS build, in package-name order.</summary>
        internal static List<TappIosContribution> Contributions(TappGoSettings settings)
        {
            var contributions = new List<TappIosContribution>();
            foreach (var module in TappEditorModules.All)
            {
                var contribution = module.Ios(settings);
                if (contribution != null)
                {
                    contributions.Add(contribution);
                }
            }

            return contributions;
        }

        /// <summary>The widget extension's generated entry point.</summary>
        internal const string BundleFileName = "TappExtensionBundle.swift";

        /// <summary>
        /// Whether any installed product renders something — which is the only reason to have an extension.
        /// </summary>
        internal static bool NeedsExtension(IEnumerable<TappIosContribution> contributions)
        {
            return Rendering(contributions).Count > 0;
        }

        /// <summary>
        /// Whether an iOS build of this project would create the widget extension — for the pages that
        /// describe a build without running one.
        /// </summary>
        /// <remarks>
        /// Asked of the same descriptions the build applies, so a page and a build cannot disagree. A
        /// description can fail — a template missing from a product's package stops the build, by design —
        /// and <see cref="ContributionsProblem"/> is what says so: here it counts as <c>true</c>, so a page
        /// shows the extension's settings rather than hiding them from the one host about to need them.
        /// </remarks>
        internal static bool NeedsExtension(TappGoSettings settings)
        {
            if (settings == null)
            {
                return false;
            }

            try
            {
                return NeedsExtension(Contributions(settings));
            }
            catch (Exception)
            {
                return true;
            }
        }

        /// <summary>
        /// Why the installed products cannot be described to a build — which stops an iOS build before
        /// anything of theirs is applied — or <c>null</c>.
        /// </summary>
        /// <remarks>
        /// The build's own message, without its <c>[Tapp]</c> tag, so a page that reads "Ready" is never one
        /// whose next build stops on a product's missing template.
        /// </remarks>
        internal static string ContributionsProblem(TappGoSettings settings)
        {
            try
            {
                Contributions(settings);
                return null;
            }
            catch (Exception exception)
            {
                const string tag = "[Tapp] ";
                var message = exception.Message ?? "";
                return message.StartsWith(tag, StringComparison.Ordinal) ? message.Substring(tag.Length) : message;
            }
        }

        /// <summary>
        /// Why the extension's own settings — its target name, its font folders — cannot be built, or
        /// <c>null</c>.
        /// </summary>
        /// <remarks>
        /// Asked only when a build creates the extension, by the build hook and by the pages that describe one,
        /// so the two say the same thing in the same words.
        /// </remarks>
        internal static string ExtensionSettingsProblem(TappGoSettings settings)
        {
            var targetName = settings.extensionTargetName;
            if (string.IsNullOrWhiteSpace(targetName) || targetName.Any(c => !char.IsLetterOrDigit(c) && c != '-'))
            {
                return $"Extension target name \"{targetName}\" is unusable — it becomes an Xcode target and " +
                       "part of a bundle identifier, so it must be letters, digits and hyphens only. Fix it in " +
                       "Project Settings → Tapp.";
            }

            foreach (var (folder, label) in new[]
                     {
                         (settings.fontAnimationsFolder, "Font Animations Folder"),
                         (settings.fontsFolder, "Fonts Folder"),
                     })
            {
                // Both optional — blank passes.
                if (!string.IsNullOrWhiteSpace(folder) && !Directory.Exists(ResolveProjectPath(folder)))
                {
                    return $"{label} \"{folder}\" (Project Settings → Tapp) does not exist.";
                }
            }

            return null;
        }

        /// <summary>
        /// Resolves a setting typed as a project-relative path (e.g. <c>Assets/TappAssets/FontAnimations</c>)
        /// against the project root, so it reads correctly regardless of the working directory the build runs
        /// from.
        /// </summary>
        /// <remarks>
        /// <c>Application.dataPath</c> is always <c>&lt;project&gt;/Assets</c> in both the Editor and batchmode —
        /// the project-root anchor, never a literal path. An already-absolute <paramref name="relativePath"/>
        /// passes through unchanged: <c>Path.Combine</c> discards the first argument once the second is rooted.
        /// </remarks>
        internal static string ResolveProjectPath(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                return relativePath;
            }

            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            return Path.GetFullPath(Path.Combine(projectRoot, relativePath));
        }

        /// <summary>
        /// The Swift source of the widget extension's <c>@main</c> bundle, assembled from every product's lines.
        /// </summary>
        /// <remarks>
        /// <para>
        /// iOS gives an app one widget extension for Live Activities and widgets alike, and a Swift target one
        /// <c>@main</c>, so the products cannot each ship a bundle file. What is imported here is exactly what
        /// the extension target links, because both come from the same contributions — those that render
        /// (<see cref="Rendering"/>), and no others.
        /// </para>
        /// <para>
        /// Pure, so a test can read what a build would compile without exporting one.
        /// </para>
        /// </remarks>
        internal static string BundleSource(IEnumerable<TappIosContribution> contributions)
        {
            var imports = new List<string> { "SwiftUI", "WidgetKit" };
            var setup = new List<string>();
            var body = new List<string>();
            var declarations = new List<string>();
            var keychain = false;
            foreach (var contribution in Rendering(contributions))
            {
                keychain |= contribution.ExtensionKeychainSharing;
                foreach (var module in contribution.BundleImports)
                {
                    if (!imports.Contains(module))
                    {
                        imports.Add(module);
                    }
                }

                setup.AddRange(contribution.BundleSetup);
                body.AddRange(contribution.BundleBody);
                declarations.AddRange(contribution.BundleDeclarations);
            }

            var source = new System.Text.StringBuilder();
            source.Append(BundleHeader).Append(keychain ? EntitledToTheKeychain : EntitledToTheAppGroupOnly);
            foreach (var module in imports)
            {
                source.Append("import ").Append(module).Append('\n');
            }

            source.Append("\n@main\nstruct TappExtensionBundle: WidgetBundle {\n\n");
            source.Append("    init() {\n");
            foreach (var statement in setup)
            {
                source.Append(Indent(statement, 8)).Append('\n');
            }

            source.Append("    }\n\n    var body: some Widget {\n");
            foreach (var entry in body)
            {
                source.Append(Indent(entry, 8)).Append('\n');
            }

            source.Append("    }\n}\n");
            foreach (var declaration in declarations)
            {
                source.Append('\n').Append(declaration.TrimEnd()).Append('\n');
            }

            return source.ToString();
        }

        private const string BundleHeader =
            "//\n" +
            "//  TappExtensionBundle.swift\n" +
            "//  Tapp widget extension\n" +
            "//\n" +
            "//  GENERATED — do not edit in your Xcode project.\n" +
            "//\n" +
            "//  Unity regenerates the Xcode project on every Replace build, and the Tapp build hook rewrites this\n" +
            "//  file each time from the Tapp packages installed in the Unity project — one block per package that\n" +
            "//  renders here, so what is imported is exactly what this target links. What each Tapp surface looks\n" +
            "//  like is configured server-side, not here.\n" +
            "//\n" +
            "//  init() hands the SDK the App Group before anything renders. iOS offers no way to discover it from\n" +
            "//  inside an extension, and this is the only moment available: the system can start this process on\n" +
            "//  its own, before any app code has run. Every throw is swallowed on purpose — the SDK has already\n" +
            "//  logged the reason, and throwing out of a WidgetBundle initialiser kills the extension, which the\n" +
            "//  system reports as a blank surface with nothing to read anywhere.\n" +
            "//\n";

        /// <summary>The header's last paragraph when an installed product asked for Keychain access here.</summary>
        private const string EntitledToTheKeychain =
            "//  The same hook writes this target's entitlements: the App Group, and keychain-access-groups under\n" +
            "//  it, because what renders here signs in on its own and decrypts its cache with a key kept there.\n" +
            "//\n\n";

        /// <summary>The header's last paragraph when nothing that renders here can use the Keychain.</summary>
        private const string EntitledToTheAppGroupOnly =
            "//  The same hook writes this target's entitlements: the App Group and nothing else. What renders here\n" +
            "//  is drawn by a process that cannot reliably reach the Keychain, so it is stored unencrypted and no\n" +
            "//  Keychain access is claimed.\n" +
            "//\n\n";

        private static string Indent(string text, int spaces)
        {
            var pad = new string(' ', spaces);
            return pad + text.Replace("\n", "\n" + pad);
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
        /// <param name="placement">
        /// Where a template file lands, given its path relative to <paramref name="templateRoot"/>: the path to
        /// stage it under, or <c>null</c> to leave it out. Omitted, every file is staged under its own name.
        /// This is how a template that ships one variant per module combination stages exactly one of them.
        /// </param>
        /// <returns>The staged file paths, relative to <paramref name="destination"/>.</returns>
        /// <exception cref="BuildFailedException">
        /// If a <c>__TAPP_…__</c> placeholder survives. Left in place it would reach the compiler as a
        /// literal, and an App Group of <c>"__TAPP_APP_GROUP__"</c> fails at runtime on a device with an
        /// error that names neither this file nor the setting behind it.
        /// </exception>
        internal static List<string> Stage(
            string templateRoot,
            string destination,
            IReadOnlyDictionary<string, string> tokens,
            Func<string, string> placement = null)
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
                if (placement != null)
                {
                    relative = placement(relative);
                    if (relative == null)
                    {
                        continue;
                    }
                }

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
