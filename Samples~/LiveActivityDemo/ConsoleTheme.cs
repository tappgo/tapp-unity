using System;
using System.Collections.Generic;
using UnityEngine;

namespace TappGo.Samples
{
    /// <summary>
    /// The console's look: colours, rounded backgrounds and text styles.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Separated from <see cref="LiveActivityConsole"/> so that file stays about the SDK calls. Nothing here
    /// is part of the integration — delete it and style the console however your project already does.
    /// </para>
    /// <para>
    /// IMGUI has no rounded rectangle, so the card and button backgrounds are small textures generated at
    /// runtime and nine-sliced. That is the whole trick, and it is why this sample needs no image assets and
    /// therefore no <c>.meta</c> files, no atlas, and no import settings to get wrong.
    /// </para>
    /// </remarks>
    internal sealed class ConsoleTheme
    {
        /// <summary>The graphite ground, matching the native test app's own <c>TappTheme.ground</c> exactly.</summary>
        internal static readonly Color Background = new Color32(0x0F, 0x0F, 0x14, 0xFF);
        internal static readonly Color Card = new Color32(0x18, 0x18, 0x1B, 0xFF);
        internal static readonly Color Field = new Color32(0x0E, 0x0E, 0x10, 0xFF);
        /// <summary>The Tapp purple, sampled from the wordmark rather than eyeballed.</summary>
        internal static readonly Color Accent = new Color32(0x6C, 0x29, 0xFF, 0xFF);
        internal static readonly Color AccentPressed = new Color32(0x56, 0x1F, 0xCC, 0xFF);
        /// <summary>
        /// The lightened violet, for a purple that has to carry <i>text</i> or a glyph on the dark ground.
        /// </summary>
        /// <remarks>
        /// The same split the native app makes, and for the same reason: <see cref="Accent"/> is dark enough
        /// that violet-on-card is barely legible, so a filled control uses it as a background while a bordered
        /// one tints from this instead. The native app tints its whole view tree with this value, which is what
        /// every <c>.bordered</c> button there — the copy control included — draws its label and fill from.
        /// </remarks>
        internal static readonly Color AccentText = new Color32(0xB3, 0x9B, 0xFF, 0xFF);

        /// <summary>
        /// The fill behind a bordered control — a selected tab, a chip — as the native app's <c>.bordered</c>
        /// buttons take it from their tint.
        /// </summary>
        /// <remarks>
        /// One constant rather than the same expression at each site, because the two have to be the *same*
        /// violet: two shades a step apart on one screen read as one of them being a mistake.
        /// </remarks>
        internal static readonly Color AccentFill = new Color(AccentText.r, AccentText.g, AccentText.b, 0.16f);

        internal static readonly Color Neutral = new Color32(0x2A, 0x2A, 0x2E, 0xFF);
        /// <summary>The hairline between rows inside a card.</summary>
        internal static readonly Color Stroke = new Color32(0x2C, 0x2C, 0x3A, 0xFF);
        internal static readonly Color Text = new Color32(0xF2, 0xF2, 0xF5, 0xFF);
        internal static readonly Color Muted = new Color32(0x8E, 0x8E, 0x96, 0xFF);
        internal static readonly Color Good = new Color32(0x30, 0xD1, 0x58, 0xFF);
        internal static readonly Color GoodBackground = new Color32(0x10, 0x33, 0x1D, 0xFF);
        internal static readonly Color Bad = new Color32(0xFF, 0x45, 0x3A, 0xFF);
        internal static readonly Color BadBackground = new Color32(0x3A, 0x14, 0x12, 0xFF);
        /// <summary>iOS's own systemOrange — a state worth a second look, not a failure.</summary>
        internal static readonly Color Warn = new Color32(0xFF, 0x9F, 0x0A, 0xFF);
        internal static readonly Color WarnBackground = new Color32(0x3A, 0x2A, 0x10, 0xFF);

