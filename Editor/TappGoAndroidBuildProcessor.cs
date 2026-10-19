#if UNITY_ANDROID
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TappGo.Editor.Modules;
using TappGo.Internal;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace TappGo.Editor
{
    /// <summary>
    /// The package's one hook into the Android build.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Unity regenerates the Gradle project on every build, so nothing a developer adds by hand survives.
    /// This code <i>is</i> the integration: it declares the Maven Central dependency of core and of every
    /// installed Tapp product package, then hands each product the generated project to stage what is its
    /// own — the Android half of what <see cref="TappGoBuildProcessor"/> does for the Xcode project.
    /// </para>
    /// <para>
    /// <b>Nothing native ships in this package.</b> Gradle resolves <see cref="NativePin.AndroidCoordinates"/>
    /// from Maven Central on the host's first build, exactly as a native Android host would. An EDM4U
    /// <c>Dependencies.xml</c> ships beside this for hosts that resolve through it, and this hook steps aside
    /// when it finds EDM4U's work already done.
    /// </para>
    /// <para>
    /// One mechanism for Unity 2022.3 and Unity 6, deliberately: Unity 6's <c>AndroidProjectFilesModifier</c>
    /// is the better API and the planned replacement once the package floor moves, but two code paths mean
    /// two verifiers for one output.
    /// </para>
    /// </remarks>
    internal sealed class TappGoAndroidBuildProcessor : IPreprocessBuildWithReport, IPostGenerateGradleAndroidProject
    {
        /// <summary>The oldest API level the SDK links at.</summary>
        private const int MinimumSdk = 24;

        /// <summary>Late, so other SDKs' hooks land first and this mutates a finished project.</summary>
        public int callbackOrder => 100;

        // MARK: - Before the build

        /// <summary>
        /// Rejects a project Tapp cannot be built into, before anything is generated.
        /// </summary>
        /// <remarks>
        /// Every check here has a version that would otherwise surface as a Gradle error minutes later,
        /// pointing at a generated file the developer never wrote: a minSdk below the AAR's reads
        /// <i>"uses-sdk:minSdkVersion 21 cannot be smaller than version 24 declared in library"</i>.
        /// </remarks>
        public void OnPreprocessBuild(BuildReport report)
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                return;
            }

            var minSdk = (int)PlayerSettings.Android.minSdkVersion;
            if (minSdk < MinimumSdk)
            {
                throw new BuildFailedException(
                    $"[Tapp] The Tapp Android SDK links at API {MinimumSdk}, and this project's Minimum API Level " +
                    $"is {minSdk}. Set Player Settings → Other Settings → Minimum API Level to {MinimumSdk} or " +
                    "higher. (The SDK only does anything on API 26 and later; below that it links and does nothing.)");
            }

            var settings = TappGoSettings.Load();
            if (settings == null)
            {
                throw new BuildFailedException(
                    "[Tapp] No TappGoSettings asset. Open Project Settings → Tapp to create one.");
            }

            var invalid = settings.Validate(TappPlatform.Android);
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
        }

        // MARK: - After the Gradle project is generated

        /// <summary>
        /// Adds the dependency and the widget to <c>unityLibrary</c>.
        /// </summary>
        /// <param name="path">The generated <c>unityLibrary</c> module — what Unity hands this callback.</param>
        public void OnPostGenerateGradleAndroidProject(string path)
        {
            var settings = TappGoSettings.Load();
            if (settings == null || settings.Validate(TappPlatform.Android) != null)
            {
                // Unreachable through a normal build — OnPreprocessBuild already refused. Reachable if a host
                // invokes the callback directly, and silence there would ship an app with no widget.
                throw new BuildFailedException(
                    "[Tapp] Settings are missing or invalid, so the Gradle project was not set up for Tapp.");
            }

            var buildGradle = Path.Combine(path, "build.gradle");
            if (!File.Exists(buildGradle))
            {
                throw new BuildFailedException(
                    $"[Tapp] {path} has no build.gradle (a build.gradle.kts is not supported), so the Tapp " +
                    "dependency cannot be declared.");
            }

            var version = AndroidGradle.ResolveVersion(
                NativePin.AndroidVersion,
                Environment.GetEnvironmentVariable(AndroidGradle.LocalVersionVariable),
                Environment.GetEnvironmentVariable(AndroidGradle.ReleaseBuildVariable) == "1");
            if (version != NativePin.AndroidVersion)
            {
                Debug.LogWarning(
                    $"[Tapp] {AndroidGradle.LocalVersionVariable} is set: this build declares " +
                    $"{NativePin.AndroidGroup}:*:{version} instead of the pinned " +
                    $"{NativePin.AndroidVersion}. It resolves from mavenLocal() and is not what a customer " +
                    "gets — never ship it.");
            }

            var contributions = TappEditorModules.All
                .Select(module => module.Android(settings))
                .Where(contribution => contribution != null)
                .ToList();

            var artifacts = new List<string> { NativePin.AndroidArtifact };
            artifacts.AddRange(contributions.SelectMany(contribution => contribution.Artifacts)
                .Where(artifact => artifact != NativePin.AndroidArtifact)
                .Distinct(StringComparer.Ordinal));

            var dependency = AndroidGradle.WriteGradleDependencies(
                buildGradle, NativePin.AndroidGroup, artifacts, version, strict: version != NativePin.AndroidVersion);

            var settingsGradle = Path.Combine(Path.GetDirectoryName(path) ?? path, "settings.gradle");
            if (!AndroidGradle.DeclaresMavenCentral(settingsGradle))
            {
                Debug.LogWarning(
                    "[Tapp] settings.gradle does not declare mavenCentral(), so Gradle cannot resolve " +
                    $"{NativePin.AndroidGroup}:{NativePin.AndroidArtifact}:{version}. Unity's own template declares it; a custom " +
                    "settingsTemplate.gradle has to keep it.");
            }

            // Each product stages what is its own — sources, resources, manifest entries — after the dependency
            // it compiles against is declared.
            foreach (var contribution in contributions)
            {
                contribution.PostGenerate?.Invoke(path);
            }

            Debug.Log($"[Tapp] {dependency}");
        }
    }
}
#endif
