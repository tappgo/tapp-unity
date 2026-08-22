#if UNITY_IOS
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.iOS.Xcode;
using UnityEditor.iOS.Xcode.Extensions;
using UnityEngine;

namespace TappGo.Editor
{
    /// <summary>
    /// The package's one hook into the iOS build.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Unity regenerates the Xcode project on every Replace build, so nothing a developer adds by hand
    /// survives. This code <i>is</i> the integration: it creates the Live Activity extension target, wires the
    /// framework to both targets, and gives each the entitlements it needs — which are deliberately not the
    /// same set.
    /// </para>
    /// <para>
    /// <b>One writer, and the order is not negotiable.</b> Everything that touches the project file happens in
    /// <see cref="OnPostprocessBuild"/>: read once, make every edit, write once, and only then hand over to
    /// <c>ProjectCapabilityManager</c>, which re-reads from disk. Flushing a <c>PBXProject</c> that was loaded
    /// before that hand-over silently discards the entitlement wiring — a failure the Simulator cannot show,
    /// because it does not enforce entitlements at all.
    /// </para>
    /// <para>
    /// <b>Unity already does most of the work.</b> Staging the shim into <c>UnityFramework</c>, setting
    /// <c>SWIFT_VERSION</c>, linking the framework there and pointing its framework search path at the package
    /// are all Unity's own behaviour. Only what it leaves out is done here.
    /// </para>
    /// </remarks>
    internal sealed class TappGoBuildProcessor : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        /// <summary>Where Unity places this package's iOS plugins in the generated project.</summary>
        private const string FrameworkPath = "Frameworks/com.tapp.go/Runtime/Plugins/iOS/TappGo.xcframework";

        /// <summary>The oldest iOS the app target may name — the floor <c>TappGo.xcframework</c> links at.</summary>
        /// <remarks>
        /// <b>Lower than the floor the SDK actually runs at, and deliberately so.</b> Native builds the
        /// framework at iOS 15.0 and gates its whole surface at 17.2: below that every entry point returns its
        /// neutral answer and logs one line saying it did nothing. So a host shipping iOS 15 links, launches
        /// and runs — with an SDK that is inert on the old systems, rather than a dependency it cannot resolve
        /// at all, which is what refusing the build here used to cost it. Raising this to match
        /// <see cref="ExtensionDeploymentTarget"/> would take that back.
        /// </remarks>
        private const string MinimumAppDeploymentTarget = "15.0";

