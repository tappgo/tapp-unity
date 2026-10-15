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
    /// A top-level menu is shared space, so this stays short on purpose — settings, the docs, the changelog,
    /// the welcome window and what version you actually have. Anything that belongs to a build belongs to the build hook, where it
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

        // Unity draws a separator between items whose priorities differ by 11 or more.
        private const int SettingsPriority = 0;
        private const int LinksPriority = 40;
        // One apart, so Welcome and About share a group rather than being separated from each other.
        private const int WelcomePriority = 60;
        private const int AboutPriority = 61;

        [MenuItem("Tapp/Settings…", false, SettingsPriority)]
        private static void OpenSettings()
        {
            SettingsService.OpenProjectSettings("Project/Tapp");
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
        /// The native version cannot be shown: no native SDK loads on this platform, so the bridge answers
        /// <c>editor-stub</c> in the Editor. <c>Tapp.SdkVersion()</c> reports it on a device, and that is the
        /// number that matters when a surface misbehaves — so say where to find it rather than printing a
        /// placeholder that reads like an answer.
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
                + "The native Tapp SDK version can only be read on a device — log Tapp.SdkVersion().Value "
                + "there. In the Editor it answers editor-stub.",
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
