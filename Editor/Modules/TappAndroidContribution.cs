using System;
using System.Collections.Generic;

namespace TappGo.Editor.Modules
{
    /// <summary>
    /// What one Tapp product needs from the Android build, as data.
    /// </summary>
    /// <remarks>
    /// <b>For Tapp's own packages, not for host code.</b> Core writes the one Gradle block Tapp owns — two
    /// writers sharing one marker would delete each other's lines — so a product names its artifact and core
    /// spells the coordinate, at the one version every Tapp Android artifact shares.
    /// </remarks>
    public sealed class TappAndroidContribution
    {
        /// <summary>Maven artifact names under <c>com.tappgo</c>, e.g. <c>tapp-widgets</c>.</summary>
        public List<string> Artifacts { get; } = new List<string>();

        /// <summary>
        /// Runs once the Gradle project exists, with the path of its <c>unityLibrary</c> module — for the
        /// sources, resources and manifest entries a product stages. Throw
        /// <c>BuildFailedException</c> to refuse the build.
        /// </summary>
        public Action<string> PostGenerate { get; set; }
    }
}
