using UnityEngine;
using UnityEngine.Serialization;

namespace TappGo
{
    /// <summary>
    /// Your Tapp configuration, edited in <b>Project Settings → Tapp</b> and read at launch and at build time.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Stored as a single asset at <c>Assets/Resources/TappGoSettings.asset</c>. It lives in your project, not
    /// in the package, so it survives upgrades — which is also why every field here has a default: a field
    /// added in a later version must not erase what you already set.
    /// </para>
    /// <para>
    /// The iOS build hook reads this too. A missing or empty <see cref="appGroupIdentifier"/> fails the build
    /// with a message rather than producing an app whose Live Activities silently render nothing.
    /// </para>
    /// </remarks>
    public sealed class TappGoSettings : ScriptableObject
    {
        /// <summary>Where the asset lives, and the name <c>Resources.Load</c> looks for.</summary>
        internal const string ResourceName = "TappGoSettings";

        [Header("Required")]
        [Tooltip("The App Group shared by your app and the Tapp widget extension. Must start with \"group.\".\n\n" +
                 "This is the only channel between them — get it wrong and every Tapp surface renders empty, " +
                 "with no error on the Simulator.")]
        public string appGroupIdentifier = "";

        [Tooltip("Your Tapp app id, from the Tapp back office. Required to use Live Activities.")]
        public string liveActivityAppId = "";

        [Header("Live Activities")]
        [Tooltip("Register for push-to-start and push updates. Leave on unless Tapp tells you otherwise.")]
        public bool pushEnabled = true;

        [Header("Startup")]
        [Tooltip("Configure the SDK automatically before the first scene loads.\n\n" +
                 "Turn this off if you need to configure after your own login flow — then call " +
                 "Tapp.Configure() yourself.")]
        public bool autoConfigureOnLaunch = true;

        [Header("iOS build")]
        [Tooltip("The name of the widget extension target Tapp adds to your Xcode project. " +
                 "Changing it after release orphans the extension the user already installed.")]
        public string extensionTargetName = "TappExtension";

        [Tooltip("Shown under the Live Activity on the Lock Screen. Defaults to your product name.")]
        public string extensionDisplayName = "";

        [Header("Fonts")]
        [Tooltip("Project-relative path to the base folder holding this app's frame-font animations — one " +
                 "subfolder per animation:\n\n" +
                 "    Assets/TappAssets/FontAnimations/SlotFont/SlotFont_0.ttf, SlotFont_1.ttf, …\n" +
                 "    Assets/TappAssets/FontAnimations/CoinSpin/CoinSpin_0.ttf, CoinSpin_1.ttf, …\n\n" +
                 "Every .ttf/.otf under it, at any depth, is bundled into the widget extension and declared " +
                 "in its Info.plist. \"Create Settings\" points this at Assets/TappAssets/FontAnimations and " +
                 "creates it.\n\n" +
                 "The subfolders are for your own tidiness. An animation is identified by the name its frame " +
                 "files share — \"SlotFont\", the name a Tapp design refers to it by — never by the folder " +
                 "holding them, so two animations need two different file-name stems. The panel below lists " +
                 "what Tapp found.\n\n" +
                 "Optional. A font animation is content, not part of the SDK — Tapp itself only ships the " +
                 "shared mask font every animation uses. Leave empty if this app has none.")]
        public string fontAnimationsFolder = "";

        [Tooltip("Project-relative path to the base folder holding this app's own fonts — subfolders as you " +
                 "like, one per family or however you keep them:\n\n" +
                 "    Assets/TappAssets/Fonts/Inter/Inter-Regular.ttf, Inter-Bold.ttf\n" +
                 "    Assets/TappAssets/Fonts/Display/Bungee-Regular.ttf\n\n" +
                 "Every .ttf/.otf under it, at any depth, is bundled into the widget extension and declared " +
                 "in its Info.plist, the same way as font animations above. \"Create Settings\" points this " +
                 "at Assets/TappAssets/Fonts and creates it.\n\n" +
                 "A bundled font registers under the PostScript name stored inside the file, and that — not " +
                 "the file name — is what your Tapp design must ask for. Inter-Regular.ttf is usually " +
                 "\"Inter-Regular\", not \"Inter\". The panel below reads each file and tells you.\n\n" +
                 "Optional, and unrelated to font animations: a card's text fonts are normally downloaded and " +
                 "cached by the SDK itself from the Tapp back office. Set this only if you want a font " +
                 "available before that first download completes, or while offline.")]
        // Renamed from fallbackFontsFolder — "fallback" described one reason to use it, not what it is, and
        // read as though the fonts in it were second choices. FormerlySerializedAs is what makes that a rename
        // rather than a field removed and a field added: without it Unity reads no value for the new name and
        // writes the default over whatever a host had configured, which is the upgrade this asset exists to
        // survive (§7).
        [FormerlySerializedAs("fallbackFontsFolder")]
        public string fontsFolder = "";

        /// <summary>
        /// The single settings asset, or <c>null</c> if none exists.
        /// </summary>
        /// <remarks>
        /// <para>
        /// In the Editor, logs an error if more than one exists anywhere in <c>Resources</c>. Two assets is not
        /// a harmless duplicate: <c>Resources.Load</c> returns whichever it finds first, so the build and the
        /// runtime can silently read different configurations.
        /// </para>
        /// <para>
        /// <b>That sweep is Editor-only, deliberately.</b> <c>Resources.LoadAll&lt;T&gt;("")</c> walks the
        /// host's entire <c>Resources</c> tree, and this runs at <c>BeforeSceneLoad</c> on every launch — a
        /// game with a large <c>Resources</c> folder would pay that startup cost forever, for a check that can
        /// only be acted on while editing. A player loads the one asset by name. Nothing is lost: a duplicate
        /// cannot appear between the Editor noticing and the build, because the build is made from the Editor.
        /// </para>
        /// </remarks>
        public static TappGoSettings Load()
        {
#if UNITY_EDITOR
            var all = Resources.LoadAll<TappGoSettings>("");
            if (all.Length > 1)
            {
                Debug.LogError(
                    $"[Tapp] Found {all.Length} TappGoSettings assets. Keep exactly one, at " +
                    "Assets/Resources/TappGoSettings.asset — Resources.Load picks one arbitrarily, so the " +
                    "build and the running app can disagree about your App Group.");
            }

            return all.Length > 0 ? all[0] : Resources.Load<TappGoSettings>(ResourceName);
#else
            return Resources.Load<TappGoSettings>(ResourceName);
#endif
        }

        /// <summary>
        /// Why these settings can't be used yet, or <c>null</c> if they're usable.
        /// </summary>
        /// <remarks>
        /// Shared by the runtime and the build hook so both reject the same things for the same reasons — a
        /// build that passes and a launch that fails would be the worst of both.
        /// </remarks>
        internal string Validate()
        {
            if (string.IsNullOrWhiteSpace(appGroupIdentifier))
            {
                return "appGroupIdentifier is empty. Set it in Project Settings → Tapp.";
            }

            // Ordinal, not the culture-sensitive default: an App Group is an identifier, and whether one
            // starts with "group." is not a question about the developer's locale.
            if (!appGroupIdentifier.StartsWith("group.", System.StringComparison.Ordinal))
            {
                return $"appGroupIdentifier \"{appGroupIdentifier}\" must start with \"group.\".";
            }

            if (string.IsNullOrWhiteSpace(liveActivityAppId))
            {
                return "liveActivityAppId is empty. Set it in Project Settings → Tapp.";
            }

            return null;
        }
    }
}
