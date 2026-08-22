namespace TappGo.Samples
{
    /// <summary>
    /// The Unity mark, carried in source rather than as an asset — for the same reason as
    /// <see cref="ConsoleLogo"/>: a UPM sample's <c>Samples~</c> folder never gets the <c>.meta</c> file an
    /// imported image would need, so a reference to one resolves to nothing in every importing project.
    /// </summary>
    /// <remarks>
    /// Kept as a small RGBA PNG rather than an alpha mask: the mark is a bevelled cube in three distinct
    /// greys, not one flat colour, so there is nothing to tint it from — <see cref="ConsoleLogo"/>'s trick
    /// only pays off when the shape is monochrome.
    /// </remarks>
    internal static class UnityLogo
    {
        /// <summary>The mark's aspect ratio — square, so a caller need only choose a height.</summary>
        internal const float AspectRatio = 1f;

        /// <summary>
        /// The mark, base64 of a 64×64 PNG. Generated — do not edit.
        /// </summary>
        /// <remarks>
        /// Data only, like <see cref="ConsoleLogo.Mask"/> — decoded by <see cref="ConsoleTheme"/>, which owns
        /// the console's textures.
        /// </remarks>
        internal static readonly string[] Bytes =
        {
            "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",
        };
    }
}