        internal GUIStyle Screen { get; private set; }
        internal GUIStyle Card9 { get; private set; }
        internal GUIStyle Title { get; private set; }
        internal GUIStyle Heading { get; private set; }
        internal GUIStyle Label { get; private set; }
        internal GUIStyle Value { get; private set; }
        internal GUIStyle FieldStyle { get; private set; }
        internal GUIStyle Primary { get; private set; }
        internal GUIStyle Secondary { get; private set; }
        internal GUIStyle TabOn { get; private set; }
        internal GUIStyle TabOff { get; private set; }
        internal GUIStyle TabBar { get; private set; }
        internal GUIStyle TabLabelOn { get; private set; }
        internal GUIStyle TabLabelOff { get; private set; }
        internal GUIStyle LogCallGood { get; private set; }
        internal GUIStyle LogCallBad { get; private set; }
        internal GUIStyle LogDetail { get; private set; }
        internal GUIStyle GoodBadge { get; private set; }
        internal GUIStyle BadBadge { get; private set; }
        internal GUIStyle WarnBadge { get; private set; }
        internal GUIStyle NeutralBadge { get; private set; }
        internal GUIStyle Chip { get; private set; }
        internal GUIStyle TokenValue { get; private set; }
        internal GUIStyle RowValue { get; private set; }
        internal GUIStyle Copied { get; private set; }

        /// <summary>Every texture made here, so they can all be destroyed together.</summary>
        private readonly List<Texture2D> owned = new List<Texture2D>();

        private bool built;

        /// <summary>
        /// Builds the styles, once, from inside <c>OnGUI</c>.
        /// </summary>
        /// <remarks>
        /// Not in <c>Awake</c>: <see cref="GUI.skin"/> is only valid during a GUI callback, and reading it
        /// earlier gives styles that silently render with the wrong font.
        /// </remarks>
        internal void Build()
        {
            if (built)
            {
                return;
            }

            built = true;

            Screen = Solid(Background);
            Card9 = Rounded(Card, 14);
            Card9.padding = new RectOffset(16, 16, 14, 16);
            FieldStyle = Rounded(Field, 10);
            FieldStyle.normal.textColor = Text;
            FieldStyle.padding = new RectOffset(12, 12, 0, 0);
            FieldStyle.fontSize = 15;
            FieldStyle.alignment = TextAnchor.MiddleLeft;

            Title = Text_(28, FontStyle.Bold, Text);
            Heading = Text_(17, FontStyle.Bold, Text);
            // Wrapping is not cosmetic. A label that cannot wrap reports its full single-line width as its
            // *minimum*, and GUILayout honours a minimum over the container it is in — so one long sentence
            // silently widens every card around it and pushes the right-hand end of the layout off screen.
            Label = Text_(13, FontStyle.Normal, Muted);
            Label.wordWrap = true;
            Value = Text_(15, FontStyle.Normal, Text);
            Value.wordWrap = true;
            // A push token on exactly one line, like the native row. Wrapping is off and clipping is on so a
            // 64-character token can neither take three lines nor widen the card trying to fit: the console
            // elides it to the width it has before drawing, and this is the belt to that braces.
            TokenValue = Text_(13, FontStyle.Normal, Muted);
            TokenValue.wordWrap = false;
            TokenValue.clipping = TextClipping.Clip;

            // A row's own heading — the same one-line treatment as TokenValue, a size up and in the foreground
            // colour, so the id a row is about still reads as its title.
            RowValue = Text_(15, FontStyle.Normal, Text);
            RowValue.wordWrap = false;
            RowValue.clipping = TextClipping.Clip;

            Copied = Text_(13, FontStyle.Bold, Good);
            Copied.stretchWidth = false;

            // A call's own line in the Calls list: bold like the native row's semibold header, tinted by
            // outcome rather than carrying one fixed colour, the way that row's call name is either
            // accent-tinted or danger-tinted and nothing else.
            LogCallGood = Text_(13, FontStyle.Bold, AccentText);
            LogCallGood.wordWrap = true;
            LogCallBad = Text_(13, FontStyle.Bold, Bad);
            LogCallBad.wordWrap = true;

            // The outcome underneath it, in the same muted tone the native row's detail line uses outside its
            // own HTTP-status colouring — which nothing here reports, so this never needs the green/red split.
            LogDetail = Text_(13, FontStyle.Normal, Muted);
            LogDetail.wordWrap = true;

            Primary = Button(Accent, AccentPressed, Color.white);
            Secondary = Button(Neutral, Card, Text);

            TabBar = Rounded(Card, 22);
            TabBar.padding = new RectOffset(6, 6, 6, 6);

            // Backgrounds only — content none is passed to the button, so text properties on these two would
            // never be read. The selected pill now fills its whole equal-width slot rather than hugging its
            // own label, so it reads as a segment rather than a chip sized to one word.
            TabOn = Rounded(AccentFill, 14);
            TabOff = GUIStyle.none;

            // Colourless on purpose: DrawTabContent tints both the icon and the label from the same two
            // colours (AccentText / Muted) at draw time, the way the icon already had to. Baking the violet
            // into TabLabelOn would fork that into two places that have to agree.
            TabLabelOn = Text_(11, FontStyle.Bold, Color.white);
            TabLabelOn.alignment = TextAnchor.MiddleCenter;

            TabLabelOff = Text_(11, FontStyle.Normal, Color.white);
            TabLabelOff.alignment = TextAnchor.MiddleCenter;

            // A per-row action, not a card's main call to action — Secondary is full-width and roughly the
            // size of a thumb on purpose, and a row of running activities needs several actions sized to their
            // own label instead, the way the native app's bordered, accent-tinted buttons are.
            //
            // Tinted from AccentText rather than Accent: this is the bordered, not the filled, shape — the same
            // colour the native app's `.bordered` buttons take from its tint, for both the fill and the label.
            Chip = Rounded(AccentFill, 12);
            Chip.stretchWidth = false;
            Chip.normal.textColor = AccentText;
            Chip.hover.textColor = AccentText;
            Chip.active.textColor = AccentText;
            Chip.fontSize = 14;
            Chip.fontStyle = FontStyle.Bold;
            Chip.alignment = TextAnchor.MiddleCenter;
            Chip.padding = new RectOffset(16, 16, 8, 8);

            BuildIcons();

            GoodBadge = Badge(GoodBackground, Good);
            BadBadge = Badge(BadBackground, Bad);
            WarnBadge = Badge(WarnBackground, Warn);
            NeutralBadge = Badge(Neutral, Muted);
        }

