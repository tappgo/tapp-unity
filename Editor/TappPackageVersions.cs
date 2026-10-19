using System;
using System.Collections.Generic;
using System.Linq;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace TappGo.Editor
{
    /// <summary>
    /// Holds every installed Tapp package to one version.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Tapp is a package per product, released in lockstep, and the products reach into core through an API
    /// that moves with them. Each product's <c>package.json</c> declares the core version it needs — but a
    /// project that installs by Git URL names every package itself, and the Package Manager does not compare
    /// a Git dependency against that declaration. Nothing else would notice <c>#v2.1.0</c> beside
    /// <c>#v2.2.0</c> until something failed to compile, or worse, did not.
    /// </para>
    /// <para>
    /// So both build hooks refuse a mixed set, and the settings page says so before a build does.
    /// </para>
    /// </remarks>
    internal static class TappPackageVersions
    {
        private const string Core = "com.tapp.go";

        /// <summary>Why the installed Tapp packages cannot be built together, or <c>null</c> when they can.</summary>
        internal static string Problem()
        {
            return Problem(PackageInfo.GetAllRegisteredPackages()
                .Select(package => new KeyValuePair<string, string>(package.name, package.version)));
        }

        /// <summary>The comparison, apart from the Package Manager so a test can hand it a set.</summary>
        internal static string Problem(IEnumerable<KeyValuePair<string, string>> packages)
        {
            var tapp = packages
                .Where(package => package.Key == Core || package.Key.StartsWith(Core + ".", StringComparison.Ordinal))
                .OrderBy(package => package.Key, StringComparer.Ordinal)
                .ToList();

            if (tapp.Select(package => package.Value).Distinct(StringComparer.Ordinal).Count() <= 1)
            {
                return null;
            }

            return "The installed Tapp packages are different versions — " +
                   string.Join(", ", tapp.Select(package => $"{package.Key} {package.Value}")) +
                   ". They are released together and must match: point every Tapp line in Packages/manifest.json " +
                   "at the same tag.";
        }
    }
}
