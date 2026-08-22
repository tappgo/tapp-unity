using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace TappGo.Editor
{
    /// <summary>
    /// The window a host sees once, the first time this package is resolved into their project.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A wrapper that silently does nothing until two fields are filled in is a wrapper that gets reported as
    /// broken. This says what the package does, names the two settings an iOS build is refused without, and
    /// gets out of the way — everything it offers is also reachable from the <b>Tapp</b> menu, so nothing here
    /// is the only route to anything.
    /// </para>
    /// <para>
    /// Editor-only, like the rest of <c>Editor/</c>. It ships in the tarball but never in a player.
    /// </para>
    /// </remarks>
    internal sealed class TappGoWelcomeWindow : EditorWindow
    {
        /// <summary>Where the window's markup sits inside the package, relative to its resolved root.</summary>
        private const string UxmlPath = "Editor/UI/TappWelcome.uxml";

        private VisualElement _status;
        private Label _statusText;

        /// <summary>
        /// Whether this project has already been welcomed.
        /// </summary>
        /// <remarks>
        /// <c>EditorPrefs</c> is per user and machine-wide, not per project, so the project's own path is part
        /// of the key — on a shared key the second project someone installed the package into would never see
        /// this. It also means a moved or re-cloned project is welcomed again, which is the right side to err
        /// on: showing it twice costs a click, never showing it costs the integration.
        /// </remarks>
        private static string SeenKey => $"Tapp.Welcome.Seen.{Application.dataPath}";

        /// <summary>
        /// Opens the window, and records that this project has now seen it.
        /// </summary>
        internal static void Open()
        {
            // The stored value is the version that did the welcoming. Nothing reads it yet — it is recorded so
            // that a later decision about re-welcoming on upgrade has the fact it would need, rather than
            // having to start collecting it then.
            var package = PackageInfo.FindForAssembly(typeof(TappGoWelcomeWindow).Assembly);
            EditorPrefs.SetString(SeenKey, package?.version ?? "unknown");

            var window = GetWindow<TappGoWelcomeWindow>(utility: true, title: "Welcome to Tapp", focus: true);
            window.minSize = new Vector2(470f, 420f);
            window.Show();
        }

        /// <summary>
        /// Shows the window the first time this package is loaded in a project.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>The batch-mode guard is not optional.</b> Every <c>make</c> target, and every CI job, runs the
        /// Editor with <c>-batchmode -quit</c>; a window opened there is at best noise in a log and at worst a
        /// build that never returns. `-quit` does not save anyone: this runs during the domain reload that
        /// precedes it.
        /// </para>
        /// <para>
        /// Deferred through <c>delayCall</c> because this fires inside the reload, when the Editor is still
        /// importing and <c>GetWindow</c> would be fighting the layout it is about to restore.
        /// </para>
        /// </remarks>
        [InitializeOnLoadMethod]
        private static void ShowOnFirstLoad()
        {
            if (Application.isBatchMode)
            {
                return;
            }

            // Entering play mode is a domain reload too, and a welcome window across someone's first Play is
            // exactly the wrong moment.
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            if (EditorPrefs.HasKey(SeenKey))
            {
                return;
            }

            EditorApplication.delayCall += () =>
            {
                // Re-checked rather than assumed: a compile between the reload and this callback runs another
                // reload, and both would otherwise reach here.
                if (!EditorPrefs.HasKey(SeenKey))
                {
                    Open();
                }
            };
        }

        private void CreateGUI()
        {
            // Resolved rather than written down, for the same reason the settings page does it: a package
            // installed from the registry lives in Unity's global cache under a versioned folder name, and one
            // embedded or forked can sit under any folder name at all.
            //
            // Fully qualified in the alias above: UnityEditor.PackageInfo is a different, unrelated type.
            var package = PackageInfo.FindForAssembly(typeof(TappGoWelcomeWindow).Assembly);
            // assetPath, not resolvedPath — this is an AssetDatabase lookup, so it wants the virtual
            // `Packages/<name>/…` mount; and joined with '/', which Path.Combine would turn into a backslash
            // on Windows and then find nothing.
            var uxml = package == null ? null : $"{package.assetPath}/{UxmlPath}";
            var tree = uxml == null ? null : AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(uxml);
            if (tree == null)
            {
                // Same reasoning as the settings page: an empty window reads as "Tapp isn't installed" rather
                // than "Tapp is installed wrong", and only the second one is true.
                rootVisualElement.Add(new HelpBox(
                    $"Tapp's welcome UI is missing from the package ({uxml ?? UxmlPath}). Reinstall com.tapp.go.",
                    HelpBoxMessageType.Error));
                return;
            }

            var page = tree.Instantiate();
            page.AddToClassList(EditorGUIUtility.isProSkin ? "tapp--dark" : "tapp--light");
            // Instantiate() returns a plain wrapper that does not stretch, so the ScrollView inside it would
            // collapse to its content height and the window would scroll nothing.
            page.style.flexGrow = 1f;
            rootVisualElement.Add(page);

            _status = page.Q<VisualElement>("status");
            _statusText = page.Q<Label>("statusText");

            page.Q<Label>("version").text = $"{package.name} {package.version}";

            page.Q<Button>("settingsButton").clicked += () =>
                SettingsService.OpenProjectSettings("Project/Tapp");
            // Reuses the menu's implementation rather than repeating it, so there is one definition of where
            // the docs are and it comes from the manifest.
            page.Q<Button>("docsButton").clicked += TappGoMenu.OpenDocumentation;

            ShowValidation();
        }

        /// <summary>
        /// Re-reads the settings whenever the window comes forward.
        /// </summary>
        /// <remarks>
        /// The two buttons above send people to other windows and they come back here, so a status line
        /// captured once at open would be describing the project as it was before they left.
        /// </remarks>
        private void OnFocus()
        {
            // Focus arrives before CreateGUI on the first show, and after the window is closed on the last.
            if (_status != null)
            {
                ShowValidation();
            }
        }

        private void ShowValidation()
        {
            var settings = TappGoSettings.Load();
            var invalid = settings == null
                ? "No Tapp settings in this project yet — open Tapp Settings to create them."
                : settings.Validate();

            _status.EnableInClassList("tapp-status--bad", invalid != null);
            _status.EnableInClassList("tapp-status--ok", invalid == null);

            _statusText.text = invalid != null
                // The build hook's own wording where it has one, so a person meets a given problem described
                // one way rather than two.
                ? $"Not ready: {invalid}"
                : "Ready. Your App Group and app id are set, and iOS builds will carry the widget extension.";
        }
    }
}