        /// <summary>
        /// A rounded, nine-sliced badge in one colour.
        /// </summary>
        /// <remarks>
        /// Built once and stored, never called from <c>OnGUI</c>. Each call generates a texture, and a style
        /// created per frame leaks one per frame — invisible in the Editor and fatal on a phone.
        /// </remarks>
        private GUIStyle Badge(Color fill, Color text)
        {
            var style = Rounded(fill, 11);
            style.stretchWidth = false;
            style.normal.textColor = text;
            style.fontSize = 12;
            style.fontStyle = FontStyle.Bold;
            style.alignment = TextAnchor.MiddleCenter;
            style.padding = new RectOffset(12, 12, 5, 5);
            return style;
        }

        /// <summary>Releases the generated textures. Unity does not collect them on its own.</summary>
        internal void Dispose()
        {
            foreach (var texture in owned)
            {
                if (texture != null)
                {
                    UnityEngine.Object.Destroy(texture);
                }
            }

            owned.Clear();
            built = false;
        }

        // MARK: - Icons

        /// <summary>The tab icons, in tab order, white so they can be tinted where they are drawn.</summary>
        internal Texture2D[] Icons { get; private set; }

        /// <summary>The Tapp wordmark, white, for tinting. <c>null</c> if it failed to decode.</summary>
        internal Texture2D Logo { get; private set; }

        /// <summary>The Unity mark, in its own colours — nothing to tint it from. <c>null</c> if it failed to
        /// decode.</summary>
        internal Texture2D UnityMark { get; private set; }

        /// <summary>The copy glyph, white, for tinting where it is drawn.</summary>
        internal Texture2D CopyIcon { get; private set; }

        /// <summary>The refresh glyph, white, for tinting where it is drawn.</summary>
        internal Texture2D RefreshIcon { get; private set; }

