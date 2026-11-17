using UnityEngine;

namespace TappGo.Editor.Modules
{
    /// <summary>
    /// One Tapp product as the Editor sees it: a section on the settings page, and what it needs from a build.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>For Tapp's own packages, not for host code.</b> Core owns the one hook into each platform's build —
    /// two hooks would mean two widget extension targets with one bundle identifier on iOS, and two writers
    /// deleting each other's Gradle block on Android. A product therefore never touches the generated project
    /// itself. It <i>describes</i> what it needs, as data, and core applies every product's description in
    /// one pass.
    /// </para>
    /// <para>
    /// A module registers itself from an <c>[InitializeOnLoad]</c> static constructor, through
    /// <see cref="TappEditorModules.Register"/>.
    /// </para>
    /// </remarks>
    public abstract class TappEditorModule
    {
        /// <summary>The module's package name, e.g. <c>com.tapp.go.widgets</c>. Decides the order modules apply in.</summary>
        public abstract string Id { get; }

        /// <summary>The heading of this module's section in Project Settings → Tapp.</summary>
        public abstract string DisplayName { get; }

        /// <summary>
        /// The module's own settings asset, or <c>null</c> when it has none yet.
        /// </summary>
        /// <param name="create">
        /// <c>true</c> when the developer pressed "Create Settings": make the asset, seeding it from a
        /// pre-split <c>TappGoSettings.asset</c> when that still carries this module's fields.
        /// </param>
        public virtual ScriptableObject Settings(bool create)
        {
            return null;
        }

        /// <summary>What the iOS build needs for this module, or <c>null</c> for nothing.</summary>
        public virtual TappIosContribution Ios(TappGoSettings settings)
        {
            return null;
        }

        /// <summary>What the Android build needs for this module, or <c>null</c> for nothing.</summary>
        public virtual TappAndroidContribution Android(TappGoSettings settings)
        {
            return null;
        }
    }
}