        /// <summary>The deployment target the Live Activity extension is built at.</summary>
        /// <remarks>
        /// The SDK's <i>runtime</i> floor, and not the negotiable one: the staged
        /// <c>TappLiveActivityBundle.swift</c> names <c>TappLiveActivity</c>, which native annotates
        /// <c>@available(iOS 17.2, *)</c>, and an unguarded reference to a gated type from a target below its
        /// floor fails to <b>compile</b> — it is not a runtime surprise. Nor is there a lower version of this
        /// target worth having: it exists to render a Lock Screen card, and ActivityKit does not exist below
        /// 17.2. Set on the extension only, so the app keeps whatever the developer chose.
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
        /// link floor reads <i>"module 'TappGo' has a minimum deployment target of iOS 15.0"</i>; a missing
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
                    $"[Tapp] iOS {MinimumAppDeploymentTarget} or newer is required to link TappGo, and this " +
                    $"project targets {(string.IsNullOrEmpty(deploymentTarget) ? "nothing" : deploymentTarget)}. " +
                    "Set Player Settings → Other Settings → Target minimum iOS Version. " +
                    $"Live Activities themselves need iOS {ExtensionDeploymentTarget}, which the extension " +
                    "target is built at regardless — below it the SDK links and does nothing.");
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

            var targetName = settings.extensionTargetName;
            if (string.IsNullOrWhiteSpace(targetName) || targetName.Any(c => !char.IsLetterOrDigit(c) && c != '-'))
            {
                throw new BuildFailedException(
                    $"[Tapp] Extension target name \"{targetName}\" is unusable — it becomes an Xcode target " +
                    "and part of a bundle identifier, so it must be letters, digits and hyphens only. " +
                    "Fix it in Project Settings → Tapp.");
            }

            if (PlayerSettings.iOS.sdkVersion == iOSSdkVersion.DeviceSDK &&
                string.IsNullOrEmpty(PlayerSettings.iOS.appleDeveloperTeamID))
            {
                throw new BuildFailedException(
                    "[Tapp] A device build needs a team ID: the Live Activity extension is a second target " +
                    "and Xcode cannot pick a signing team for it on its own. Set Player Settings → Other " +
                    "Settings → Signing Team ID.");
            }

            ValidateFontFolder(settings.fontAnimationsFolder, "Font Animations Folder");
            ValidateFontFolder(settings.fontsFolder, "Fonts Folder");
        }

        /// <summary>
        /// Refuses a configured font folder that doesn't exist, naming the setting to fix. Both
        /// <see cref="TappGoSettings.fontAnimationsFolder"/> and
        /// <see cref="TappGoSettings.fontsFolder"/> are optional — blank passes.
        /// </summary>
        private static void ValidateFontFolder(string configuredPath, string settingLabel)
        {
            if (!string.IsNullOrWhiteSpace(configuredPath) && !Directory.Exists(ResolveProjectPath(configuredPath)))
            {
                throw new BuildFailedException(
                    $"[Tapp] {settingLabel} \"{configuredPath}\" (Project Settings → Tapp) does not exist.");
            }
        }

        // MARK: - After the build

        /// <summary>
        /// Adds the Live Activity extension, and everything the two targets need to talk to each other.
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
                    "[Tapp] Settings are missing or invalid, so the Live Activity extension was not created.");
            }

            var output = summary.outputPath;
            var extensionName = settings.extensionTargetName;
            var staged = StageExtensionSources(output, extensionName, settings);
            staged.AddRange(StageFontFiles(output, extensionName, settings));

            // One pass over the app's Info.plist, before the project work: it both declares Live Activity
            // support and hands back the versions the extension has to match. The capabilities added below are
            // entitlements-only, so nothing rewrites this file afterwards.
            var version = ConfigureAppPlist(output);

            var projectPath = PBXProject.GetPBXProjectPath(output);
            var project = new PBXProject();
            project.ReadFromFile(projectPath);

            var app = project.GetUnityMainTargetGuid();
            var appEntitlements = project.GetBuildPropertyForAnyConfig(app, "CODE_SIGN_ENTITLEMENTS");
            var framework = FrameworkGuid(project);
            var extension = AddExtensionTarget(project, app, extensionName, staged);

            project.AddFileToBuild(extension, framework);
            project.AddFileToEmbedFrameworks(app, framework);
            ConfigureExtension(project, extension, version);

            project.WriteToFile(projectPath);

            // Everything below re-reads the project from disk. Nothing may touch `project` again.
            AddCapabilities(projectPath, output, settings, app: app, appEntitlements: appEntitlements,
                extensionName: extensionName, development: summary.options.HasFlag(BuildOptions.Development));

            Debug.Log($"[Tapp] Added the {extensionName} Live Activity extension and embedded " +
                      $"TappGo.xcframework in {Path.GetFileName(output)}.");
        }

        // MARK: - Sources

        /// <summary>
        /// Copies the extension's sources out of the package and into the build.
        /// </summary>
        /// <returns>The staged Swift files, relative to the project root.</returns>
        private static List<string> StageExtensionSources(
            string output,
            string extensionName,
            TappGoSettings settings)
        {
            var tokens = new Dictionary<string, string>
            {
                ["__TAPP_APP_GROUP__"] = settings.appGroupIdentifier,
                ["__TAPP_DISPLAY_NAME__"] = string.IsNullOrWhiteSpace(settings.extensionDisplayName)
                    ? Application.productName
                    : settings.extensionDisplayName,
            };

            var staged = ExtensionStaging.Stage(
                ExtensionStaging.TemplateRoot(),
                Path.Combine(output, extensionName),
                tokens);

            // Only the Swift files are compiled. Info.plist is reached through INFOPLIST_FILE, and adding it to
            // a build phase would copy it into the bundle a second time, as a resource.
            return staged
                .Where(path => path.EndsWith(".swift", StringComparison.OrdinalIgnoreCase))
                .Select(path => $"{extensionName}/{path}")
                .ToList();
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
            IEnumerable<string> sources)
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
            foreach (var name in new[] { "WidgetKit.framework", "SwiftUI.framework", "ActivityKit.framework" })
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
                "[Tapp] Append builds are not supported — choose Replace. Tapp adds the Live Activity " +
                "extension to the generated Xcode project, and appending to a project that already has it " +
                "produces duplicate targets and build phases that fail at submission rather than here.");
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

            project.SetBuildProperty(extension, "FRAMEWORK_SEARCH_PATHS", "$(inherited)");
            project.AddBuildProperty(extension, "FRAMEWORK_SEARCH_PATHS",
                $"$(PROJECT_DIR)/{Path.GetDirectoryName(FrameworkPath)?.Replace('\\', '/')}");

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
        /// Declares Live Activity support in the app's <c>Info.plist</c>, and reads back the version and build
        /// number the extension has to match.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Both halves in one read-modify-write, because they are the same file: two independent readers of one
        /// plist in one hook is the shape that lets the second silently drop what the first wrote.
        /// </para>
        /// <para>
        /// Without <c>NSSupportsLiveActivities</c> every start fails with
        /// <see cref="TappErrorCode.LiveActivitiesUnavailable"/> — the same code the user's own Settings toggle
        /// produces, so the two are indistinguishable from inside the app. Setting it here is what makes that
        /// code mean what it says.
        /// </para>
        /// <para>
        /// The versions are read from the generated plist rather than from <c>PlayerSettings</c>, so the two
        /// bundles agree by construction whatever Unity chose to write. An appex whose version differs from its
        /// host is rejected at submission, and an empty one is rejected at install.
        /// </para>
        /// <para>
        /// Missing values throw rather than defaulting. A default would paper over a host project whose own app
        /// bundle is malformed, and put a version on the extension that its app doesn't have — trading a clear
        /// failure here for a confusing one at App Store validation.
        /// </para>
        /// </remarks>
        private static (string shortVersion, string buildNumber) ConfigureAppPlist(string output)
        {
            var path = Path.Combine(output, "Info.plist");
            var plist = new PlistDocument();
            plist.ReadFromFile(path);

            var shortVersion = PlistString(plist.root, "CFBundleShortVersionString");
            var buildNumber = PlistString(plist.root, "CFBundleVersion");

            if (string.IsNullOrEmpty(shortVersion) || string.IsNullOrEmpty(buildNumber))
            {
                throw new BuildFailedException(
                    "[Tapp] The app's Info.plist has no CFBundleShortVersionString or CFBundleVersion, so the " +
                    "Live Activity extension has no version to match. Set Version and Build in " +
                    "Player Settings → Identification.");
            }

            plist.root.SetBoolean("NSSupportsLiveActivities", true);
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

        /// <summary>
        /// The staged framework's file reference.
        /// </summary>
        /// <exception cref="BuildFailedException">
        /// If it isn't there. Never a warning: without the framework the app has nothing to embed and would
        /// die at launch under <c>dyld</c>, and a build that says nothing is worse than one that stops.
        /// </exception>
        private static string FrameworkGuid(PBXProject project)
        {
            var framework = project.FindFileGuidByProjectPath(FrameworkPath);
            if (framework == null)
            {
                throw new BuildFailedException(
                    $"[Tapp] {FrameworkPath} is not in the generated project, so it cannot be linked or " +
                    "embedded and the app would crash at launch. Reinstall the package, or report this against " +
                    "the Unity version in use.");
            }

            return framework;
        }

        // MARK: - Entitlements

        /// <summary>
        /// Gives each target the entitlements it needs — which are not the same set.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The app gets the App Group <b>and</b> Keychain access filed under it, because auth tokens live in
        /// the Keychain under the App Group as the access group. The extension gets the App Group
        /// <b>only</b>: a Live Activity is rendered by a process the system launches for a Lock Screen
        /// presentation, and that process cannot reliably reach the data-protection Keychain — the read fails
        /// with <c>errSecNotAvailable</c> whatever it is entitled to. What it renders is stored unencrypted
        /// for exactly that reason, so the entitlement would be a claim with nothing behind it.
        /// </para>
        /// <para>
        /// An existing entitlements file is <b>extended, not replaced</b>. Another SDK's hook may have created
        /// one already, and pointing the target at a fresh file of ours would strip whatever it put there
        /// while leaving its build succeeding.
        /// </para>
        /// </remarks>
        private static void AddCapabilities(
            string projectPath,
            string output,
            TappGoSettings settings,
            string app,
            string appEntitlements,
            string extensionName,
            bool development)
        {
            var group = settings.appGroupIdentifier;

            var appManager = new ProjectCapabilityManager(
                projectPath,
                string.IsNullOrEmpty(appEntitlements) ? $"{extensionName}/App.entitlements" : appEntitlements,
                targetName: null,
                targetGuid: app);

            appManager.AddAppGroups(new[] { group });

            // The prefix is required and is not added for us: the access group is the App Group qualified by
            // the team's identifier prefix, which Xcode expands at signing time.
            appManager.AddKeychainSharing(new[] { $"$(AppIdentifierPrefix){group}" });

            if (settings.pushEnabled)
            {
                // Only produces `aps-environment`, which the SDK reads to choose an APNs host. The provisioning
                // profile is what actually decides it at signing, so this is a default rather than a promise.
                appManager.AddPushNotifications(development);
            }

            appManager.WriteToFile();

            var extensionManager = new ProjectCapabilityManager(
                projectPath,
                $"{extensionName}/{extensionName}.entitlements",
                extensionName);
            extensionManager.AddAppGroups(new[] { group });
            extensionManager.WriteToFile();
        }

        // MARK: - Internals

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
        private static string ResolveProjectPath(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                return relativePath;
            }

            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            return Path.GetFullPath(Path.Combine(projectRoot, relativePath));
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
