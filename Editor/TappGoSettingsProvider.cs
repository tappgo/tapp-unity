using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TappGo.Editor.Modules;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace TappGo.Editor
{
    /// <summary>
    /// <b>Project Settings → Tapp</b>: the one place a host configures the SDK.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The highest-leverage code in the package. Every field here is one a build hook will refuse to build
    /// without, and catching a malformed App Group at the moment someone types it is worth more than the same
    /// message arriving on a tester's device three days later — where a missing App Group renders an empty
    /// surface with no error at all.
    /// </para>
    /// <para>
    /// Built with UI Toolkit, from <c>UI/TappSettings.uxml</c> and its stylesheet. The frame is ours — logo,
    /// rule, status strip, button. The fields are stock <see cref="PropertyField"/>s bound to the asset, which
    /// is what keeps undo, right-click revert and settings-search highlighting working; hand-rolled controls
    /// would look the same and quietly lose all three.
    /// </para>
    /// </remarks>
    internal sealed class TappGoSettingsProvider : SettingsProvider
    {
        private const string AssetPath = "Assets/Resources/" + TappGoSettings.ResourceName + ".asset";

        /// <summary>
        /// Where a fresh settings asset points <see cref="TappGoSettings.fontAnimationsFolder"/> and
        /// <see cref="TappGoSettings.fontsFolder"/> — a discoverable, host-owned home for content Tapp
        /// doesn't ship (see CLAUDE.md §1), created alongside the asset rather than left for a host to invent
        /// and type in by hand.
        /// </summary>
        private const string DefaultFontAnimationsFolder = "Assets/TappAssets/FontAnimations";
        private const string DefaultFontsFolder = "Assets/TappAssets/Fonts";

        /// <summary>Where the page's markup sits inside the package, relative to its resolved root.</summary>
        private const string UxmlPath = "Editor/UI/TappSettings.uxml";

        /// <summary>
        /// How many entries a font list shows before it starts collapsed. Chosen to be a screenful rather than
        /// a principle: below it the list is a fact worth reading, above it a reference worth having.
        /// </summary>
        private const int CollapseAbove = 8;

        /// <summary>How many files or animations a warning names before it stops and counts the rest.</summary>
        private const int MaxWarningLines = 6;

        private SerializedObject _settings;
        private VisualElement _fields;
        private VisualElement _create;
        private VisualElement _status;
        private VisualElement _fontAnimations;
        private VisualElement _fonts;
        private Label _statusText;

        /// <summary>The folder settings the panels currently describe, so an unrelated edit can skip the scan.</summary>
        private (string animations, string fonts) _scanned;

        /// <summary>
        /// Whether an iOS build of the project as it stands creates the widget extension. Decides whether the
        /// extension's own fields — its names, its fonts — are on the page at all.
        /// </summary>
        private bool _needsExtension;

        /// <summary>
        /// The core fields that configure the widget extension and nothing else. Shown only while something
        /// installed makes a build create one: with core alone, or Widgets kept for Picture in Picture, they
        /// would be four settings that apply to no target.
        /// </summary>
        private static readonly HashSet<string> ExtensionOnlyFields = new HashSet<string>
        {
            nameof(TappGoSettings.extensionTargetName),
            nameof(TappGoSettings.extensionDisplayName),
            nameof(TappGoSettings.fontAnimationsFolder),
            nameof(TappGoSettings.fontsFolder),
        };

        private const string ExtensionOnlyClass = "tapp-extension-only";

        private TappGoSettingsProvider() : base("Project/Tapp", SettingsScope.Project)
        {
            keywords = SearchKeywords();
        }

        /// <summary>
        /// What the Project Settings search finds this page by: Tapp itself, and what is actually on it.
        /// </summary>
        /// <remarks>
        /// Built from the installed products and the fields their sections show, not written down — a search
        /// for "live activity" that lands on a page with no Live Activity field is a page lying about its
        /// contents.
        /// </remarks>
        internal static HashSet<string> SearchKeywords()
        {
            var words = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "tapp", "app group" };
            foreach (var module in TappEditorModules.All)
            {
                var name = module.DisplayName.ToLowerInvariant();
                words.Add(name);
                // The search matches each typed word as a substring, and "activity" is not one of
                // "activities": a host typing the singular — the likelier search — must find the page too.
                words.Add(Singular(name));

                var asset = module.Settings(create: false);
                if (asset != null)
                {
                    using (var serialized = new SerializedObject(asset))
                    {
                        words.UnionWith(GetSearchKeywordsFromSerializedObject(serialized));
                    }
                }
            }

            return words;
        }

        /// <summary>The English singular of a display name's last word: "live activities" → "live activity".</summary>
        internal static string Singular(string name)
        {
            if (name.EndsWith("ies", StringComparison.Ordinal))
            {
                return name.Substring(0, name.Length - 3) + "y";
            }

            return name.EndsWith("s", StringComparison.Ordinal) ? name.Substring(0, name.Length - 1) : name;
        }

        [SettingsProvider]
        internal static SettingsProvider Create() => new TappGoSettingsProvider();

        public override void OnActivate(string searchContext, VisualElement root)
        {
            // Resolved rather than written down, for the same reason the extension template is: a package
            // installed from the registry lives in Unity's global cache under a versioned folder name, and one
            // embedded or forked can sit under any folder name at all.
            //
            // Fully qualified: UnityEditor.PackageInfo is a different, unrelated type of the same name.
            var package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(
                typeof(TappGoSettingsProvider).Assembly);
            // assetPath, not resolvedPath: this is an AssetDatabase lookup, so it wants the virtual
            // `Packages/<name>/…` mount rather than the real folder on disk — and joined with '/' rather than
            // Path.Combine, which would put a backslash in it on Windows and find nothing.
            var uxml = package == null ? null : $"{package.assetPath}/{UxmlPath}";
            var tree = uxml == null ? null : AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(uxml);
            if (tree == null)
            {
                // Rather than an empty page: this only happens if the package is malformed, and an empty
                // settings page reads as "Tapp isn't installed" instead of "Tapp is installed wrong".
                root.Add(new HelpBox(
                    $"Tapp's settings UI is missing from the package ({uxml ?? UxmlPath}). Reinstall com.tapp.go.",
                    HelpBoxMessageType.Error));
                return;
            }

            var page = tree.Instantiate();
            page.AddToClassList(EditorGUIUtility.isProSkin ? "tapp--dark" : "tapp--light");

            // Scrolls, because the page grows a section per installed product. Left to the settings window's
            // own layout it does not overflow — flex squeezes every row into the height there is, and a page
            // with two products installed draws its fields on top of each other.
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1;
            scroll.Add(page);
            root.Add(scroll);

            _fields = page.Q<VisualElement>("fields");
            _create = page.Q<VisualElement>("create");
            _status = page.Q<VisualElement>("status");
            _fontAnimations = page.Q<VisualElement>("fontAnimations");
            _fonts = page.Q<VisualElement>("fonts");
            _statusText = page.Q<Label>("statusText");
            // The wrapper's version, read rather than written down so it can't go stale. The *native* version
            // would be the more useful number, but it can only be got by calling into the native SDK, which
            // does not exist on this platform — the Editor bridge reports `editor-stub`. So this names the
            // wrapper, and Tapp.SdkVersion() names the native on a device.
            page.Q<Label>("version").text = $"{package.name} {package.version}";

            // Says what is installed, since that is what decides what a build links: core, and each product
            // package by the name its section below carries.
            var installed = new List<string> { "Core" };
            installed.AddRange(TappEditorModules.All.Select(module => module.DisplayName));
            page.Q<Label>("tagline").text = "Installed: " + string.Join("  ·  ", installed);
            page.Q<Button>("createButton").clicked += CreateSettings;

            // Again here, not only in the constructor: a product's fields join the search once its settings
            // asset exists, and that can happen after this provider was made.
            keywords = SearchKeywords();

            // The animation read-out is a view of files on disk, not of the asset, so nothing in the
            // SerializedObject changes when a host drops in a folder of frames. projectChanged is what fires
            // for that, and only for it — polling a recursive directory walk from OnInspectorUpdate would run
            // it ten times a second to learn nothing.
            EditorApplication.projectChanged -= ShowFontPanels;
            EditorApplication.projectChanged += ShowFontPanels;

            Bind(TappGoSettings.Load());
        }

        public override void OnDeactivate()
        {
            EditorApplication.projectChanged -= ShowFontPanels;
        }

        /// <summary>
        /// Points the page at an asset, or at the create prompt when there isn't one.
        /// </summary>
        private void Bind(TappGoSettings settings)
        {
            _fields.Clear();

            var missing = settings == null;
            _create.EnableInClassList("tapp-hidden", !missing);
            _fields.EnableInClassList("tapp-hidden", missing);
            _status.EnableInClassList("tapp-hidden", missing);

            if (missing)
            {
                _settings = null;
                ShowFontPanels();
                return;
            }

            _settings = new SerializedObject(settings);

            var property = _settings.GetIterator();
            property.NextVisible(true);   // skips m_Script
            while (property.NextVisible(false))
            {
                var field = new PropertyField(property.Copy());
                if (ExtensionOnlyFields.Contains(property.name))
                {
                    // A [Header] is drawn inside the first field under it, so hiding these four takes the
                    // "iOS build" and "Fonts" headings with them.
                    field.AddToClassList(ExtensionOnlyClass);
                }

                _fields.Add(field);
            }

            _fields.Bind(_settings);
            AddModuleSections();

            // Revalidate on every edit, including undo and a change made in the Inspector while this page is
            // open — TrackSerializedObjectValue fires for all of them, where a callback per field would not.
            _fields.TrackSerializedObjectValue(_settings, _ => Refresh());

            // The font panels are rebuilt here whatever they showed before, not only when a folder changed:
            // on a first bind nothing has hidden them yet.
            ShowExtensionFields();
            ShowValidation();
            ShowFontPanels();
        }

        /// <summary>
        /// Everything on the page that is a judgement of the settings rather than a field of them.
        /// </summary>
        /// <remarks>
        /// In this order: whether there will be an extension decides which fields and font panels exist, and
        /// the status describes what is left.
        /// </remarks>
        private void Refresh()
        {
            ShowExtensionFields();
            ShowValidation();
            ShowFontPanelsIfFoldersChanged();
        }

        /// <summary>
        /// Shows the extension's own fields only while a build would create the extension.
        /// </summary>
        /// <remarks>
        /// Asked again on every edit, a product's included: whether an installed product renders anything can
        /// turn on one of its own settings — Widgets renders nothing until it has a widget id.
        /// </remarks>
        private void ShowExtensionFields()
        {
            _needsExtension = ExtensionStaging.NeedsExtension(_settings?.targetObject as TappGoSettings);
            _fields.Query<PropertyField>(className: ExtensionOnlyClass)
                .ForEach(field => field.EnableInClassList("tapp-hidden", !_needsExtension));
        }

        /// <summary>
        /// One section per installed Tapp product package, under core's own fields.
        /// </summary>
        /// <remarks>
        /// Each product keeps its settings in an asset of its own, so it can be installed and removed without
        /// touching core's. A product whose asset does not exist yet gets a button rather than an asset made
        /// behind the developer's back: it has to be committed, and a file that appeared because a page was
        /// opened is the one nobody adds to source control.
        /// </remarks>
        private void AddModuleSections()
        {
            foreach (var module in TappEditorModules.All)
            {
                var heading = new VisualElement();
                heading.AddToClassList("tapp-module-heading");
                var title = new Label(module.DisplayName);
                title.AddToClassList("tapp-module-heading__title");
                var from = new Label(module.Id);
                from.AddToClassList("tapp-module-heading__package");
                heading.Add(title);
                heading.Add(from);
                _fields.Add(heading);

                var asset = module.Settings(create: false);
                if (asset == null)
                {
                    var owner = module;
                    _fields.Add(new Button(() =>
                    {
                        owner.Settings(create: true);

                        // The product's fields join the search now that its asset exists.
                        keywords = SearchKeywords();
                        Bind(TappGoSettings.Load());
                    })
                    {
                        text = $"Create {module.DisplayName} settings",
                    });
                    continue;
                }

                var serialized = new SerializedObject(asset);
                var section = new VisualElement();
                var property = serialized.GetIterator();
                property.NextVisible(true);   // skips m_Script
                while (property.NextVisible(false))
                {
                    section.Add(new PropertyField(property.Copy()));
                }

                section.Bind(serialized);
                section.TrackSerializedObjectValue(serialized, _ => Refresh());
                _fields.Add(section);
            }
        }

        private void ShowValidation()
        {
            if (_settings?.targetObject is not TappGoSettings settings)
            {
                return;
            }

            // The same strings the build hooks fail with, deliberately — one wording for one problem, so
            // nobody has to work out whether two messages mean the same thing. A mixed set of package versions
            // stops both platforms' builds, so it stands in for either.
            var verdict = TappBuildReadiness.Judge(EditorUserBuildSettings.activeBuildTarget, settings, _needsExtension);

            _status.EnableInClassList("tapp-status--bad", verdict.Blocking);
            _status.EnableInClassList("tapp-status--ok", !verdict.Blocking);
            _statusText.text = StatusText(verdict, TappEditorModules.All.Count, _needsExtension);
        }

        /// <summary>
        /// What the status strip says. Apart from the page so a test can read every case.
        /// </summary>
        /// <remarks>
        /// The Ready sentence promises only what this project's build does: an extension when something
        /// installed renders in one, and nothing on screen at all when no product is installed.
        /// </remarks>
        internal static string StatusText(TappBuildReadiness.Verdict verdict, int products, bool needsExtension)
        {
            if (verdict.Blocking)
            {
                return verdict.Problems;
            }

            if (verdict.ReadyFor != null)
            {
                return $"Ready for {verdict.ReadyFor} builds.\n\n{verdict.Problems}";
            }

            if (products == 0)
            {
                return "Ready. Core is configured. Install a product package to put something on screen.";
            }

            const string OnADevice =
                "Tapp surfaces are verified on a device. The Simulator does not enforce App Group "
                + "entitlements, so it renders a placeholder instead of failing.";

            return needsExtension
                ? "Ready. Tapp adds the widget extension to your Xcode project on each iOS build — nothing to "
                  + "set up by hand in Xcode.\n\n" + OnADevice
                : "Ready. Tapp sets up your Xcode and Gradle projects on each build — nothing to set up by hand.";
        }

        /// <summary>
        /// Lists what the two font folders actually contain, as the extension will see them.
        /// </summary>
        /// <remarks>
        /// <para>
        /// A path field can only tell a host that a folder exists. What decides whether a Live Activity
        /// renders what they meant is finer than that and invisible from here — frames grouped by the name
        /// their files share, ordinary fonts registered under a name stored inside the file — and every way of
        /// getting it wrong ends the same way: the right pixels never appear, on a device, with nothing
        /// logged. That is what these two panels are for.
        /// </para>
        /// <para>
        /// Warnings, never errors. Every file in either folder is bundled and registered whatever its name.
        /// </para>
        /// </remarks>
        private void ShowFontPanels()
        {
            _scanned = FontFolders();
            ShowFontAnimations();
            ShowFonts();
        }

        /// <summary>
        /// Rebuilds the font panels only when one of the two folder settings has actually changed.
        /// </summary>
        /// <remarks>
        /// <see cref="ShowFontPanels"/> walks both folders and reads every font file's bytes to get at its
        /// PostScript name, and the change tracker it hangs off fires for <i>any</i> edit to the asset — so
        /// rebuilding unconditionally re-read the whole font library on every keystroke typed into the App
        /// Group field. Nothing else on this page depends on a font, so nothing else needs the scan.
        /// </remarks>
        private void ShowFontPanelsIfFoldersChanged()
        {
            if (FontFolders() != _scanned)
            {
                ShowFontPanels();
            }
        }

        /// <summary>
        /// The two folder settings the panels are a read-out of. Blanks when there is no asset — and when no
        /// build would create the extension, since fonts are bundled into it and nowhere else.
        /// </summary>
        private (string animations, string fonts) FontFolders()
        {
            return _needsExtension && _settings?.targetObject is TappGoSettings settings
                ? (settings.fontAnimationsFolder, settings.fontsFolder)
                : (null, null);
        }

        /// <summary>
        /// Clears <paramref name="panel"/> and decides whether there is a folder worth reporting on.
        /// </summary>
        /// <returns>
        /// <c>true</c> when <paramref name="folder"/> exists and the caller should describe it. <c>false</c>
        /// when the setting is blank (the panel is hidden — most projects bundle no fonts) or names a folder
        /// that isn't there, which the panel reports as the build failure it will be.
        /// </returns>
        private static bool PrepareFolderPanel(VisualElement panel, string folder)
        {
            panel.Clear();

            if (string.IsNullOrWhiteSpace(folder))
            {
                panel.AddToClassList("tapp-hidden");
                return false;
            }

            panel.RemoveFromClassList("tapp-hidden");

            if (Directory.Exists(folder))
            {
                return true;
            }

            // The build hook fails on this, so say the same thing here rather than reporting an empty folder —
            // "nothing in it" and "no folder" are not the same problem.
            panel.Add(new HelpBox(
                $"\"{folder}\" doesn't exist. iOS builds will fail until it does, or until the field is "
                + "cleared.", HelpBoxMessageType.Error));
            return false;
        }

        private void ShowFontAnimations()
        {
            var folder = FontFolders().animations;
            if (_fontAnimations == null || !PrepareFolderPanel(_fontAnimations, folder))
            {
                return;
            }

            var report = FontAnimationInventory.InspectFolder(folder);
            if (report.IsEmpty)
            {
                _fontAnimations.Add(new HelpBox(
                    $"No .ttf or .otf files under \"{folder}\". Put each animation in its own subfolder — "
                    + "SlotFont/SlotFont_0.ttf, SlotFont_1.ttf, … — or clear the field if this app has no font "
                    + "animations.", HelpBoxMessageType.Info));
                return;
            }

            if (report.Animations.Count > 0)
            {
                var list = List(
                    _fontAnimations,
                    report.Animations.Count == 1
                        ? "1 font animation bundled into the extension"
                        : $"{report.Animations.Count} font animations bundled into the extension",
                    report.Animations.Count);

                foreach (var animation in report.Animations)
                {
                    list.Add(Row(animation.Name, animation.Frames.Count == 1
                        ? "1 frame"
                        : $"{animation.Frames.Count} frames"));
                }

                list.Add(Note(
                    "These names are what a Tapp design refers to each animation by — they come from the file "
                    + "names, not from the folders."));
            }

            var gaps = report.Animations
                .Where(one => one.MissingNumbers.Count > 0)
                .Select(one => $"{one.Name} — no file for frame {FontAnimationInventory.Describe(one.MissingNumbers)}")
                .ToList();
            if (gaps.Count > 0)
            {
                _fontAnimations.Add(new HelpBox(
                    "Nothing fails — the frames that are there play in order — but if these went missing, the "
                    + "animation is short by that much.\n\n" + Lines(gaps), HelpBoxMessageType.Warning));
            }

            var doubled = report.Animations
                .Where(one => one.Shadowed.Count > 0)
                .Select(one => $"{one.Name} — {string.Join(", ", one.Shadowed)}")
                .ToList();
            if (doubled.Count > 0)
            {
                _fontAnimations.Add(new HelpBox(
                    "More than one file claims the same frame number. Only one file per number is drawn, so "
                    + "these are bundled and never seen.\n\n" + Lines(doubled), HelpBoxMessageType.Warning));
            }

            if (report.Unnumbered.Count > 0)
            {
                _fontAnimations.Add(new HelpBox(
                    // No angle brackets in any of this text: a UI Toolkit Label parses rich-text tags by
                    // default, so a literal "name_number" placeholder written that way vanishes from the page.
                    "Not named as a frame is (a name, then a number, like SlotFont_0.ttf), so no animation "
                    + "includes them. They are still bundled as ordinary fonts; move them to the Fonts Folder "
                    + "if that is what they are.\n\n" + Lines(report.Unnumbered),
                    HelpBoxMessageType.Warning));
            }
        }

        /// <summary>
        /// Lists the Fonts Folder by the names a design has to ask for, not by file name.
        /// </summary>
        /// <remarks>
        /// The file name is the one thing about a bundled font that doesn't matter: iOS registers it under the
        /// PostScript name stored inside the file, and that is what <c>Font.custom(_:)</c> resolves. So
        /// <c>Inter-Regular.ttf</c> is very often <c>Inter-Regular</c> and never <c>Inter</c>, and a design
        /// that asks for the wrong one gets a system font with no complaint from anywhere. Reading the name
        /// out of each file is the only way this page can be worth more than a directory listing.
        /// </remarks>
        private void ShowFonts()
        {
            var folder = FontFolders().fonts;
            if (_fonts == null || !PrepareFolderPanel(_fonts, folder))
            {
                return;
            }

            var fonts = BundledFontNames.InspectFolder(folder);
            if (fonts.Count == 0)
            {
                _fonts.Add(new HelpBox(
                    $"No .ttf or .otf files under \"{folder}\". Subfolders are fine — Inter/Inter-Regular.ttf, "
                    + "Display/Bungee-Regular.ttf — or clear the field if this app bundles no fonts of its own.",
                    HelpBoxMessageType.Info));
                return;
            }

            var list = List(
                _fonts,
                fonts.Count == 1 ? "1 font bundled into the extension" : $"{fonts.Count} fonts bundled into the extension",
                fonts.Count);

            foreach (var font in fonts)
            {
                list.Add(Row(font.PostScriptName ?? "(no PostScript name)", font.FileName));
            }

            list.Add(Note(
                "Ask for these names in your Tapp design. They are the PostScript names stored inside the "
                + "files, which is what iOS registers them under — the file name has no bearing on it."));

            var unnamed = fonts.Where(font => string.IsNullOrEmpty(font.PostScriptName))
                .Select(font => font.FileName)
                .ToList();
            if (unnamed.Count > 0)
            {
                _fonts.Add(new HelpBox(
                    "These carry no PostScript name, so there is no name a design can ask for them by. Text "
                    + "naming them falls back to a system font.\n\n" + Lines(unnamed),
                    HelpBoxMessageType.Warning));
            }

            var collisions = fonts
                .Where(font => !string.IsNullOrEmpty(font.PostScriptName))
                .GroupBy(font => font.PostScriptName, StringComparer.Ordinal)
                .Where(group => group.Count() > 1)
                .Select(group => $"{group.Key} — {string.Join(", ", group.Select(font => font.FileName))}")
                .ToList();
            if (collisions.Count > 0)
            {
                _fonts.Add(new HelpBox(
                    "More than one file registers under the same name. A design asking for it gets whichever "
                    + "iOS registered first.\n\n" + Lines(collisions), HelpBoxMessageType.Warning));
            }
        }

        /// <summary>
        /// A collapsible, scrolling list added to <paramref name="panel"/>; returns the element rows go into.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Both of these lists are as long as a host's asset folder, which is to say unbounded — a project with
        /// forty fonts pushed the status strip and everything below it off the bottom of the page, and the
        /// Project Settings window would not scroll to reach them. So the list carries its own height: it is
        /// capped by <c>.tapp-fonts__list</c> in the stylesheet and scrolls inside itself, which keeps the page
        /// the same size for forty fonts as for four however the host window behaves.
        /// </para>
        /// <para>
        /// Collapsed from <see cref="CollapseAbove"/> entries, because past that the list is reference material
        /// rather than something to read — the count in the header is the part that was worth seeing. <b>The
        /// warnings deliberately stay outside</b>: a warning folded away is a warning nobody reads, and they
        /// are the whole reason either panel exists.
        /// </para>
        /// </remarks>
        private static VisualElement List(VisualElement panel, string title, int count)
        {
            var foldout = new Foldout { text = title, value = count <= CollapseAbove };
            foldout.AddToClassList("tapp-fonts__foldout");

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("tapp-fonts__list");

            foldout.Add(scroll);
            panel.Add(foldout);
            return scroll;
        }

        /// <summary>
        /// One item per line, cut off at <see cref="MaxWarningLines"/> with a count of the rest.
        /// </summary>
        /// <remarks>
        /// A warning naming forty files is as unreadable as no warning, and it grows the page in exactly the
        /// way the lists above were capped to stop. Six is enough to see the shape of the problem; whoever has
        /// forty of them does not need all forty spelled out to know what to go and look at.
        /// </remarks>
        private static string Lines(IReadOnlyList<string> items)
        {
            var text = string.Join("\n", items.Take(MaxWarningLines));
            return items.Count > MaxWarningLines
                ? $"{text}\n…and {items.Count - MaxWarningLines} more."
                : text;
        }

        private static Label Note(string text)
        {
            var label = new Label(text);
            label.AddToClassList("tapp-fonts__note");
            return label;
        }

        /// <summary>
        /// One line of a font panel: the name that matters, then what it came from, pushed to the right.
        /// </summary>
        /// <remarks>
        /// Both panels lead with the name a design has to use, because that is the thing neither folder shows
        /// you. The detail is right-aligned by a growing spacer rather than by padding the name to a fixed
        /// width — the names here are a host's, and one long enough to overrun a fixed column would either
        /// clip or shove the whole line out of alignment.
        /// </remarks>
        private static VisualElement Row(string name, string detail)
        {
            var row = new VisualElement();
            row.AddToClassList("tapp-fonts__row");

            var nameLabel = new Label(name);
            nameLabel.AddToClassList("tapp-fonts__name");

            var spacer = new VisualElement();
            spacer.AddToClassList("tapp-fonts__spacer");

            var detailLabel = new Label(detail);
            detailLabel.AddToClassList("tapp-fonts__detail");

            row.Add(nameLabel);
            row.Add(spacer);
            row.Add(detailLabel);
            return row;
        }

        private void CreateSettings()
        {
            Directory.CreateDirectory("Assets/Resources");

            var created = ScriptableObject.CreateInstance<TappGoSettings>();

            // Fonts are bundled into the widget extension and nowhere else, so the two folders are made — and
            // the two fields pointed at them — only when something installed makes a build create one. Core
            // alone gets no folders it has no use for; the fields are simply blank if a product arrives later.
            if (ExtensionStaging.NeedsExtension(created))
            {
                CreateWithGitkeep(DefaultFontAnimationsFolder);
                CreateWithGitkeep(DefaultFontsFolder);
                created.fontAnimationsFolder = DefaultFontAnimationsFolder;
                created.fontsFolder = DefaultFontsFolder;
            }

            AssetDatabase.CreateAsset(created, AssetPath);
            AssetDatabase.SaveAssets();

            Bind(created);

            // Select it too: the asset has to be committed, and a host who never sees it in the Project window
            // is the one who forgets to add it to source control.
            Selection.activeObject = created;
        }

        /// <summary>
        /// Creates <paramref name="folder"/> with a placeholder file in it.
        /// </summary>
        /// <remarks>
        /// Git doesn't track an empty folder; without a file in it, a fresh clone would have the settings
        /// asset pointing at a folder that plainly doesn't exist, which the build hook refuses to build with.
        /// </remarks>
        private static void CreateWithGitkeep(string folder)
        {
            Directory.CreateDirectory(folder);
            var keep = Path.Combine(folder, ".gitkeep");
            if (!File.Exists(keep))
            {
                File.WriteAllText(keep, "");
            }
        }
    }
}
