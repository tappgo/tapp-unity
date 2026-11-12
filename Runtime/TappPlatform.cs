namespace TappGo
{
    /// <summary>
    /// The platform a configuration is judged for, and a native call is made on.
    /// </summary>
    /// <remarks>
    /// A host never picks one — the build does. Public because a Tapp product package is told which it is
    /// running on (see <see cref="TappGo.Modules.TappModule"/>), and the Editor counts as
    /// <see cref="iOS"/>: that is the platform every setting applies to.
    /// </remarks>
    public enum TappPlatform
    {
        /// <summary>An iOS player, and the Editor.</summary>
        iOS,

        /// <summary>An Android player.</summary>
        Android,
    }
}
