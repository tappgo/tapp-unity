namespace TappGo.Internal
{
    /// <summary>
    /// The native SDK versions this package is built for — the one place they are spelled in code.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Nothing native ships inside this package. The iOS build hook asks Xcode to resolve the public Swift
    /// package at <see cref="IosVersion"/>; the Android build hook writes <see cref="AndroidCoordinates"/> into
    /// the Gradle project for Maven Central to resolve. These constants are those two requests.
    /// </para>
    /// <para>
    /// <c>docs/NATIVE-CONTRACT.md</c> declares the same versions in prose, and <c>Editor/TappGoDependencies.xml</c>
    /// repeats the Android one for hosts with the External Dependency Manager. <c>Tools/native-pin.py --check</c>
    /// fails CI when any of the three disagree, so moving a pin is a three-file edit on purpose. The Widgets
    /// package keeps one more copy of the Android version, in its own <c>Editor/TappWidgetsDependencies.xml</c>;
    /// the same check reads it when that repo is checked out beside this one.
    /// </para>
    /// </remarks>
    internal static class NativePin
    {
        /// <summary>The public Swift package, exactly as its release notes spell it — no <c>.git</c> suffix.</summary>
        /// <remarks>
        /// Xcode derives a package's identity from the URL's last path component. Spelling it the way a native
        /// host would lets Xcode recognise a host that already depends on it as the same package.
        /// </remarks>
        internal const string IosRepositoryUrl = "https://github.com/tappgo/tapp-ios";

        /// <summary>The exact tag Xcode resolves. Never a range: the binary's checksum is per-tag.</summary>
        internal const string IosVersion = "2.2.1";

        /// <summary>The core product: the whole bridge facade, and the one module every shim imports.</summary>
        /// <remarks>
        /// The package vends one product per Tapp module. This one is always linked; each installed product
        /// package names its own. A surface product lists core among its own targets, so naming core beside
        /// it asks for nothing twice — it is spelled out so every target's link line says what it links.
        /// </remarks>
        internal const string IosCoreProductName = "Tapp";

        /// <summary>The Maven Central artifact, in Gradle's <c>group:name:version</c> spelling.</summary>
        internal const string AndroidGroup = "com.tappgo";
        internal const string AndroidArtifact = "tapp-core";
        internal const string AndroidVersion = "2.1.0";
        internal const string AndroidCoordinates = AndroidGroup + ":" + AndroidArtifact + ":" + AndroidVersion;

        /// <summary>The version the running platform's binary is expected to report.</summary>
        internal static string Expected(TappPlatform platform)
        {
            return platform == TappPlatform.Android ? AndroidVersion : IosVersion;
        }
    }
}
