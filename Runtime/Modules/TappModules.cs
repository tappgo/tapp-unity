using System;
using System.Collections.Generic;

namespace TappGo.Modules
{
    /// <summary>
    /// The Tapp products installed in this project.
    /// </summary>
    /// <remarks>
    /// <b>For Tapp's own packages, not for host code.</b> A product that is not installed is simply not here —
    /// there is no switch to turn one off, because the package manager already is one.
    /// </remarks>
    public static class TappModules
    {
        private static readonly List<TappModule> Registered = new List<TappModule>();

        /// <summary>
        /// Adds a module. Registering the same <see cref="TappModule.Id"/> again replaces the earlier one, so a
        /// domain reload that runs a registration twice leaves one module, not two.
        /// </summary>
        public static void Register(TappModule module)
        {
            if (module == null || string.IsNullOrEmpty(module.Id))
            {
                return;
            }

            Registered.RemoveAll(existing => string.Equals(existing.Id, module.Id, StringComparison.Ordinal));
            Registered.Add(module);
            Registered.Sort((left, right) => string.CompareOrdinal(left.Id, right.Id));
        }

        /// <summary>Every registered module, in the ordinal order of its <see cref="TappModule.Id"/>.</summary>
        public static IReadOnlyList<TappModule> All => Registered;

        /// <summary>Forgets a module. Tests use it to put the registry back as they found it.</summary>
        public static void Unregister(string id)
        {
            Registered.RemoveAll(existing => string.Equals(existing.Id, id, StringComparison.Ordinal));
        }
    }
}
