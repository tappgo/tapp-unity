using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TappGo.Internal;
using UnityEditor.Build;

namespace TappGo.Editor
{
    /// <summary>
    /// The one Gradle block Tapp owns, and the version every Tapp Android artifact is declared at.
    /// </summary>
    /// <remarks>
    /// Pure file work with no Unity build API in it, so the Editor tests drive it against a temporary
    /// <c>build.gradle</c>. Core writes the block for every installed Tapp package — two writers sharing one
    /// marker would each delete the other's lines.
    /// </remarks>
    internal static class AndroidGradle
    {
        private const string GradleBegin = "// [Tapp] BEGIN — added by com.tapp.go's build hook; regenerated on every build, do not edit";
        private const string GradleEnd = "// [Tapp] END";

        /// <summary>The artifact Tapp shipped before it was split into modules. Naming it is a build failure.</summary>
        private const string RetiredArtifact = "sdk";

        /// <summary>
        /// Declares a version published to <c>mavenLocal()</c> instead of <see cref="NativePin.AndroidVersion"/>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The Android twin of <c>TAPP_IOS_LOCAL_XCFRAMEWORK</c>, and it exists for a sharper reason than
        /// trying a native change early. The SDK bakes its backend environment into its own build: the
        /// release variant — the one on Maven Central — talks to production and nothing a host does can
        /// point it elsewhere. A developer without the production key therefore gets a 401 on every
        /// request and a widget that never leaves its loading layout, which looks exactly like a broken
        /// integration. The development backend lives in the SDK's <b>debug</b> variant, which
        /// <c>Tapp-Android</c> publishes to <c>mavenLocal()</c> as <c>…:&lt;version&gt;-debug</c>.
        /// </para>
        /// <para>
        /// Refused under <see cref="ReleaseBuildVariable"/>, and it needs the host project's own
        /// <c>settingsTemplate.gradle</c> to declare <c>mavenLocal()</c> — nothing shipped does.
        /// </para>
        /// </remarks>
        internal const string LocalVersionVariable = "TAPP_ANDROID_LOCAL_VERSION";

        /// <summary>Set by the release and smoke workflows, and what makes a local version a hard error.</summary>
        /// <remarks>
        /// The same variable the iOS hook reads for its own local path. Spelled twice because the two hooks
        /// are behind opposite platform guards and never compile together.
        /// </remarks>
        internal const string ReleaseBuildVariable = "TAPP_RELEASE_BUILD";

