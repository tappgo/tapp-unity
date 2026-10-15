#if UNITY_IOS
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using TappGo.Editor.Modules;
using TappGo.Internal;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.iOS.Xcode;
using UnityEditor.iOS.Xcode.Extensions;
using UnityEngine;

namespace TappGo.Editor
{
    /// <summary>
    /// Tapp's one hook into the iOS build — for core and for every Tapp product package installed beside it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Unity regenerates the Xcode project on every Replace build, so nothing a developer adds by hand
    /// survives. This code <i>is</i> the integration: it creates the widget extension target, wires the
    /// frameworks to every target that needs them, and gives each the entitlements it needs — which are
    /// deliberately not the same set.
    /// </para>
    /// <para>
    /// <b>One hook, however many products.</b> iOS gives an app a single widget extension for Live Activities
    /// and widgets alike, so two hooks would mean two targets with one bundle identifier. Each product package
    /// instead <i>describes</i> what it needs as a <see cref="TappIosContribution"/>, and this hook applies
    /// them all. With no product installed there is nothing to render, so no extension is created: core alone
    /// links its framework into the app and stops.
    /// </para>
    /// <para>
    /// <b>One writer, and the order is not negotiable.</b> Everything that touches the project file happens in
    /// <see cref="OnPostprocessBuild"/>: read once, make every edit, write once, and only then hand over to
    /// <c>ProjectCapabilityManager</c>, which re-reads from disk. Flushing a <c>PBXProject</c> that was loaded
    /// before that hand-over silently discards the entitlement wiring — a failure the Simulator cannot show,
    /// because it does not enforce entitlements at all.
    /// </para>
    /// <para>
    /// <b>Nothing native ships in a Tapp package.</b> The frameworks are one Swift package Xcode resolves from
    /// <see cref="NativePin.IosRepositoryUrl"/> at the pinned version, the first time the project builds. This
    /// hook adds that package reference and hangs its products on the three targets that need them: Unity's
    /// <c>UnityFramework</c> (where the shims' <c>import Tapp</c> compiles), the app (which embeds and signs
    /// the dylibs), and the extension (which renders with them).
    /// </para>
    /// <para>
    /// <b>Unity already does the rest.</b> Staging the shims into <c>UnityFramework</c> and setting
    /// <c>SWIFT_VERSION</c> are Unity's own behaviour. Only what it leaves out is done here.
    /// </para>
    /// </remarks>
    internal sealed class TappGoBuildProcessor : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        /// <summary>
        /// A folder of local xcframeworks to wire in place of the Swift package — development only.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The public package only has tagged releases, and a native change has to be tried here before it is
        /// tagged. Pointing this at a folder holding the native repos' builds — <c>Tapp.xcframework</c>, and one
        /// xcframework per installed product package —
        /// makes the hook stage those into the export and link them by hand, the way the package did before
        /// it stopped shipping one. Everything else about the build is identical, so what it proves is the
        /// wrapper — not the release path.
        /// </para>
        /// <para>
        /// Refused under <see cref="ReleaseBuildVariable"/>: a release exported against a local binary would
        /// verify and pass with bytes no customer can resolve.
        /// </para>
        /// </remarks>
        internal const string LocalFrameworkVariable = "TAPP_IOS_LOCAL_XCFRAMEWORK";

        /// <summary>Set by the release and smoke workflows, and what makes a local framework a hard error.</summary>
        internal const string ReleaseBuildVariable = "TAPP_RELEASE_BUILD";

        /// <summary>Where local frameworks are staged inside the export — a folder Unity does not own.</summary>
        private const string LocalFrameworkFolder = "Frameworks/Tapp";

        /// <summary>Where the app target's staged Swift lives inside the export.</summary>
        private const string AppSourcesFolder = "TappApp";

        /// <summary>The oldest iOS the app target may name — the floor the native frameworks link at.</summary>
        /// <remarks>
        /// <b>Lower than the floor the SDK actually runs at, and deliberately so.</b> Native builds the
        /// framework at iOS 15.0 and gates its whole surface at 17.2: below that every entry point returns its
        /// neutral answer and logs one line saying it did nothing. So a host shipping iOS 15 links, launches
        /// and runs — with an SDK that is inert on the old systems, rather than a dependency it cannot resolve
        /// at all, which is what refusing the build here used to cost it. Raising this to match
        /// <see cref="ExtensionDeploymentTarget"/> would take that back.
        /// </remarks>
        private const string MinimumAppDeploymentTarget = "15.0";

        /// <summary>The deployment target the widget extension is built at.</summary>
        /// <remarks>
        /// The SDK's <i>runtime</i> floor, and not the negotiable one: the generated bundle names the products'
        /// widget types, which native annotates <c>@available(iOS 17.2, *)</c>, and an unguarded reference to a
        /// gated type from a target below its floor fails to <b>compile</b> — it is not a runtime surprise.
        /// Nor is there a lower version of this target worth having: nothing Tapp renders exists below 17.2.
        /// Set on the extension only, so the app keeps whatever the developer chose.
        /// </remarks>
        private const string ExtensionDeploymentTarget = "17.2";

        /// <summary>What Unity sets on its own targets; the extension has to match or Swift won't compile.</summary>
        private const string SwiftVersion = "5.0";

