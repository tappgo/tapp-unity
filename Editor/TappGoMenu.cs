using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager.UI;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace TappGo.Editor
{
    /// <summary>
    /// The <b>Tapp</b> menu: the five things a host does that aren't building.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A top-level menu is shared space, so this stays short on purpose — settings, the sample, the docs, and
    /// what version you actually have. Anything that belongs to a build belongs to the build hook, where it
    /// runs whether or not somebody remembered to click it.
    /// </para>
    /// <para>
    /// Every URL and version here is read from the installed package rather than written down, so a menu item
    /// cannot quietly point somewhere the package no longer does.
    /// </para>
    /// </remarks>
    internal static class TappGoMenu
    {
        private const string PackageName = "com.tapp.go";

        /// <summary>
        /// One type from the sample, used to notice a copy of it that is already in the project.
        /// </summary>
        /// <remarks>
        /// Renaming <c>LiveActivityConsole</c> without changing this turns the duplicate check into a no-op
        /// that keeps passing, which is why a test asserts the two still agree.
        /// </remarks>
        internal const string SampleMarkerScript = "LiveActivityConsole";

        // Unity draws a separator between items whose priorities differ by 11 or more.
        private const int SettingsPriority = 0;
        private const int SamplePriority = 20;
        private const int LinksPriority = 40;
        // One apart, so Welcome and About share a group rather than being separated from each other.
        private const int WelcomePriority = 60;
        private const int AboutPriority = 61;

        [MenuItem("Tapp/Settings…", false, SettingsPriority)]
        private static void OpenSettings()
        {
            SettingsService.OpenProjectSettings("Project/Tapp");
        }

        /// <summary>
        /// Copies the Live Activity sample into <c>Assets/Samples</c>, the same as the Package Manager button.
        /// </summary>
        /// <remarks>
        /// Worth a menu item because the Package Manager one is three clicks deep behind a package selection,
        /// and this is the first thing a new integrator should run.
        /// </remarks>
        [MenuItem("Tapp/Import Live Activity Sample", false, SamplePriority)]
        private static void ImportSample()
        {
            var package = Package();
            if (package == null)
            {
                return;
            }

            // The package ships exactly one sample, so this is a first-element lookup rather than a loop over
            // several — spelling it as one says which, and leaves the "none at all" case reading as the
            // packaging defect it would be.
            var sample = Sample.FindByPackage(package.name, package.version).FirstOrDefault();
            if (sample.importPath == null)
            {
                Debug.LogError($"[Tapp] {package.name} {package.version} ships no samples.");
                return;
            }

            // Import overwrites without asking, and the sample is meant to be edited — so a second click must
            // not be able to throw away someone's changes silently.
            if (sample.isImported && !EditorUtility.DisplayDialog(
                    "Import Live Activity Sample",
                    $"\"{sample.displayName}\" is already imported at:\n\n{sample.importPath}\n\n"
                    + "Importing again overwrites it, including any changes you have made there.",
                    "Overwrite", "Cancel"))
            {
                return;
            }

            // …and a copy somewhere *other* than where Unity would put it is the worse case, because importing
            // then leaves two, and every script in the sample becomes a duplicate class definition.
            var elsewhere = SampleScriptsOutside(sample);
            if (elsewhere != null && !EditorUtility.DisplayDialog(
                    "Import Live Activity Sample",
                    $"This project already contains the sample's scripts at:\n\n{elsewhere}\n\n"
                    + "That is not where Unity imports samples, so importing would add a second copy rather "
                    + "than replace this one — and every script in it would then be defined twice (CS0111).\n\n"
                    + "Delete or move the existing copy first.",
                    "Import anyway", "Cancel"))
            {
                return;
            }

            sample.Import(Sample.ImportOptions.OverridePreviousImports);
        }

        /// <summary>
        /// Where the sample's scripts already live, if that is somewhere Unity would not import them.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <see cref="Sample.isImported"/> only answers for the canonical destination —
        /// <c>Assets/Samples/&lt;package&gt;/&lt;version&gt;/&lt;sample&gt;</c>. A copy that was moved, renamed,
        /// or symlinked in reports as "not imported", so the prompt above never fires and the import lands a
        /// second set of the same types. This repo's own test project is symlinked exactly that way, but a host
        /// who reorganised their <c>Assets/</c> gets there too.
        /// </para>
        /// <para>
        /// One type is enough to look for. Finding the sample's whole file list would be more thorough and no
        /// more useful: the question is only whether a second copy is about to exist.
        /// </para>
        /// </remarks>
        private static string SampleScriptsOutside(Sample sample)
        {
            // Project-relative, because that is the form AssetDatabase returns and the two get compared below.
            // Application.dataPath ends in "/Assets", so its parent is the project folder.
            var projectRoot = Path.GetDirectoryName(Application.dataPath);
            var destination = projectRoot != null && sample.importPath.StartsWith(projectRoot)
                ? sample.importPath.Substring(projectRoot.Length + 1).Replace('\\', '/')
                : null;

            return AssetDatabase.FindAssets($"{SampleMarkerScript} t:MonoScript")
                .Select(AssetDatabase.GUIDToAssetPath)
                // FindAssets matches names loosely — an unrelated LiveActivityConsoleExtras would count.
                .Where(path => Path.GetFileNameWithoutExtension(path) == SampleMarkerScript)
                .Select(path => path.Replace('\\', '/'))
                .FirstOrDefault(path =>
                    destination == null || !path.StartsWith(destination + "/", StringComparison.Ordinal));
        }

        [MenuItem("Tapp/Documentation", false, LinksPriority)]
        internal static void OpenDocumentation()
        {
            Open(Package()?.documentationUrl, "documentationUrl");
        }

        [MenuItem("Tapp/Changelog", false, LinksPriority)]
        private static void OpenChangelog()
        {
            Open(Package()?.changelogUrl, "changelogUrl");
        }

        /// <summary>
        /// Reopens the first-run window, which otherwise appears once per project and never again.
        /// </summary>
        /// <remarks>
        /// The window is the only thing in the package that decides for itself when to appear, so it needs a
        /// way back — for the second person on a team, who joined after the project was already welcomed.
        /// </remarks>
        [MenuItem("Tapp/Welcome", false, WelcomePriority)]
        private static void Welcome()
        {
            TappGoWelcomeWindow.Open();
        }

        /// <summary>
        /// Which versions are installed — the first question any support conversation starts with.
        /// </summary>
        /// <remarks>
        /// The native version cannot be shown: it lives in the xcframework, which does not load on this
        /// platform, so the bridge answers <c>editor-stub</c> in the Editor. The sample's <b>SDK</b> tab reports
        /// it on device, and that is the number that matters when a Live Activity misbehaves — so say where to
        /// find it rather than printing a placeholder that reads like an answer.
        /// </remarks>
        [MenuItem("Tapp/About", false, AboutPriority)]
        private static void About()
        {
            var package = Package();
            if (package == null)
            {
                return;
            }

            // Title from the manifest rather than written down, like every other string on this menu — and the
            // body then carries the id and version only, because repeating the display name under it says
            // nothing twice.
            EditorUtility.DisplayDialog(
                package.displayName,
                $"{package.name} {package.version}\n\n"
                + "The native TappGo version can only be read on a device — run the Live Activity sample and "
                + "look at its SDK tab.",
                "OK");
        }

        private static PackageInfo Package()
        {
            var package = PackageInfo.FindForAssembly(typeof(TappGoMenu).Assembly);
            if (package == null)
            {
                Debug.LogError(
                    $"[Tapp] Could not find {PackageName} in this project. If you copied the package into "
                    + "Assets/ rather than installing it, install it through the Package Manager instead.");
            }

            return package;
        }

        private static void Open(string url, string field)
        {
            if (string.IsNullOrEmpty(url))
            {
                Debug.LogError($"[Tapp] {PackageName} declares no {field}.");
                return;
            }

            Application.OpenURL(url);
        }
    }
}
