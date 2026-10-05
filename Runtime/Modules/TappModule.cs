namespace TappGo.Modules
{
    /// <summary>
    /// One Tapp product — Live Activities, Widgets, or one that does not exist yet — as core sees it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>For Tapp's own packages, not for host code.</b> Core ships <c>configure</c>, the player's identity and
    /// nothing else; every product is a separate package that plugs in here. That is what lets a project
    /// install only the products it uses, and what lets a new product ship without a change to core.
    /// </para>
    /// <para>
    /// A module registers itself once, from a <c>RuntimeInitializeOnLoadMethod</c> at
    /// <c>SubsystemRegistration</c> — before any scene loads, so before any host code can call
    /// <see cref="Tapp.Configure"/>. Registering fills an in-memory list and nothing more: no native call, no
    /// settings read. It holds no state core depends on: every member is asked again on every
    /// <see cref="Tapp.Configure"/>.
    /// </para>
    /// <para>
    /// <b>The Editor needs it registered too.</b> That hook runs when a player starts, which the Editor does
    /// only in Play mode — and the settings page and both build hooks validate through this same registry. So
    /// a product's Editor module registers its runtime module as well, from its own <c>InitializeOnLoad</c>;
    /// without that, the product's settings are never asked about and a build passes with them empty.
    /// Registering twice is harmless: the registry keeps one module per <see cref="Id"/>.
    /// </para>
    /// </remarks>
    public abstract class TappModule
    {
        /// <summary>
        /// The module's package name, e.g. <c>com.tapp.go.widgets</c>. Modules are asked in the ordinal order
        /// of this string, so the <c>configure</c> payload is the same on every launch and on every machine.
        /// </summary>
        public abstract string Id { get; }

        /// <summary>
        /// Why this module's settings can't be used on <paramref name="platform"/>, or <c>null</c> when they can.
        /// </summary>
        /// <remarks>
        /// Asked at launch and by the build hook, so both refuse the same thing in the same words. The first
        /// module with something to say wins, after core's own rules.
        /// </remarks>
        public virtual string Validate(TappGoSettings settings, TappPlatform platform)
        {
            return null;
        }

        /// <summary>
        /// Adds this module's keys to the <c>configure</c> payload.
        /// </summary>
        /// <remarks>
        /// Each native surface configures itself from its own keys and ignores the rest, so a module writes
        /// only what its surface reads — and nothing at all when it has nothing configured.
        /// </remarks>
        public virtual void ContributeConfiguration(JsonBuilder json, TappGoSettings settings, TappPlatform platform)
        {
        }

        /// <summary>
        /// Runs after the native <c>configure</c> succeeded, for a surface that needs a call of its own.
        /// </summary>
        /// <returns><c>null</c> when there was nothing to do; otherwise the outcome, which becomes
        /// <see cref="Tapp.Configure"/>'s own when it is a failure.</returns>
        public virtual TappResult? AfterConfigure(TappGoSettings settings, TappPlatform platform)
        {
            return null;
        }
    }
}