        /// <summary>
        /// Draws the three tab icons.
        /// </summary>
        /// <remarks>
        /// Drawn rather than imported so the sample still ships as source with no assets — no atlas, no import
        /// settings, and no <c>.meta</c> files, which Unity will not generate inside a <c>Samples~</c> folder
        /// anyway. Each shape is a signed distance field evaluated per pixel, which anti-aliases for free and
        /// stays clean when the whole GUI is scaled up on a phone.
        /// </remarks>
        private void BuildIcons()
        {
            const int size = 24;

            // A 2x2 grid, matching the native app's square.grid.2x2.
            var grid = Icon(size, p => RoundedBox(
                new Vector2(Mathf.Repeat(p.x - 1f, 11f), Mathf.Repeat(p.y - 1f, 11f)) - new Vector2(5.5f, 5.5f),
                new Vector2(4f, 4f), 1.5f));

            // A bolt: a thick zigzag, which reads correctly at this size without a polygon fill.
            var bolt = Icon(size, p => Mathf.Min(
                Segment(p, new Vector2(14f, 3f), new Vector2(8f, 12f)),
                Mathf.Min(
                    Segment(p, new Vector2(8f, 12f), new Vector2(15f, 12f)),
                    Segment(p, new Vector2(15f, 12f), new Vector2(9f, 21f)))) - 1.5f);

            // A terminal: a chevron and a cursor rule.
            var terminal = Icon(size, p => Mathf.Min(
                Mathf.Min(
                    Segment(p, new Vector2(5f, 7f), new Vector2(10f, 12f)),
                    Segment(p, new Vector2(10f, 12f), new Vector2(5f, 17f))),
                Segment(p, new Vector2(13f, 17f), new Vector2(19f, 17f))) - 1.4f);

            Icons = new[] { grid, bolt, terminal };

            // A copy glyph: two offset rounded outlines, which is how doc.on.doc reads at this size. Outlined
            // rather than filled — `abs(distance)` turns a shape into its own border — because two solid cards
            // would merge into one blob wherever they overlap.
            CopyIcon = Icon(size, p => Mathf.Min(
                Mathf.Abs(RoundedBox(p - new Vector2(9.5f, 14.5f), new Vector2(5f, 6f), 1.8f)) - 0.85f,
                Mathf.Abs(RoundedBox(p - new Vector2(15f, 9.5f), new Vector2(5f, 6f), 1.8f)) - 0.85f));

            // A refresh glyph: a ring, broken where the arrow goes, with a chevron on the break — arrow.clockwise.
            // The break is a quadrant test rather than an angle: the ring is symmetric, so which part to leave
            // out is a matter of which corner, and a corner is two comparisons.
            RefreshIcon = Icon(size, p =>
            {
                var d = p - new Vector2(12f, 12.5f);
                var ring = Mathf.Abs(d.magnitude - 6.6f) - 1.25f;
                if (d.x > 0f && d.y < -2.5f)
                {
                    ring = float.MaxValue;
                }

                var chevron = Mathf.Min(
                    Segment(p, new Vector2(13.6f, 4.4f), new Vector2(18.6f, 6.6f)),
                    Segment(p, new Vector2(18.6f, 6.6f), new Vector2(14.6f, 10.2f))) - 1.25f;

                return Mathf.Min(ring, chevron);
            });

            // The wordmark as an alpha mask, so DrawLogo can tint it; the Unity mark as-is, since a bevelled
            // cube in three greys has nothing to tint it from.
            Logo = DecodePng(ConsoleLogo.Mask, asAlphaMask: true);
            UnityMark = DecodePng(UnityLogo.Bytes, asAlphaMask: false);
        }

        /// <summary>
        /// Decodes one of the embedded base64 PNGs, tracked for disposal like every other texture here.
        /// </summary>
        /// <remarks>
        /// <paramref name="asAlphaMask"/> moves the image into the alpha channel of a white texture:
        /// <c>LoadImage</c> expands a greyscale PNG to RGB, so a mask arrives in the red channel rather than in
        /// alpha, and a tinted draw reads alpha. Returns <c>null</c> rather than throwing if a decode ever
        /// fails — a console missing a mark is a great deal better than one that will not draw.
        /// </remarks>
        private Texture2D DecodePng(string[] base64, bool asAlphaMask)
        {
            var decoded = new Texture2D(2, 2, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };

            if (!decoded.LoadImage(Convert.FromBase64String(string.Concat(base64))))
            {
                UnityEngine.Object.Destroy(decoded);
                return null;
            }

            if (asAlphaMask)
            {
                var pixels = decoded.GetPixels32();
                for (var i = 0; i < pixels.Length; i++)
                {
                    pixels[i] = new Color32(255, 255, 255, pixels[i].r);
                }

                decoded.SetPixels32(pixels);
                decoded.Apply();
            }

            return Track(decoded);
        }