        /// <summary>
        /// Late, so other SDKs' hooks land first and this mutates a finished project.
        /// </summary>
        /// <remarks>
        /// It does not make the project file safe from them — a hook that read before this one and writes after
        /// still wins, and nothing here can prevent that. It only means the common case, where each hook reads
        /// and writes in turn, resolves in our favour.
        /// </remarks>
        public int callbackOrder => 100;

        // MARK: - Before the build

        /// <summary>
        /// Rejects a project the extension cannot be built into, before anything is generated.
        /// </summary>
        /// <remarks>
        /// Every check here has a version that would otherwise surface as an Xcode error twenty minutes later,
        /// pointing at a generated file the developer never wrote. A deployment target below the framework's
        /// link floor reads <i>"module 'Tapp' has a minimum deployment target of iOS 15.0"</i>; a missing
        /// team ID reads as a signing failure on a target the developer has never heard of.
        /// </remarks>
        public void OnPreprocessBuild(BuildReport report)
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.iOS)
            {
                return;
            }

            // Checked here so it costs nothing, and again after the build: the summary is not guaranteed to
            // carry the options yet at this point, and the one that matters is the one the build actually ran.
            RefuseAppendBuild(report.summary.options);

            var deploymentTarget = PlayerSettings.iOS.targetOSVersionString;
            if (VersionKey(deploymentTarget) < VersionKey(MinimumAppDeploymentTarget))
            {
                throw new BuildFailedException(
                    $"[Tapp] iOS {MinimumAppDeploymentTarget} or newer is required to link Tapp, and this " +
                    $"project targets {(string.IsNullOrEmpty(deploymentTarget) ? "nothing" : deploymentTarget)}. " +
                    "Set Player Settings → Other Settings → Target minimum iOS Version. " +
                    $"Tapp's surfaces themselves need iOS {ExtensionDeploymentTarget} — below it the SDK " +
                    "links and does nothing.");
            }

            var settings = TappGoSettings.Load();
            if (settings == null)
            {
                throw new BuildFailedException(
                    "[Tapp] No TappGoSettings asset. Open Project Settings → Tapp to create one.");
            }

            var invalid = settings.Validate();
            if (invalid != null)
            {
                throw new BuildFailedException($"[Tapp] {invalid}");
            }

            var mixed = TappPackageVersions.Problem();
            if (mixed != null)
            {
                throw new BuildFailedException($"[Tapp] {mixed}");
            }

            RemovedAutoConfigure.WarnIfStillRelied();

            var contributions = Contributions(settings);

            // The extension's own settings are judged only when this build creates one. With nothing installed
            // that renders — core alone, or Widgets kept for Picture in Picture — its name, its fonts and its
            // signing team apply to nothing, and refusing a build over them would be refusing it for a target
            // the project never gets.
            if (ExtensionStaging.NeedsExtension(contributions))
            {
                var extensionProblem = ExtensionStaging.ExtensionSettingsProblem(settings);
                if (extensionProblem != null)
                {
                    throw new BuildFailedException($"[Tapp] {extensionProblem}");
                }

                if (PlayerSettings.iOS.sdkVersion == iOSSdkVersion.DeviceSDK &&
                    string.IsNullOrEmpty(PlayerSettings.iOS.appleDeveloperTeamID))
                {
                    throw new BuildFailedException(
                        "[Tapp] A device build needs a team ID: the Tapp widget extension is a second target " +
                        "and Xcode cannot pick a signing team for it on its own. Set Player Settings → Other " +
                        "Settings → Signing Team ID.");
                }
            }

            // Judged before the export rather than after: a twenty-minute build that then refuses is the
            // wrong place to learn a variable was set. Reading it here and again in the post-process is
            // deliberate — the second read is the one that acts, and the two never disagree in one process.
            LocalFrameworks(contributions);
        }

        /// <summary>What every installed product package asks of this build, in package-name order.</summary>
        private static List<TappIosContribution> Contributions(TappGoSettings settings)
        {
            return ExtensionStaging.Contributions(settings);
        }

        /// <summary>
        /// The folder of local frameworks to wire instead of the Swift package, or <c>null</c> for the package.
        /// </summary>
        /// <exception cref="BuildFailedException">
        /// If one is set on a release build, or set and missing a framework this build links. Both are
        /// configuration errors on the machine running the build, not in the project, and the message says
        /// which variable to unset or which framework to build.
        /// </exception>
        private static string LocalFrameworks(IReadOnlyList<TappIosContribution> contributions)
        {
            var local = Environment.GetEnvironmentVariable(LocalFrameworkVariable);
            if (string.IsNullOrEmpty(local))
            {
                return null;
            }

            if (Environment.GetEnvironmentVariable(ReleaseBuildVariable) == "1")
            {
                throw new BuildFailedException(
                    $"[Tapp] {LocalFrameworkVariable} is set on a release build. A release resolves the native " +
                    $"frameworks from {NativePin.IosRepositoryUrl} at {NativePin.IosVersion} and nothing else; " +
                    "unset the variable.");
            }

            foreach (var product in ExtensionStaging.NativeProducts(contributions))
            {
                var framework = Path.Combine(local, product + ".xcframework");
                if (!Directory.Exists(framework) || !File.Exists(Path.Combine(framework, "Info.plist")))
                {
                    throw new BuildFailedException(
                        $"[Tapp] {LocalFrameworkVariable} points at {local}, which has no {product}.xcframework. " +
                        "Point it at the folder `make fetch-native` fills, or unset the variable to resolve the " +
                        "Swift package.");
                }
            }

            return local;
        }

        // MARK: - After the build

        /// <summary>
        /// Links the frameworks, and adds the widget extension when an installed product has something to render.
        /// </summary>
        public void OnPostprocessBuild(BuildReport report)
        {
            var summary = report.summary;
            if (summary.platform != BuildTarget.iOS || summary.result == BuildResult.Failed)
            {
                return;
            }

            RefuseAppendBuild(summary.options);

            var settings = TappGoSettings.Load();
            if (settings == null || settings.Validate() != null)
            {
                // Unreachable through a normal build — OnPreprocessBuild already refused. Reachable if a host
                // invokes the post-process directly, and silence there would ship an app with no extension.
                throw new BuildFailedException(
                    "[Tapp] Settings are missing or invalid, so the Xcode project was not set up for Tapp.");
            }

            var output = summary.outputPath;
            var contributions = Contributions(settings);
            var extensionName = ExtensionStaging.NeedsExtension(contributions) ? settings.extensionTargetName : null;
            var appSources = StageAppSources(output, contributions);
            var localFrameworks = LocalFrameworks(contributions);
            var products = ExtensionStaging.NativeProducts(contributions);
            var extensionProducts = ExtensionStaging.ExtensionProducts(contributions);

            var staged = new List<string>();
            if (extensionName != null)
            {
                staged = StageExtensionSources(output, extensionName, settings, contributions);
                staged.AddRange(StageFontFiles(output, extensionName, settings));
            }

            // One pass over the app's Info.plist, before the project work: it carries what the products
            // declare and hands back the versions the extension has to match. The capabilities added below are
            // entitlements-only, so nothing rewrites this file afterwards.
            var version = ConfigureAppPlist(output, contributions, extension: extensionName != null);

            var projectPath = PBXProject.GetPBXProjectPath(output);
            var project = new PBXProject();
            project.ReadFromFile(projectPath);

            var app = project.GetUnityMainTargetGuid();
            var unityFramework = project.GetUnityFrameworkTargetGuid();
            var appEntitlements = project.GetBuildPropertyForAnyConfig(app, "CODE_SIGN_ENTITLEMENTS");
            var extension = extensionName == null
                ? null
                : AddExtensionTarget(project, app, extensionName, staged, contributions);

            foreach (var source in appSources)
            {
                project.AddFileToBuild(app, project.AddFile(source, source, PBXSourceTree.Source));
            }

            if (localFrameworks == null)
            {
                AddSwiftPackage(project, products, extensionProducts,
                    app: app, unityFramework: unityFramework, extension: extension);
            }
            else
            {
                AddLocalFrameworks(project, output, localFrameworks, products, extensionProducts,
                    app: app, unityFramework: unityFramework, extension: extension);
            }

            if (extension != null)
            {
                ConfigureExtension(project, extension, version);
            }

            project.WriteToFile(projectPath);

            // Everything below re-reads the project from disk. Nothing may touch `project` again.
            AddCapabilities(projectPath, settings, app: app, appEntitlements: appEntitlements,
                extensionName: extensionName,
                push: contributions.Any(contribution => contribution.PushNotifications),
                extensionKeychain: ExtensionStaging.Rendering(contributions)
                    .Any(contribution => contribution.ExtensionKeychainSharing),
                development: summary.options.HasFlag(BuildOptions.Development));

            var what = extensionName == null
                ? $"Linked {string.Join(" + ", products)} into {Path.GetFileName(output)} — nothing installed " +
                  "renders in a widget extension, so none was added"
                : $"Added the {extensionName} extension to {Path.GetFileName(output)} with " +
                  $"{string.Join(" + ", extensionProducts)}; the app links {string.Join(" + ", products)}";
            Debug.Log(localFrameworks == null
                ? $"[Tapp] {what}; Xcode resolves {NativePin.IosVersion} from {NativePin.IosRepositoryUrl} on first build."
                : $"[Tapp] {what}, against the LOCAL frameworks in {localFrameworks} — not a release configuration.");
        }

        // MARK: - The native framework

        /// <summary>
        /// Adds the public Swift package and hangs its products on every target that needs them.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Exact version, never a range.</b> The binaries' checksums live in the package manifest at that
        /// tag, and the contract this package was tested against is that tag's. "Whatever is newest" is the
        /// one thing <c>docs/NATIVE-CONTRACT.md</c> forbids.
        /// </para>
        /// <para>
        /// <b>Three targets, three reasons.</b> <c>UnityFramework</c> compiles the shims, whose
        /// <c>import Tapp</c> is resolved by the core product and nothing else — the whole bridge facade is in
        /// core, so no shim ever names a surface. The app target gets <i>every</i> product: a dynamic
        /// product linked to an <i>application</i> is what Xcode embeds and signs into
        /// <c>App.app/Frameworks</c> — a framework target's dependencies are not embedded transitively, so
        /// without this the app links, archives and dies at launch under <c>dyld</c> — and it is also the only
        /// thing that loads a surface into the app's process at all. Core finds a surface by its Obj-C class
        /// name, so a surface nothing links answers <c>surfaceNotLinked</c> however it was configured. The
        /// extension links core and the products that render in it
        /// (<see cref="ExtensionStaging.ExtensionProducts"/>) — a product installed for what it does in the
        /// app alone stays out — and reaches the app's copies through the rpath
        /// <see cref="ConfigureExtension"/> sets.
        /// </para>
        /// <para>
        /// <c>weak</c> is <c>false</c> everywhere. A weak link only hides a missing dylib until the first
        /// call, which is the worst of the failure modes this hook exists to prevent.
        /// </para>
        /// </remarks>
        private static void AddSwiftPackage(
            PBXProject project, List<string> products, List<string> extensionProducts,
            string app, string unityFramework, string extension)
        {
            var package = project.AddRemotePackageReferenceAtVersion(NativePin.IosRepositoryUrl, NativePin.IosVersion);
            project.AddRemotePackageFrameworkToProject(unityFramework, NativePin.IosCoreProductName, package, false);
            foreach (var product in products)
            {
                project.AddRemotePackageFrameworkToProject(app, product, package, false);
                if (extension != null && extensionProducts.Contains(product))
                {
                    project.AddRemotePackageFrameworkToProject(extension, product, package, false);
                }
            }
        }

        /// <summary>
        /// Stages local xcframeworks into the export and wires them the way the package would have been.
        /// </summary>
        /// <remarks>
        /// The same table as <see cref="AddSwiftPackage"/>: core into <c>UnityFramework</c>, every product into
        /// the app, and those that render into the extension. Embedded and signed in the app only — an appex
        /// with its own copy is an
        /// App Store Connect rejection. Unity no longer stages anything under <c>Runtime/Plugins/iOS</c> for
        /// this package, so the link into <c>UnityFramework</c> that Unity once did on its own is done here
        /// too.
        /// </remarks>
        private static void AddLocalFrameworks(
            PBXProject project, string output, string source, List<string> products, List<string> extensionProducts,
            string app, string unityFramework, string extension)
        {
            var folder = Path.Combine(output, LocalFrameworkFolder);
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, true);
            }

            Directory.CreateDirectory(folder);

            foreach (var product in products)
            {
                var name = product + ".xcframework";
                var path = $"{LocalFrameworkFolder}/{name}";
                CopyDirectory(Path.Combine(source, name), Path.Combine(folder, name));

                var framework = project.AddFile(path, path, PBXSourceTree.Source);
                if (product == NativePin.IosCoreProductName)
                {
                    project.AddFileToBuild(unityFramework, framework);
                }

                project.AddFileToBuild(app, framework);
                if (extension != null && extensionProducts.Contains(product))
                {
                    project.AddFileToBuild(extension, framework);
                }

                project.AddFileToEmbedFrameworks(app, framework);
            }

            var searchPath = $"$(PROJECT_DIR)/{LocalFrameworkFolder}";
            foreach (var target in new[] { app, unityFramework, extension }.Where(guid => guid != null))
            {
                project.AddBuildProperty(target, "FRAMEWORK_SEARCH_PATHS", "$(inherited)");
                project.AddBuildProperty(target, "FRAMEWORK_SEARCH_PATHS", searchPath);
            }

            Debug.LogWarning(
                $"[Tapp] {LocalFrameworkVariable} is set: the export links {string.Join(", ", products)} from " +
                $"{source} instead of resolving {NativePin.IosVersion} from {NativePin.IosRepositoryUrl}. " +
                "Development only.");
        }

        private static void CopyDirectory(string source, string destination)
        {
            Directory.CreateDirectory(destination);
            foreach (var file in Directory.GetFiles(source))
            {
                File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), true);
            }

            foreach (var folder in Directory.GetDirectories(source))
            {
                CopyDirectory(folder, Path.Combine(destination, Path.GetFileName(folder)));
            }
        }

        // MARK: - Sources

        /// <summary>
        /// Stages the extension's <c>Info.plist</c> and writes its generated entry point.
        /// </summary>
        /// <returns>The staged Swift files, relative to the project root.</returns>
        private static List<string> StageExtensionSources(
            string output,
            string extensionName,
            TappGoSettings settings,
            IReadOnlyList<TappIosContribution> contributions)
        {
            var tokens = new Dictionary<string, string>
            {
                ["__TAPP_DISPLAY_NAME__"] = string.IsNullOrWhiteSpace(settings.extensionDisplayName)
                    ? Application.productName
                    : settings.extensionDisplayName,
            };

            var folder = Path.Combine(output, extensionName);
            ExtensionStaging.Stage(ExtensionStaging.TemplateRoot(ExtensionStaging.ExtensionTemplatePath), folder, tokens);
            File.WriteAllText(
                Path.Combine(folder, ExtensionStaging.BundleFileName),
                ExtensionStaging.BundleSource(contributions));

            // Only the Swift file is compiled. Info.plist is reached through INFOPLIST_FILE, and adding it to
            // a build phase would copy it into the bundle a second time, as a resource.
            return new List<string> { $"{extensionName}/{ExtensionStaging.BundleFileName}" };
        }

        /// <summary>
        /// Stages the Swift each product wants the <i>app</i> target to compile.
        /// </summary>
        /// <remarks>
        /// One subfolder per product, named after the product it links, so two products can each ship a file
        /// called the same thing. The folder is emptied first, so a Replace build does not keep the last
        /// build's copy of a package that has since been removed.
        /// </remarks>
        /// <returns>The staged Swift files, relative to the project root.</returns>
        private static List<string> StageAppSources(string output, IReadOnlyList<TappIosContribution> contributions)
        {
            var root = Path.Combine(output, AppSourcesFolder);
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }

            Directory.CreateDirectory(root);

            var staged = new List<string>();
            for (var index = 0; index < contributions.Count; index++)
            {
                var contribution = contributions[index];
                if (string.IsNullOrEmpty(contribution.AppSourcesRoot))
                {
                    continue;
                }

                var name = contribution.SwiftProducts.Count > 0
                    ? contribution.SwiftProducts[0]
                    : index.ToString(CultureInfo.InvariantCulture);
                staged.AddRange(ExtensionStaging
                    .Stage(contribution.AppSourcesRoot, Path.Combine(root, name), new Dictionary<string, string>())
                    .Where(path => path.EndsWith(".swift", StringComparison.OrdinalIgnoreCase))
                    .Select(path => $"{AppSourcesFolder}/{name}/{path}"));
            }

            return staged;
        }

        /// <summary>
        /// Stages a host's own font files — <see cref="TappGoSettings.fontAnimationsFolder"/> and
        /// <see cref="TappGoSettings.fontsFolder"/> — and declares them in the extension's
        /// <c>Info.plist</c>.
        /// </summary>
        /// <remarks>
        /// Content, not SDK behavior (see CLAUDE.md §1): staged from wherever the host put them in their own
        /// project, never from this package. Both folders are optional; an unset one stages nothing.
        /// </remarks>
        /// <returns>The staged font files, relative to the project root, ready to add to the extension target.</returns>
        private static List<string> StageFontFiles(string output, string extensionName, TappGoSettings settings)
        {
            var animationFrames = FontFileStaging.Stage(
                ResolveProjectPath(settings.fontAnimationsFolder),
                Path.Combine(output, extensionName, "Fonts", "Animations"),
                "Font Animations Folder");
            // Straight into Fonts/, with the animations in Fonts/Animations/ beneath it. Naming this one after
            // its setting would make it Fonts/Fonts, and inventing a third word for it — Fallback, Regular,
            // Text — would mean the generated project called it something the settings page doesn't.
            var bundledFonts = FontFileStaging.Stage(
                ResolveProjectPath(settings.fontsFolder),
                Path.Combine(output, extensionName, "Fonts"),
                "Fonts Folder");

            // Each call only checks collisions within its own folder — Xcode still flattens both sets into the
            // same bundle, so a name shared between the two would silently replace one with the other there.
            var collision = animationFrames.FirstOrDefault(name =>
                bundledFonts.Contains(name, StringComparer.OrdinalIgnoreCase));
            if (collision != null)
            {
                throw new BuildFailedException(
                    $"[Tapp] \"{collision}\" is in both the Font Animations Folder and the Fonts Folder — " +
                    "Xcode bundles them flat, so one would silently replace the other. Rename one.");
            }

            WarnAboutFramesThatWontAnimate(animationFrames);
            WarnAboutFontsThatCannotBeAskedFor(ResolveProjectPath(settings.fontsFolder));
            ConfigureExtensionFontsPlist(output, extensionName, animationFrames.Concat(bundledFonts).ToList());

            return animationFrames.Select(name => $"{extensionName}/Fonts/Animations/{name}")
                .Concat(bundledFonts.Select(name => $"{extensionName}/Fonts/{name}"))
                .ToList();
        }

        /// <summary>
        /// Logs the staged animation frames that will be bundled and then never drawn.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Warnings, not a failed build. Bundling a font that takes part in no animation is legitimate — it is
        /// an ordinary font — so the only thing wrong here is a host who believed otherwise, and the belief is
        /// what this corrects. Project Settings → Tapp says the same thing while there is still time to fix it;
        /// this is for the build nobody opened that page before.
        /// </para>
        /// <para>
        /// Silent otherwise: a project with a tidy folder of frames gets no output at all.
        /// </para>
        /// </remarks>
        private static void WarnAboutFramesThatWontAnimate(IReadOnlyList<string> animationFrames)
        {
            var report = FontAnimationInventory.Inspect(animationFrames);

            foreach (var animation in report.Animations.Where(one => one.MissingNumbers.Count > 0))
            {
                Debug.LogWarning(
                    $"[Tapp] The \"{animation.Name}\" font animation has no file for frame "
                    + $"{FontAnimationInventory.Describe(animation.MissingNumbers)}. The frames that are there "
                    + "play in order, so nothing fails — but if those went missing, the animation is short by "
                    + "that much.");
            }

            foreach (var animation in report.Animations.Where(one => one.Shadowed.Count > 0))
            {
                Debug.LogWarning(
                    $"[Tapp] The \"{animation.Name}\" font animation has more than one file for the same frame "
                    + $"number ({string.Join(", ", animation.Shadowed)}). Only one file per number is drawn, so "
                    + "these are bundled and never seen.");
            }

            if (report.Unnumbered.Count > 0)
            {
                Debug.LogWarning(
                    $"[Tapp] {string.Join(", ", report.Unnumbered)} sit in the Font Animations Folder but are "
                    + "not named as a frame is (a name, then a number, like SlotFont_0.ttf), so no animation "
                    + "includes them. They are still bundled as ordinary fonts.");
            }
        }

        /// <summary>
        /// Logs the bundled fonts a design has no way to ask for.
        /// </summary>
        /// <remarks>
        /// A bundled font is looked up by the PostScript name stored inside the file, so one that carries no
        /// PostScript name registers as nothing a design can name, and two that carry the same one are one
        /// font as far as the renderer is concerned. Neither fails anything: the text falls back to a system
        /// font, on a device, with no message. Warnings for the same reason the frame ones are — the file is
        /// bundled either way and might be there on purpose.
        /// </remarks>
        private static void WarnAboutFontsThatCannotBeAskedFor(string fontsFolder)
        {
            var fonts = BundledFontNames.InspectFolder(fontsFolder);

            var unnamed = fonts.Where(font => string.IsNullOrEmpty(font.PostScriptName))
                .Select(font => font.FileName)
                .ToList();
            if (unnamed.Count > 0)
            {
                Debug.LogWarning(
                    $"[Tapp] {string.Join(", ", unnamed)} in the Fonts Folder carry no PostScript name, so a "
                    + "design has no name to ask for them by. They are bundled, and text that names them falls "
                    + "back to a system font.");
            }

            foreach (var shared in fonts
                         .Where(font => !string.IsNullOrEmpty(font.PostScriptName))
                         .GroupBy(font => font.PostScriptName, StringComparer.Ordinal)
                         .Where(group => group.Count() > 1))
            {
                Debug.LogWarning(
                    $"[Tapp] {string.Join(", ", shared.Select(font => font.FileName))} in the Fonts Folder all "
                    + $"register as \"{shared.Key}\". A design asking for that name gets whichever iOS "
                    + "registered first.");
            }
        }

        // MARK: - The extension target

        /// <summary>
        /// Creates the extension target, or finds it if a hook that ran earlier already did.
        /// </summary>
        private static string AddExtensionTarget(
            PBXProject project,
            string app,
            string extensionName,
            IEnumerable<string> sources,
            IReadOnlyList<TappIosContribution> contributions)
        {
            var infoPlist = $"{extensionName}/Info.plist";
            var bundleId = $"{PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.iOS)}.{extensionName}";

            var extension = project.AddAppExtension(app, extensionName, bundleId, infoPlist);

            foreach (var source in sources)
            {
                project.AddFileToBuild(extension, project.AddFile(source, source, PBXSourceTree.Source));
            }

            // Swift auto-links what it imports, so these are belt and braces — but an explicit reference is
            // what makes the dependency visible in the project a developer opens to debug it.
            var frameworks = new[] { "WidgetKit.framework", "SwiftUI.framework" }
                .Concat(ExtensionStaging.Rendering(contributions)
                    .SelectMany(contribution => contribution.ExtensionFrameworks))
                .Distinct(StringComparer.Ordinal);
            foreach (var name in frameworks)
            {
                project.AddFrameworkToProject(extension, name, false);
            }

            return extension;
        }

        /// <summary>
        /// Refuses an Append build, which this hook cannot apply twice.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Append hands back a project this hook has already modified, and every edit here is additive: a second
        /// extension target with the same bundle identifier, the source file in the compile phase twice, two
        /// embed entries for one framework, the App Group listed twice. Some of those fail loudly, and the ones
        /// that don't fail at submission instead.
        /// </para>
        /// <para>
        /// <b>Detecting it would be better than refusing it, and cannot be done.</b> Unity 6 keeps
        /// <c>GetAllTargetGuids</c> and <c>FindTargetGuidByName</c> internal, so there is no supported way to
        /// ask whether the target already exists — and text-matching the project file to decide would be
        /// guessing at a format Unity is free to change. Refusing is the honest option: Replace regenerates the
        /// project, which is the model this whole hook is built on.
        /// </para>
        /// </remarks>
        private static void RefuseAppendBuild(BuildOptions options)
        {
            if (!options.HasFlag(BuildOptions.AcceptExternalModificationsToPlayer))
            {
                return;
            }

            throw new BuildFailedException(
                "[Tapp] Append builds are not supported — choose Replace. Every edit Tapp makes to the " +
                "generated Xcode project is additive, and making them again on a project that already has " +
                "them produces duplicate targets, package references and build phases that fail at " +
                "submission rather than here.");
        }

        /// <summary>
        /// Sets what the extension needs and nothing Unity already handles.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>`ARCHS` is deliberately not set.</b> Inheriting it is what keeps a simulator build coherent —
        /// pinning the extension to <c>arm64</c> while the app builds <c>x86_64</c> produces a bundle whose
        /// appex does not match its host, and Xcode links both without complaint.
        /// </para>
        /// <para>
        /// <b>The rpath entry is the one that bites.</b> An appex runs from
        /// <c>App.app/PlugIns/X.appex</c> while the framework lives in <c>App.app/Frameworks</c>, so without
        /// <c>@executable_path/../../Frameworks</c> the extension launches into a <c>dyld</c> failure — which
        /// the system shows as a blank Lock Screen card and reports nowhere.
        /// </para>
        /// </remarks>
        private static void ConfigureExtension(
            PBXProject project, string extension, (string shortVersion, string buildNumber) version)
        {
            project.SetBuildProperty(extension, "IPHONEOS_DEPLOYMENT_TARGET", ExtensionDeploymentTarget);
            project.SetBuildProperty(extension, "SWIFT_VERSION", SwiftVersion);
            project.SetBuildProperty(extension, "TARGETED_DEVICE_FAMILY", "1,2");
            project.SetBuildProperty(extension, "PRODUCT_NAME", "$(TARGET_NAME)");

            // The staged Info.plist reaches these through $(MARKETING_VERSION) and $(CURRENT_PROJECT_VERSION),
            // and an unset build setting expands to an empty string rather than failing — which installd
            // rejects on the device with "does not have a CFBundleVersion key with a non-zero length string
            // value". Nothing catches it earlier: the archive builds, signs and validates.
            project.SetBuildProperty(extension, "MARKETING_VERSION", version.shortVersion);
            project.SetBuildProperty(extension, "CURRENT_PROJECT_VERSION", version.buildNumber);

            // Embedded in the app, so it must not be archived as a product of its own.
            project.SetBuildProperty(extension, "SKIP_INSTALL", "YES");

            // No FRAMEWORK_SEARCH_PATHS: the Swift package supplies its own, and a local framework adds the
            // one it needs in AddLocalFramework.
            project.SetBuildProperty(extension, "LD_RUNPATH_SEARCH_PATHS", "$(inherited)");
            project.AddBuildProperty(extension, "LD_RUNPATH_SEARCH_PATHS", "@executable_path/Frameworks");
            project.AddBuildProperty(extension, "LD_RUNPATH_SEARCH_PATHS", "@executable_path/../../Frameworks");

            var team = PlayerSettings.iOS.appleDeveloperTeamID;
            if (!string.IsNullOrEmpty(team))
            {
                project.SetBuildProperty(extension, "DEVELOPMENT_TEAM", team);
            }
        }

        /// <summary>
        /// Writes what the installed products declare into the app's <c>Info.plist</c>, and reads back the
        /// version and build number the extension has to match.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Both halves in one read-modify-write, because they are the same file: two independent readers of one
        /// plist in one hook is the shape that lets the second silently drop what the first wrote.
        /// </para>
        /// <para>
        /// The versions are read from the generated plist rather than from <c>PlayerSettings</c>, so the two
        /// bundles agree by construction whatever Unity chose to write. An appex whose version differs from its
        /// host is rejected at submission, and an empty one is rejected at install.
        /// </para>
        /// <para>
        /// Missing values throw rather than defaulting — when there is an extension to copy them onto. A
        /// default would paper over a host project whose own app bundle is malformed, and put a version on the
        /// extension that its app doesn't have — trading a clear failure here for a confusing one at App Store
        /// validation. With no extension the two values are not this hook's to judge, and nothing is refused.
        /// </para>
        /// <para>
        /// <c>UIBackgroundModes</c> is extended rather than replaced: another SDK's hook may already have put
        /// a mode there.
        /// </para>
        /// </remarks>
        private static (string shortVersion, string buildNumber) ConfigureAppPlist(
            string output, IReadOnlyList<TappIosContribution> contributions, bool extension)
        {
            var path = Path.Combine(output, "Info.plist");
            var plist = new PlistDocument();
            plist.ReadFromFile(path);

            var shortVersion = PlistString(plist.root, "CFBundleShortVersionString");
            var buildNumber = PlistString(plist.root, "CFBundleVersion");

            // The two values exist to be copied onto the extension, so with none there is nothing to refuse.
            if (extension && (string.IsNullOrEmpty(shortVersion) || string.IsNullOrEmpty(buildNumber)))
            {
                throw new BuildFailedException(
                    "[Tapp] The app's Info.plist has no CFBundleShortVersionString or CFBundleVersion, so the " +
                    "Tapp widget extension has no version to match. Set Version and Build in " +
                    "Player Settings → Identification.");
            }

            // Only what an installed product asked for: each of these is a claim App Review can read, and an
            // app with no Live Activity code has no business declaring that it supports them.
            foreach (var flag in contributions.SelectMany(contribution => contribution.AppPlistFlags))
            {
                plist.root.SetBoolean(flag, true);
            }

            var wanted = contributions.SelectMany(contribution => contribution.AppBackgroundModes).ToList();
            if (wanted.Count > 0)
            {
                var modes = plist.root.values.TryGetValue("UIBackgroundModes", out var existing)
                    ? existing as PlistElementArray
                    : null;
                modes = modes ?? plist.root.CreateArray("UIBackgroundModes");
                foreach (var mode in wanted)
                {
                    if (!modes.values.Any(present => (present as PlistElementString)?.value == mode))
                    {
                        modes.AddString(mode);
                    }
                }
            }

            plist.WriteToFile(path);

            return (shortVersion, buildNumber);
        }

        /// <summary>
        /// Declares the extension's bundled font files — animation frames and a host's own fonts alike — in its
        /// own <c>Info.plist</c>.
        /// </summary>
        /// <remarks>
        /// A <c>UIAppFonts</c> entry is what makes a bundled font available at launch — the only registration
        /// WidgetKit's on-device Lock Screen renderer honors for a Live Activity's frame fonts. Registering them
        /// only at runtime (which the SDK also does, defensively) is not enough on its own; see
        /// <c>FontRegistrar.isAlreadyAvailable</c> in the native SDK. A no-op when nothing was staged, so a host
        /// with no bundled fonts gets no <c>UIAppFonts</c> key at all.
        /// </remarks>
        private static void ConfigureExtensionFontsPlist(string output, string extensionName, IReadOnlyList<string> fontFileNames)
        {
            if (fontFileNames.Count == 0)
            {
                return;
            }

            var path = Path.Combine(output, extensionName, "Info.plist");
            var plist = new PlistDocument();
            plist.ReadFromFile(path);

            var fonts = plist.root.CreateArray("UIAppFonts");
            foreach (var name in fontFileNames)
            {
                fonts.AddString(name);
            }

            plist.WriteToFile(path);
        }

        /// <summary>Reads a string entry, treating a missing or non-string one as absent.</summary>
        private static string PlistString(PlistElementDict dict, string key)
        {
            return dict.values.TryGetValue(key, out var element)
                ? (element as PlistElementString)?.value
                : null;
        }

        // MARK: - Entitlements

        /// <summary>
        /// Gives each target the entitlements it needs — which are not the same set.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The app gets the App Group <b>and</b> Keychain access filed under it, because auth tokens live in
        /// the Keychain under the App Group as the access group. The extension always gets the App Group, and
        /// Keychain access <b>only when a product asks for it</b>
        /// (<see cref="TappIosContribution.ExtensionKeychainSharing"/>). A Home Screen widget does: it
        /// authenticates on its own and decrypts its cache with a key kept there, so without the access group
        /// it renders a placeholder for ever. A Live Activity does not: it is rendered by a process the system
        /// launches for a Lock Screen presentation, and that process cannot reliably reach the data-protection
        /// Keychain — the read fails with <c>errSecNotAvailable</c> whatever it is entitled to. What it renders
        /// is stored unencrypted for exactly that reason, so on a Live Activities-only build the entitlement
        /// would be a claim with nothing behind it.
        /// </para>
        /// <para>
        /// An existing entitlements file is <b>extended, not replaced</b>. Another SDK's hook may have created
        /// one already, and pointing the target at a fresh file of ours would strip whatever it put there
        /// while leaving its build succeeding.
        /// </para>
        /// </remarks>
        private static void AddCapabilities(
            string projectPath,
            TappGoSettings settings,
            string app,
            string appEntitlements,
            string extensionName,
            bool push,
            bool extensionKeychain,
            bool development)
        {
            var group = settings.appGroupIdentifier;

            // The prefix is required and is not added for us: the access group is the App Group qualified by
            // the team's identifier prefix, which Xcode expands at signing time.
            var keychainGroups = new[] { $"$(AppIdentifierPrefix){group}" };

            // With no extension there is no folder of ours to keep a new entitlements file in but the app
            // sources one, which StageAppSources always creates.
            var ownFolder = extensionName ?? AppSourcesFolder;

            var appManager = new ProjectCapabilityManager(
                projectPath,
                string.IsNullOrEmpty(appEntitlements) ? $"{ownFolder}/App.entitlements" : appEntitlements,
                targetName: null,
                targetGuid: app);

            appManager.AddAppGroups(new[] { group });

            appManager.AddKeychainSharing(keychainGroups);

            if (push)
            {
                // Only produces `aps-environment`, which the SDK reads to choose an APNs host. The provisioning
                // profile is what actually decides it at signing, so this is a default rather than a promise.
                appManager.AddPushNotifications(development);
            }

            appManager.WriteToFile();

            if (extensionName == null)
            {
                return;
            }

            var extensionManager = new ProjectCapabilityManager(
                projectPath,
                $"{extensionName}/{extensionName}.entitlements",
                extensionName);
            extensionManager.AddAppGroups(new[] { group });
            if (extensionKeychain)
            {
                extensionManager.AddKeychainSharing(keychainGroups);
            }

            extensionManager.WriteToFile();
        }

        // MARK: - Internals

        /// <summary>See <see cref="ExtensionStaging.ResolveProjectPath"/>.</summary>
        private static string ResolveProjectPath(string relativePath)
        {
            return ExtensionStaging.ResolveProjectPath(relativePath);
        }

        /// <summary>
        /// Reduces a version string to a single comparable number, <c>major * 1000 + minor</c>, or <c>0</c>
        /// when it can't be read.
        /// </summary>
        /// <remarks>
        /// Major alone is not enough: the floor it compares against carries a minor component that decides
        /// the answer, so 14.5 must fail where 15.0 passes. That mattered more when the floor was 17.2 and
        /// 17.0 had to fail too, and it stays because a floor with a minor is the kind this keeps meeting.
        /// An unreadable setting reads as too old, so the build fails with an explanation rather than
        /// reaching Xcode without one.
        /// </remarks>
        private static int VersionKey(string version)
        {
            if (string.IsNullOrEmpty(version))
            {
                return 0;
            }

            var parts = version.Split('.');
            return VersionComponent(parts, 0) * 1000 + VersionComponent(parts, 1);
        }

        private static int VersionComponent(string[] parts, int index)
        {
            return index < parts.Length &&
                   int.TryParse(parts[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : 0;
        }
    }
}
#endif