        /// <summary>
        /// Appends the Maven dependency as one owned block, or explains why it must not.
        /// </summary>
        /// <remarks>
        /// <para>
        /// A second <c>dependencies {}</c> block at the end of the file rather than a line inside Unity's:
        /// Gradle accepts any number of them, so nothing Unity or a host template wrote has to be parsed.
        /// </para>
        /// <para>
        /// <b>Three things stop the write.</b> A marked block from a previous run is replaced. Any other line
        /// already naming the artifact — a host's template, or EDM4U with <c>Dependencies.xml</c> — is left
        /// alone when its version is ours and a failure when it is not, since two versions of one SDK is a
        /// resolution Gradle decides silently. And an <c>.aar</c> of the SDK already in <c>libs/</c> (EDM4U's
        /// local mode) means no Maven line at all: a local copy beside a Maven copy is a duplicate-class
        /// failure at dex time.
        /// </para>
        /// </remarks>
        /// <summary>
        /// The version to declare: the pin, or the <see cref="LocalVersionVariable"/> build when one is named.
        /// </summary>
        /// <param name="pinned">What <see cref="NativePin.AndroidVersion"/> says a customer resolves.</param>
        /// <param name="local">The environment's value, or null/blank when it is unset.</param>
        /// <param name="releaseBuild">Whether <see cref="ReleaseBuildVariable"/> is set.</param>
        /// <exception cref="BuildFailedException">If a local version is named on a release build.</exception>
        internal static string ResolveVersion(string pinned, string local, bool releaseBuild)
        {
            if (string.IsNullOrWhiteSpace(local))
            {
                return pinned;
            }

            if (releaseBuild)
            {
                throw new BuildFailedException(
                    $"[Tapp] {LocalVersionVariable} is set on a release build. A release resolves " +
                    $"{NativePin.AndroidCoordinates} from Maven Central and nothing else; unset the variable.");
            }

            return local.Trim();
        }
        /// <summary>
        /// Declares every Tapp artifact this build links, inside the one block Tapp owns.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The block is removed and rewritten on every build, so a package that was uninstalled takes its line
        /// with it. One <c>implementation</c> line per artifact — core's is spelled out even though the
        /// products already depend on it, so a version conflict is reported against the artifact that has it.
        /// </para>
        /// <para>
        /// An artifact the host already declares at the same version is left alone; at another version it is a
        /// build failure, because two versions of one SDK cannot both be right. An artifact EDM4U resolved
        /// into <c>libs/</c> gets no Maven line beside it.
        /// </para>
        /// </remarks>
        /// <returns>One sentence per artifact, for the build log.</returns>
        /// <param name="strict">
        /// Whether to pin each artifact with Gradle's <c>strictly</c>, which a local version needs and the pin
        /// does not. <c>Tapp-Android</c> publishes its debug variant as an <i>alias</i> of the release one, so a
        /// product's POM names plain <c>tapp-core:&lt;pin&gt;</c> however it was resolved. Gradle ranks a
        /// qualifier below the release it qualifies, so that transitive beats the <c>-debug</c> line here and
        /// resolution fails on a version Maven Central does not have — the local build's own dependency,
        /// missing. <c>strictly</c> says this version is the answer rather than an opening bid.
        /// </param>
        internal static string WriteGradleDependencies(
            string buildGradlePath, string group, IReadOnlyList<string> artifacts, string version,
            bool strict = false)
        {
            var text = File.ReadAllText(buildGradlePath);
            var file = Path.GetFileName(buildGradlePath);

            var begin = text.IndexOf(GradleBegin, StringComparison.Ordinal);
            if (begin >= 0)
            {
                var end = text.IndexOf(GradleEnd, begin, StringComparison.Ordinal);
                if (end < 0)
                {
                    throw new BuildFailedException(
                        $"[Tapp] {buildGradlePath} has a `{GradleBegin}` marker with no `{GradleEnd}` — the block was " +
                        "hand-edited. Remove both markers and rebuild.");
                }

                text = text.Substring(0, begin).TrimEnd('\n', '\r', ' ') + "\n" + text.Substring(end + GradleEnd.Length).TrimStart('\n', '\r');
            }

            if (Declared(text, $"{group}:{RetiredArtifact}:") != null)
            {
                throw new BuildFailedException(
                    $"[Tapp] {file} declares {group}:{RetiredArtifact}, which no longer exists: the Tapp Android SDK " +
                    $"is now {group}:tapp-core plus one artifact per product, and this package declares them " +
                    "itself. Remove the line (a mainTemplate.gradle, or an EDM4U *Dependencies.xml).");
            }

            var libs = Path.Combine(Path.GetDirectoryName(buildGradlePath) ?? ".", "libs");
            var lines = new List<string>();
            var report = new List<string>();
            foreach (var artifact in artifacts)
            {
                var coordinates = $"{group}:{artifact}:{version}";
                var declared = Declared(text, $"{group}:{artifact}:");
                if (declared == version)
                {
                    report.Add($"{coordinates} is already declared in {file}; nothing added.");
                    continue;
                }

                if (declared != null)
                {
                    throw new BuildFailedException(
                        $"[Tapp] {file} already declares {group}:{artifact}:{declared}, and this package needs " +
                        $"{version}. Two versions of the Tapp SDK cannot both be right — align the other " +
                        "declaration (a mainTemplate.gradle, or an EDM4U *Dependencies.xml) with this package's pin.");
                }

                if (Directory.Exists(libs) && Directory.GetFiles(libs, $"{group}.{artifact}-*.aar").Length > 0)
                {
                    report.Add($"{group}.{artifact}-*.aar is in libs/ (EDM4U local mode); no Maven line added beside it.");
                    continue;
                }

                lines.Add(strict
                    ? $"    implementation('{group}:{artifact}') {{ version {{ strictly '{version}' }} }}"
                    : $"    implementation '{coordinates}'");
                report.Add($"added implementation '{coordinates}'{(strict ? " (strictly)" : string.Empty)} to {file}.");
            }

            if (lines.Count == 0)
            {
                File.WriteAllText(buildGradlePath, text);
                return string.Join(" ", report);
            }

            var block = new StringBuilder()
                .Append('\n')
                .Append(GradleBegin).Append('\n')
                .Append("dependencies {\n")
                .Append(string.Join("\n", lines)).Append('\n')
                .Append("}\n")
                .Append(GradleEnd).Append('\n');

            File.WriteAllText(buildGradlePath, text.TrimEnd('\n', '\r') + "\n" + block);
            return string.Join(" ", report);
        }

        /// <summary>The version a non-comment line declares for <paramref name="prefix"/>, or <c>null</c>.</summary>
        private static string Declared(string text, string prefix)
        {
            foreach (var line in text.Split('\n'))
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("//", StringComparison.Ordinal))
                {
                    continue;
                }

                var at = trimmed.IndexOf(prefix, StringComparison.Ordinal);
                if (at < 0)
                {
                    continue;
                }

                var declared = trimmed.Substring(at + prefix.Length);
                return new string(declared.TakeWhile(c => c != '\'' && c != '"' && c != ')' && !char.IsWhiteSpace(c)).ToArray());
            }

            return null;
        }

        /// <summary>Whether a generated <c>settings.gradle</c> lets Maven Central be searched.</summary>
        internal static bool DeclaresMavenCentral(string settingsGradlePath)
        {
            return File.Exists(settingsGradlePath) && File.ReadAllText(settingsGradlePath).Contains("mavenCentral()");
        }
    }
}