        /// <summary>Signed distance to a rounded box centred on the origin.</summary>
        private static float RoundedBox(Vector2 p, Vector2 half, float radius)
        {
            var d = new Vector2(Mathf.Abs(p.x), Mathf.Abs(p.y)) - half + new Vector2(radius, radius);
            return new Vector2(Mathf.Max(d.x, 0f), Mathf.Max(d.y, 0f)).magnitude
                   + Mathf.Min(Mathf.Max(d.x, d.y), 0f) - radius;
        }

        /// <summary>Distance to a line segment, which gives round caps and joins for nothing.</summary>
        private static float Segment(Vector2 p, Vector2 a, Vector2 b)
        {
            var pa = p - a;
            var ba = b - a;
            var t = Mathf.Clamp01(Vector2.Dot(pa, ba) / Vector2.Dot(ba, ba));
            return (pa - (ba * t)).magnitude;
        }

        /// <summary>Rasterises a distance field into a white texture whose alpha carries the shape.</summary>
        private Texture2D Icon(int size, Func<Vector2, float> distance)
        {
            var texture = new Texture2D(size, size, TextureFormat.ARGB32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    // Sampled at the pixel centre; the half-pixel band around the zero crossing is the
                    // anti-aliased edge. Flipped in y because a texture's origin is bottom-left while these
                    // shapes are written the way they are read, top-down.
                    var p = new Vector2(x + 0.5f, size - (y + 0.5f));
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(0.5f - distance(p))));
                }
            }

            texture.Apply();
            return Track(texture);
        }

        // MARK: - Construction

        private GUIStyle Button(Color fill, Color pressed, Color text)
        {
            var style = Rounded(fill, 12);
            style.active.background = Track(RoundedTexture(pressed, 12));
            style.normal.textColor = text;
            style.active.textColor = text;
            style.hover.textColor = text;
            style.fontSize = 16;
            style.fontStyle = FontStyle.Bold;
            style.alignment = TextAnchor.MiddleCenter;
            return style;
        }

        private GUIStyle Rounded(Color fill, int radius)
        {
            var texture = Track(RoundedTexture(fill, radius));
            return new GUIStyle
            {
                normal = { background = texture },
                border = new RectOffset(radius, radius, radius, radius),
            };
        }

        private GUIStyle Solid(Color fill)
        {
            var texture = Track(new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave });
            texture.SetPixel(0, 0, fill);
            texture.Apply();
            return new GUIStyle { normal = { background = texture } };
        }

        private static GUIStyle Text_(int size, FontStyle weight, Color colour)
        {
            return new GUIStyle
            {
                fontSize = size,
                fontStyle = weight,
                normal = { textColor = colour },
                alignment = TextAnchor.MiddleLeft,
            };
        }

        /// <summary>
        /// A square with rounded corners, sized so its straight edges are one pixel wide.
        /// </summary>
        /// <remarks>
        /// The alpha ramps across the last pixel of the curve rather than stepping, which is what stops the
        /// corners from looking like stairs once the whole GUI is scaled up on a phone.
        /// </remarks>
        private static Texture2D RoundedTexture(Color fill, int radius)
        {
            var size = radius * 2 + 2;
            var texture = new Texture2D(size, size, TextureFormat.ARGB32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    // The nearest point on the "straight" cross through the middle. Inside it the distance is
                    // zero, so alpha clamps to 1 and only the four corner quadrants get shaped.
                    var cx = Mathf.Clamp(x, radius, size - 1 - radius);
                    var cy = Mathf.Clamp(y, radius, size - 1 - radius);
                    var distance = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy));
                    var alpha = Mathf.Clamp01(radius - distance + 0.5f);
                    texture.SetPixel(x, y, new Color(fill.r, fill.g, fill.b, fill.a * alpha));
                }
            }

            texture.Apply();
            return texture;
        }

        private Texture2D Track(Texture2D texture)
        {
            owned.Add(texture);
            return texture;
        }
    }
}
