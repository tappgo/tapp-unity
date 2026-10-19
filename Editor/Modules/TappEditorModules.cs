using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor.Build;
using UnityEditor.PackageManager;

namespace TappGo.Editor.Modules
{
    /// <summary>
    /// The Tapp products installed in this project, as the Editor sees them.
    /// </summary>
    /// <remarks>
    /// <b>For Tapp's own packages, not for host code.</b> The Editor-side twin of
    /// <see cref="TappGo.Modules.TappModules"/>, with the same rules: a package that is not installed is not
    /// here, and the order is the ordinal order of the package names.
    /// </remarks>
    public static class TappEditorModules
    {
        private static readonly List<TappEditorModule> Registered = new List<TappEditorModule>();

        /// <summary>Adds a module; the same <see cref="TappEditorModule.Id"/> again replaces the earlier one.</summary>
        public static void Register(TappEditorModule module)
        {
            if (module == null || string.IsNullOrEmpty(module.Id))
            {
                return;
            }

            Registered.RemoveAll(existing => string.Equals(existing.Id, module.Id, StringComparison.Ordinal));
            Registered.Add(module);
            Registered.Sort((left, right) => string.CompareOrdinal(left.Id, right.Id));
        }

        /// <summary>Every registered module, in the ordinal order of its <see cref="TappEditorModule.Id"/>.</summary>
        public static IReadOnlyList<TappEditorModule> All => Registered;

        /// <summary>Forgets a module. Tests use it to put the registry back as they found it.</summary>
        public static void Unregister(string id)
        {
            Registered.RemoveAll(existing => string.Equals(existing.Id, id, StringComparison.Ordinal));
        }

        /// <summary>
        /// The absolute path of a folder inside the package <paramref name="assembly"/> belongs to.
        /// </summary>
        /// <remarks>
        /// <para>
        /// A module passes <b>its own</b> assembly. Templates live in the package that owns them, and resolving
        /// one against the calling code's package instead would silently look in core for a file the Widgets
        /// package ships.
        /// </para>
        /// <para>
        /// A missing package or folder is a build failure with its name in it, rather than a template quietly
        /// left unstaged: the symptom of that is an Xcode project that builds and an app that renders nothing.
        /// </para>
        /// </remarks>
        /// <exception cref="BuildFailedException">If the package cannot be resolved or the folder is absent.</exception>
        public static string PackageFolder(Assembly assembly, string relativePath)
        {
            var package = PackageInfo.FindForAssembly(assembly);
            if (package == null)
            {
                throw new BuildFailedException(
                    $"[Tapp] Could not resolve the package {assembly.GetName().Name} belongs to. Reinstall it " +
                    "through the Package Manager — a loose copy of the sources under Assets/ is not supported.");
            }

            var root = Path.Combine(package.resolvedPath, relativePath);
            if (!Directory.Exists(root))
            {
                throw new BuildFailedException(
                    $"[Tapp] The template {relativePath} is missing from {package.name} at {root}. This is a " +
                    "packaging defect — report it rather than working around it.");
            }

            return root;
        }
    }
}
