using System.Collections.Generic;

namespace TappGo.Editor.Modules
{
    /// <summary>
    /// What one Tapp product needs from the iOS build, as data.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>For Tapp's own packages, not for host code.</b> Every list is additive and core applies them all in
    /// one pass over the project — see <see cref="TappEditorModule"/> for why a product does not edit the
    /// Xcode project itself. Plain data also means a contribution can be asserted on in an Editor test, with
    /// no Xcode project anywhere.
    /// </para>
    /// <para>
    /// <b>The widget extension's entry point is assembled from the four <c>Bundle…</c> lists.</b> iOS allows
    /// an app one widget extension for all of this, and a Swift target one <c>@main</c> — so the products
    /// cannot each ship a bundle file. Each hands over its lines instead, and core writes the one file. A
    /// product with nothing in <see cref="BundleBody"/> has nothing to render; when no installed product
    /// does, no extension target is created at all.
    /// </para>
    /// </remarks>
    public sealed class TappIosContribution
    {
        /// <summary>
        /// The Swift package products to link into the app, e.g. <c>TappWidgets</c> — and into the extension
        /// too, when this contribution has a <see cref="BundleBody"/>.
        /// </summary>
        /// <remarks>
        /// Linking into the <i>app</i> is what embeds the framework and what loads the surface into the app's
        /// process: native core finds a surface by its Obj-C class name, so a product nothing links answers
        /// <c>surfaceNotLinked</c> however it was configured. Core's own product is always linked. A product
        /// that renders nothing in the extension is left out of it.
        /// </remarks>
        public List<string> SwiftProducts { get; } = new List<string>();

        /// <summary>System frameworks the extension links, e.g. <c>ActivityKit.framework</c>.</summary>
        public List<string> ExtensionFrameworks { get; } = new List<string>();

        /// <summary><c>import</c> lines' module names for the generated bundle file, e.g. <c>TappWidgets</c>.</summary>
        public List<string> BundleImports { get; } = new List<string>();

        /// <summary>
        /// Statements for the bundle's <c>init()</c>, one per entry, already complete Swift. Must not throw:
        /// an error escaping a <c>WidgetBundle</c> initialiser kills the extension, which iOS shows as a blank
        /// card and reports nowhere.
        /// </summary>
        public List<string> BundleSetup { get; } = new List<string>();

        /// <summary>The bundle's <c>body</c> entries, one widget expression per entry.</summary>
        /// <remarks>
        /// An entry here is what makes a product one that renders in the extension. With none, the product
        /// stays out of that target altogether: its <see cref="ExtensionFrameworks"/>,
        /// <see cref="BundleImports"/>, <see cref="BundleSetup"/>, <see cref="BundleDeclarations"/> and
        /// <see cref="ExtensionKeychainSharing"/> are not read.
        /// </remarks>
        public List<string> BundleBody { get; } = new List<string>();

        /// <summary>Top-level declarations to follow the bundle, e.g. an <c>AppIntentsPackage</c>.</summary>
        public List<string> BundleDeclarations { get; } = new List<string>();

        /// <summary>
        /// A folder of Swift the <i>app</i> target compiles, or <c>null</c>. Resolve it with
        /// <see cref="TappEditorModules.PackageFolder"/> against the module's own assembly.
        /// </summary>
        public string AppSourcesRoot { get; set; }

        /// <summary><c>true</c>-valued keys for the app's <c>Info.plist</c>, e.g. <c>NSSupportsLiveActivities</c>.</summary>
        public List<string> AppPlistFlags { get; } = new List<string>();

        /// <summary>
        /// <c>UIBackgroundModes</c> the app needs, e.g. <c>audio</c>. Extended, never replaced. App Review asks
        /// about every one, so a product adds a mode only for a host that switched the feature on.
        /// </summary>
        public List<string> AppBackgroundModes { get; } = new List<string>();

        /// <summary>Whether the app needs the push entitlement (<c>aps-environment</c>).</summary>
        public bool PushNotifications { get; set; }

        /// <summary>
        /// Whether the widget extension needs Keychain Sharing under the App Group, which the app always has.
        /// </summary>
        /// <remarks>
        /// A Home Screen widget authenticates and fetches on its own when WidgetKit renders it, and what it
        /// caches is encrypted under a key kept in the shared Keychain — so without the access group it renders
        /// a placeholder for ever, on a device only and with nothing logged. A Live Activity is the opposite:
        /// its render process cannot reach the Keychain whatever it is entitled to, so a product that renders
        /// nothing else leaves this off and the extension claims nothing it cannot use.
        /// </remarks>
        public bool ExtensionKeychainSharing { get; set; }
    }
}
