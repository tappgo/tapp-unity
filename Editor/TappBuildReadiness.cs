using UnityEditor;

namespace TappGo.Editor
{
    /// <summary>
    /// What the settings page and the Welcome window say about whether this project builds — one judgement, so
    /// the two cannot describe one project differently.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Per platform.</b> An App Group is required on iOS and means nothing on Android; a widget's update
    /// period is the reverse. Judging every project as iOS left an Android-only project red for a setting it
    /// never needs, and left an Android-only mistake green until the Android build refused it.
    /// </para>
    /// <para>
    /// <b>The colour follows the platform the Editor is set to build</b>, because that is the build the host
    /// is about to make. The other platform's problem is still named — a project that ships both has to hear
    /// about both — but it does not turn a page red for a platform the host may never build. Set to build
    /// neither — a standalone player, say — the Editor gives no hint which one the host means, so a problem on
    /// either is red.
    /// </para>
    /// <para>
    /// Only what the settings decide. What only a build can know — the deployment target, the signing team,
    /// the Minimum API Level — is still the build hook's to refuse, in its own words.
    /// </para>
    /// <para>
    /// Pure, so a test can hold every combination without an Editor window.
    /// </para>
    /// </remarks>
    internal static class TappBuildReadiness
    {
        /// <summary>What a status strip needs: its colour, and what to say.</summary>
        internal readonly struct Verdict
        {
            /// <summary>Whether the build the Editor is set to make would be refused.</summary>
            internal bool Blocking { get; }

            /// <summary>One line per platform whose builds would be refused, or <c>null</c> when none would.</summary>
            internal string Problems { get; }

            /// <summary>
            /// The platform to call ready when the other one is not — <c>"iOS"</c> or <c>"Android"</c> — and
            /// <c>null</c> when there is nothing to qualify.
            /// </summary>
            internal string ReadyFor { get; }

            internal Verdict(bool blocking, string problems, string readyFor)
            {
                Blocking = blocking;
                Problems = problems;
                ReadyFor = readyFor;
            }
        }

        /// <summary>
        /// The judgement for this project's settings: each platform's own rules, the mixed-version check both
        /// build hooks apply, whether the installed products can be described at all, and the extension's
        /// settings when a build creates one.
        /// </summary>
        /// <remarks>
        /// The same order the iOS build hook refuses in, so the sentence shown is the one the build stops with.
        /// </remarks>
        internal static Verdict Judge(BuildTarget active, TappGoSettings settings, bool needsExtension)
        {
            var mixed = TappPackageVersions.Problem();
            var ios = settings.Validate(TappPlatform.iOS)
                      ?? mixed
                      ?? ExtensionStaging.ContributionsProblem(settings)
                      ?? (needsExtension ? ExtensionStaging.ExtensionSettingsProblem(settings) : null);
            var android = settings.Validate(TappPlatform.Android) ?? mixed;
            return Judge(active, ios, android);
        }

        /// <param name="active">The platform the Editor is set to build.</param>
        /// <param name="ios">Why an iOS build would be refused, or <c>null</c>.</param>
        /// <param name="android">Why an Android build would be refused, or <c>null</c>.</param>
        internal static Verdict Judge(BuildTarget active, string ios, string android)
        {
            if (ios == null && android == null)
            {
                return new Verdict(false, null, null);
            }

            string problems;
            if (ios != null && android != null)
            {
                problems = ios == android
                    ? $"iOS and Android builds will fail: {ios}"
                    : active == BuildTarget.Android
                        ? $"Android builds will fail: {android}\n\niOS builds will fail: {ios}"
                        : $"iOS builds will fail: {ios}\n\nAndroid builds will fail: {android}";
                return new Verdict(true, problems, null);
            }

            problems = ios != null ? $"iOS builds will fail: {ios}" : $"Android builds will fail: {android}";

            // One platform fails and the other does not. That blocks unless the Editor is set to build the one
            // that passes; set to build neither, the page cannot know which the host means, so it says no.
            var passing = ios == null ? BuildTarget.iOS : BuildTarget.Android;
            return active == passing
                ? new Verdict(false, problems, ios == null ? "iOS" : "Android")
                : new Verdict(true, problems, null);
        }
    }
}
