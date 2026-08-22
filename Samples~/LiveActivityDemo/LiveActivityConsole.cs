using System;
using System.Collections.Generic;
using UnityEngine;

namespace TappGo.Samples
{
    /// <summary>
    /// A console for driving every Tapp call by hand, and reading back exactly what each one answered.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Drop it on any GameObject in a scene — it needs no other setup, no prefab, and no Canvas. It draws with
    /// IMGUI on purpose: a sample that pulled in uGUI or TextMeshPro would force those packages on every
    /// project that installs Tapp, including ones that use neither. Your own game should not build UI this way.
    /// </para>
    /// <para>
    /// <b>Everything here is a real call.</b> Nothing is faked, so what the console reports is what your app
    /// would get. In the Editor the native framework does not exist, so every answer comes from the stub and
    /// says <c>editor-stub</c> — a Live Activity can only be seen on a physical device.
    /// </para>
    /// </remarks>
    public sealed class LiveActivityConsole : MonoBehaviour
    {
        [Tooltip("The Tapp live activity id to drive, as configured in the back office.")]
        public string activityId = "";

        [Tooltip("The screen to start on.")]
        public string entryId = "screen-1";

        [Tooltip("The user id the Set User ID button sends.")]
        public string userId = "demo-user-1";

        private enum Tab
        {
            Sdk,
            Live,
            Console,
        }

        private static readonly string[] TabTitles = { "SDK", "Live", "Console" };

        private Tab tab = Tab.Sdk;

        private readonly ConsoleTheme theme = new ConsoleTheme();

        /// <summary>
        /// One Tapp call as the native app's own Console tab shows it: the call signature, colour-coded by
        /// outcome, and the detail line underneath it.
        /// </summary>
        private readonly struct LogEntry
        {
            public readonly string Call;
            public readonly string Detail;
            public readonly bool Failed;

            public LogEntry(string call, string detail, bool failed)
            {
                Call = call;
                Detail = detail;
                Failed = failed;
            }
        }

        /// <summary>Newest first, so the last thing that happened is the thing you are looking at.</summary>
        private readonly List<LogEntry> log = new List<LogEntry>();

        private Vector2 scroll;

        /// <summary><c>StartLiveActivity</c>'s <c>Seconds</c>, entered as minutes — 0 means none.</summary>
        private int durationMinutes;

        /// <summary>What iOS last reported running, as of the last <see cref="Reconcile"/> — not a cache this
        /// app maintains by watching its own calls.</summary>
        private TappActivityInfo[] running = Array.Empty<TappActivityInfo>();

        /// <summary>
        /// What <see cref="Tapp.SdkVersion"/> answered at startup — the version, or the code it failed with.
        /// </summary>
        /// <remarks>
        /// Read once and kept, rather than called each frame: it crosses the bridge, and <c>OnGUI</c> runs
        /// several times a frame. The answer cannot change while the process lives.
        /// </remarks>
        private string nativeVersion;

        /// <summary>Whether <see cref="nativeVersion"/> is a version or the reason there isn't one.</summary>
        private bool nativeVersionOk;

        /// <summary>How wide a token label may be, set from the real content width each frame.</summary>
        private float tokenWidth = 200f;

        /// <summary>Each long line's elided form per style, and the width they were all fitted to — see
        /// <see cref="Elided"/>.</summary>
        private readonly Dictionary<GUIStyle, Dictionary<string, string>> elided =
            new Dictionary<GUIStyle, Dictionary<string, string>>();

        private float elidedAt;

        /// <summary>
        /// Per-activity dismissal delay in minutes, present only while that row's options are open.
        /// </summary>
        /// <remarks>
        /// Keyed by ActivityKit's id rather than the Tapp id, so two activities started from the same Tapp id
        /// keep their own setting. Absence is the closed state, which is what makes "closed" and "no delay"
        /// impossible to confuse.
        /// </remarks>
        private readonly Dictionary<string, int> dismissalMinutes = new Dictionary<string, int>();

        /// <summary>
        /// Which row's token was last copied, and when — so the confirmation shows on that row alone and
        /// expires on its own.
        /// </summary>
        /// <remarks>
        /// One row, not a set: copying is a single act, and a second copy moves the confirmation rather than
        /// lighting a second one. Keyed by ActivityKit's id for the reason <see cref="dismissalMinutes"/> is —
        /// two cards of one campaign share a Tapp id and hold separate tokens.
        /// </remarks>
        private string copiedActivityId;

        private float copiedAt = float.NegativeInfinity;

        /// <summary>
        /// Why <c>configure</c> last failed, or <c>null</c> for a success — so the header can say so without
        /// re-running it.
        /// </summary>
        /// <remarks>
        /// One field, not a <c>bool</c> beside a reason: they would be two writers of one fact, and a call site
        /// that set one and forgot the other would badge "Configured" with an error line under it.
        /// </remarks>
        private string configureError;

        /// <summary>Editable so a real app id can be tried on the phone without another build.</summary>
        private string appId = "";
        private string appGroup = "";

        /// <summary>
        /// Reference width the layout is designed against, in iPhone points.
        /// </summary>
        /// <remarks>
        /// IMGUI draws in pixels, so on a phone every control comes out too small to hit. Scaling the matrix by
        /// the real screen width against this one keeps the layout identical everywhere, and makes every size
        /// below a point value rather than a pixel guess.
        /// </remarks>
        private const float ReferenceWidth = 393f;

        // Taller than a single-line tab bar: the icon now sits above the title rather than beside it, the way
        // the native test app's TabView lays out a tab item.
        private const float TabBarHeight = 60f;

        /// <summary>How many lines to keep. Enough to see a whole start/update/end round trip.</summary>
        private const int MaxLines = 80;

        /// <summary>
        /// Everything to the right of a token on its row: the copy control, the gap, and room for the
        /// confirmation that appears beside it.
        /// </summary>
        /// <remarks>
        /// Reserved rather than measured, because the token has to be elided <i>before</i> the row is laid out —
        /// a label that cannot wrap reports its full width as its minimum, and GUILayout honours a minimum over
        /// the container it is in, so an unelided token would widen the card instead of being cut to fit it.
        /// </remarks>
        private const float TokenRowReserve = 118f;

        /// <summary>How long "Copied" stays up, matching the native row's one second.</summary>
        private const float CopiedFor = 1f;

        private const string ActivityIdKey = "tapp.demo.activityId";
        private const string AppIdKey = "tapp.demo.appId";
        private const string TabKey = "tapp.demo.tab";

        private void Start()
        {
            // Surviving a relaunch matters here: a device test ends with force-quitting the app to prove an
            // activity outlives the process, and retyping ids on a phone keyboard every time is the kind of
            // friction that stops people running the test.
            activityId = PlayerPrefs.GetString(ActivityIdKey, activityId);
            tab = (Tab)Mathf.Clamp(PlayerPrefs.GetInt(TabKey, 0), 0, TabTitles.Length - 1);

            var settings = TappGoSettings.Load();
            appGroup = settings != null ? settings.appGroupIdentifier : "";
            appId = PlayerPrefs.GetString(AppIdKey, settings != null ? settings.liveActivityAppId : "");

            // Kept for the SDK tab as well as traced. A version that only ever reached the log was the one
            // number nobody could get at on a device with no console attached — which is precisely the
            // situation this sample exists to be useful in.
            var version = Tapp.SdkVersion();
            nativeVersionOk = version.Ok;
            nativeVersion = version.Ok ? version.Value : version.Code;
            Trace(version.Ok ? $"native {version.Value}" : $"version unavailable — {version}");

            // Configure has already run — Bootstrap does it before the first scene loads — and running it again
            // is how the badge gets to report something true rather than inferred. It is safe: configure is
            // local, records the same settings and reloads the extension, with no network and no session
            // change. A game would not repeat it; a console that claims "Configured" should have checked.
            //
            // Handed the asset read just above rather than letting Configure load it again: TappGoSettings.Load
            // is a Resources scan, and this launch has already paid for two of them.
            Remember(Tapp.Configure(settings));

            // Reconciling at launch is the one call every host should make, so the sample leads with it.
            Reconcile();

            // A tap that cold-starts the app has already happened by the time this runs, so the event below
            // never fires for it. Missing this line is why "it works when the app was open, not when it
            // wasn't" is the usual shape of a broken deep-link integration.
            if (!string.IsNullOrEmpty(Application.absoluteURL))
            {
                OnDeepLink(Application.absoluteURL);
            }
        }

        private void OnEnable() => Application.deepLinkActivated += OnDeepLink;

        private void OnDisable() => Application.deepLinkActivated -= OnDeepLink;

        private void OnDestroy() => theme.Dispose();

        // MARK: - Layout

        /// <summary>
        /// The fixed layout options, built once.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Not a micro-optimisation.</b> A <c>GUILayoutOption</c> is a class wrapping a boxed value, and every
        /// inline <c>Fixed.Heading</c> allocates one plus the <c>params</c> array around it — about a
        /// hundred short-lived objects per pass across this file. IMGUI runs a full pass <i>per input event</i>,
        /// not per frame, so a focused text field on a phone — touches, keystrokes, the soft keyboard's editing
        /// callbacks — turns that into thousands of allocations a second and collections you feel as lag while
        /// typing.
        /// </para>
        /// <para>
        /// Safe to build statically, unlike a <c>GUIStyle</c>: an option carries a number and a flag and never
        /// reads <see cref="GUI.skin"/>, so it does not need a live GUI callback to exist.
        /// </para>
        /// </remarks>
        private static class Fixed
        {
            internal static readonly GUILayoutOption[] Title = { GUILayout.Height(36f) };
            internal static readonly GUILayoutOption[] Heading = { GUILayout.Height(22f) };
            internal static readonly GUILayoutOption[] Caption = { GUILayout.Height(16f) };
            internal static readonly GUILayoutOption[] Field = { GUILayout.Height(38f) };
            internal static readonly GUILayoutOption[] Action = { GUILayout.Height(46f) };
            internal static readonly GUILayoutOption[] StepperValue = { GUILayout.Height(34f) };
            internal static readonly GUILayoutOption[] Chip =
                { GUILayout.Width(44f), GUILayout.Height(34f) };
            internal static readonly GUILayoutOption[] Badge =
                { GUILayout.Height(24f), GUILayout.ExpandWidth(false) };
            internal static readonly GUILayoutOption[] SideAction =
                { GUILayout.Height(46f), GUILayout.Width(110f) };
            internal static readonly GUILayoutOption[] Hug = { GUILayout.ExpandWidth(false) };
            internal static readonly GUILayoutOption[] Fill = { GUILayout.ExpandWidth(true) };
            internal static readonly GUILayoutOption[] TabBarRow = { GUILayout.Height(TabBarHeight) };
        }

        /// <summary>
        /// The safe area the options below were built for, so they are rebuilt only when it actually moves.
        /// </summary>
        /// <remarks>
        /// The same bargain <see cref="Elided"/> makes, for the same reason: these three depend on a width that
        /// is stable for the whole run on a phone that isn't rotating, so recomputing them per pass would be the
        /// allocation <see cref="Fixed"/> exists to avoid, just with a variable in it.
        /// </remarks>
        private Rect measuredFor;

        private GUILayoutOption[] tokenWidthOption;
        private GUILayoutOption[] contentWidthOption;
        private GUILayoutOption[] viewportHeightOption;

        private void OnGUI()
        {
            theme.Build();

            var scale = Screen.width / ReferenceWidth;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

            var area = SafeArea(scale);
            var viewport = new Rect(0f, 0f, area.width, area.height - TabBarHeight - 56f);

            if (area != measuredFor || tokenWidthOption == null)
            {
                measuredFor = area;

                // The card's inner width, less what the token row keeps for its own controls. Derived from the
                // real safe area rather than assumed from ReferenceWidth, so a device whose safe area is
                // narrower still elides to something that fits it.
                tokenWidth = area.width - theme.Card9.padding.horizontal - TokenRowReserve;

                tokenWidthOption = new[] { GUILayout.Width(tokenWidth) };
                contentWidthOption = new[] { GUILayout.Width(area.width) };
                viewportHeightOption = new[] { GUILayout.Height(viewport.height) };
            }

            GUI.Box(new Rect(0f, 0f, ReferenceWidth, Screen.height / scale), GUIContent.none, theme.Screen);
            GUILayout.BeginArea(area);

            GUILayout.BeginVertical();
            GUILayout.Space(4f);
            GUILayout.Label(TabTitles[(int)tab], theme.Title, Fixed.Title);
            GUILayout.Space(8f);

            // Both scrollbars hidden — a debug console with a visible chrome scrollbar down its right edge
            // reads as a desktop tool, and iOS shows no indicator at rest either. Dragging replaces it, below.
            //
            // The content width is pinned rather than left to expand, so that a style which forgets to wrap
            // clips instead of silently widening every card around it.
            scroll = GUILayout.BeginScrollView(scroll, false, false,
                GUIStyle.none, GUIStyle.none,
                viewportHeightOption);
            GUILayout.BeginVertical(contentWidthOption);
            switch (tab)
            {
                case Tab.Sdk:
                    DrawSdkTab();
                    break;
                case Tab.Live:
                    DrawLiveTab();
                    break;
                default:
                    DrawConsoleTab();
                    break;
            }

            GUILayout.EndVertical();
            GUILayout.EndScrollView();
            DragToScroll(viewport);
            GUILayout.FlexibleSpace();
            DrawTabBar();
            GUILayout.EndVertical();

            GUILayout.EndArea();

            DismissKeyboardOnBackgroundTap();
        }

        /// <summary>
        /// Drops keyboard focus when a tap lands on nothing, which is what takes the soft keyboard back down.
        /// </summary>
        /// <remarks>
        /// <para>
        /// There is no "tapped outside" test to write here. A control that wanted the tap has already called
        /// <c>Event.Use()</c> on its way past, and a used event reads as <see cref="EventType.Used"/> — so an
        /// event still reporting <see cref="EventType.MouseDown"/> this late in <c>OnGUI</c> is, by definition,
        /// one nothing claimed. Placed after <c>EndArea</c> for that reason: every control has had its turn.
        /// </para>
        /// <para>
        /// Clearing <see cref="GUIUtility.keyboardControl"/> alone does <b>not</b> lower a phone's on-screen
        /// keyboard — checked against Unity's own IMGUI source: <c>TextEditor.OnLostFocus</c> only flips an
        /// internal flag, and nothing reacts to focus moving away by touching the actual keyboard. The keyboard
        /// is a live <see cref="TouchScreenKeyboard"/> the field opened for itself, held in that control's own
        /// <see cref="TextEditor.keyboardOnScreen"/> — reachable, without reflection, because
        /// <c>GUI.TextField</c> keeps its per-control state through <see cref="GUIUtility.QueryStateObject"/>,
        /// which is public. Setting <see cref="TouchScreenKeyboard.active"/> to <c>false</c> is what a real
        /// device actually responds to.
        /// </para>
        /// </remarks>
        private static void DismissKeyboardOnBackgroundTap()
        {
            if (Event.current.type != EventType.MouseDown || GUIUtility.keyboardControl == 0)
            {
                return;
            }

            if (GUIUtility.QueryStateObject(typeof(TextEditor), GUIUtility.keyboardControl) is TextEditor editor
                && editor.keyboardOnScreen != null)
            {
                editor.keyboardOnScreen.active = false;
                editor.keyboardOnScreen = null;
            }

            GUIUtility.keyboardControl = 0;
            Event.current.Use();
        }

        /// <summary>
        /// The drawable rectangle, in the scaled space, with the notch and home indicator excluded.
        /// </summary>
        /// <remarks>
        /// <see cref="UnityEngine.Screen.safeArea"/> is in pixels with its origin at the bottom-left, while
        /// IMGUI works top-left in the scaled space — so both axes have to be converted, not just divided.
        /// Skipping this is why a hand-rolled debug UI usually has its first row under the clock.
        /// </remarks>
        private static Rect SafeArea(float scale)
        {
            var safe = Screen.safeArea;
            var left = safe.x / scale;
            var top = (Screen.height - safe.yMax) / scale;
            var width = safe.width / scale;
            var height = safe.height / scale;
            return new Rect(left + 16f, top + 8f, width - 32f, height - 16f);
        }

        private void DrawTabBar()
        {
            GUILayout.BeginHorizontal(theme.TabBar, Fixed.TabBarRow);

            // One rect for the whole row, split into equal slots. GUILayout.Button sizes each button to its
            // own content, and "SDK", "Live" and "Console" are not the same width — a tab bar whose buttons
            // are not the same size does not read as a tab bar.
            var row = GUILayoutUtility.GetRect(0f, TabBarHeight - 12f, Fixed.Fill);
            var slotWidth = row.width / TabTitles.Length;

            for (var i = 0; i < TabTitles.Length; i++)
            {
                var slot = new Rect(row.x + (slotWidth * i), row.y, slotWidth, row.height);
                var selected = (int)tab == i;

                // GUIContent.none: the label is drawn by DrawTabContent, below the icon, not centred on one
                // line the way a GUIStyle would place button text.
                if (GUI.Button(slot, GUIContent.none, selected ? theme.TabOn : theme.TabOff))
                {
                    tab = (Tab)i;

                    // Remembered, so a relaunch comes back where you were. That matters more here than it
                    // looks: the device test worth running is a force-quit, and landing back on the Live tab
                    // is the difference between one tap and four.
                    PlayerPrefs.SetInt(TabKey, i);
                }

                DrawTabContent(slot, i, selected);
            }

            GUILayout.EndHorizontal();
        }

        /// <summary>
        /// Scrolls the content by dragging it, since the scrollbar is hidden.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Without this the hidden scrollbar would make anything below the fold unreachable: IMGUI scroll views
        /// are driven by their scrollbar and the scroll wheel, and a phone has neither.
        /// </para>
        /// <para>
        /// Skipped while a control owns the mouse — <c>hotControl</c> is non-zero from the moment a button or
        /// text field is pressed — so dragging inside a field still selects text, and a drag that starts on a
        /// button still cancels it by moving away, rather than scrolling the page out from under the touch.
        /// </para>
        /// </remarks>
        private void DragToScroll(Rect viewport)
        {
            var current = Event.current;
            if (current.type != EventType.MouseDrag ||
                GUIUtility.hotControl != 0 ||
                !viewport.Contains(current.mousePosition))
            {
                return;
            }

            // Content follows the finger, so the scroll offset moves the other way. BeginScrollView clamps
            // whatever it is handed, so overscrolling past either end needs no arithmetic here.
            scroll.y -= current.delta.y;
            current.Use();
        }

        /// <summary>
        /// Draws the wordmark, left-aligned and tinted, followed by the Unity mark at the same height —
        /// this is a Unity package, so the SDK tab says so beside its own name rather than only in the title
        /// bar.
        /// </summary>
        /// <remarks>
        /// Reserves its space through <c>GUILayout</c> and paints into the rect that comes back, so the card
        /// grows around it. Falls back to the product name if a mark failed to decode — a card with no title
        /// would read as a layout fault rather than a missing image.
        /// </remarks>
        private void DrawLogo()
        {
            const float wordmarkWidth = 116f;
            const float gap = 10f;
            const float unityScale = 0.9f;
            var height = wordmarkWidth / ConsoleLogo.AspectRatio;
            var unityHeight = height * unityScale;
            var unityWidth = theme.UnityMark != null ? unityHeight / UnityLogo.AspectRatio : 0f;

            if (theme.Logo == null)
            {
                GUILayout.Label("Tapp", theme.Heading, GUILayout.Height(height));
                return;
            }

            var totalWidth = wordmarkWidth + (theme.UnityMark != null ? gap + unityWidth : 0f);
            var row = GUILayoutUtility.GetRect(totalWidth, height, Fixed.Hug);
            if (Event.current.type != EventType.Repaint)
            {
                return;
            }

            DrawTinted(new Rect(row.x, row.y, wordmarkWidth, height), theme.Logo, ConsoleTheme.Accent);

            if (theme.UnityMark != null)
            {
                // Its own colours, not tinted: the mark is a bevelled cube in three greys, unlike the flat
                // wordmark beside it. Centred on the wordmark's row rather than top-aligned, since it is drawn
                // smaller than that row's height.
                GUI.DrawTexture(
                    new Rect(
                        row.x + wordmarkWidth + gap,
                        row.y + ((height - unityHeight) / 2f),
                        unityWidth,
                        unityHeight),
                    theme.UnityMark);
            }
        }

        /// <summary>Draws a tab's icon above its title, both centred in the slot and tinted to match —
        /// the arrangement the native test app's <c>TabView</c> uses.</summary>
        private void DrawTabContent(Rect slot, int index, bool selected)
        {
            if (Event.current.type != EventType.Repaint)
            {
                return;
            }

            const float glyph = 20f;
            const float gap = 4f;

            var label = TabTitles[index];
            var labelStyle = selected ? theme.TabLabelOn : theme.TabLabelOff;
            var labelHeight = labelStyle.CalcHeight(new GUIContent(label), slot.width);

            var hasIcon = theme.Icons != null && index < theme.Icons.Length;
            var contentHeight = (hasIcon ? glyph + gap : 0f) + labelHeight;
            var top = slot.y + ((slot.height - contentHeight) / 2f);

            var previous = GUI.color;
            GUI.color = selected ? ConsoleTheme.AccentText : ConsoleTheme.Muted;

            if (hasIcon)
            {
                var iconRect = new Rect(slot.x + ((slot.width - glyph) / 2f), top, glyph, glyph);
                GUI.DrawTexture(iconRect, theme.Icons[index]);
                top += glyph + gap;
            }

            // The label style carries no colour of its own — like the icon above, it is tinted here rather
            // than baked per selection state, so both read from the same two colours.
            var labelRect = new Rect(slot.x, top, slot.width, labelHeight);
            GUI.Label(labelRect, label, labelStyle);

            GUI.color = previous;
        }

        // MARK: - Tabs

        private void DrawSdkTab()
        {
            GUILayout.BeginVertical(theme.Card9);
            DrawLogo();
            GUILayout.Space(12f);

            // The badge sits on its own row under the mark rather than beside it, as in the native app. A
            // horizontal row would let the mark shrink to make space for a longer status.
            var configured = configureError == null;
            GUILayout.BeginHorizontal();
            GUILayout.Label(configured ? "Configured" : "Not configured",
                configured ? theme.GoodBadge : theme.BadBadge,
                Fixed.Badge);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            // Only when there is a reason to. A success said everything it has to say in the badge, and a row
            // reading "on launch" under it just gave the eye something to read that changes nothing.
            if (!configured)
            {
                GUILayout.Space(6f);
                GUILayout.Label(configureError, theme.Label, Fixed.Caption);
            }

            // The native version, which is not the same claim as the badge above it. `Configured` says a call
            // crossed the bridge and came back happy; this says which binary answered. When a Live Activity
            // does not appear, that is the first thing support asks for, and on a device with no console
            // attached this row is the only place to read it.
            GUILayout.Space(10f);
            GUILayout.Label(nativeVersionOk ? "Native SDK" : "Native SDK unavailable", theme.Label, Fixed.Caption);
            GUILayout.Label(string.IsNullOrEmpty(nativeVersion) ? "—" : nativeVersion, theme.Value);

            GUILayout.EndVertical();

            Gap();

            GUILayout.BeginVertical(theme.Card9);
            GUILayout.Label("Configuration", theme.Heading, Fixed.Heading);
            GUILayout.Space(8f);
            GUILayout.Label("App Group", theme.Label, Fixed.Caption);
            GUILayout.Label(string.IsNullOrEmpty(appGroup) ? "—" : appGroup, theme.Value);
            GUILayout.Space(10f);
            GUILayout.Label("App ID", theme.Label, Fixed.Caption);
            appId = GUILayout.TextField(appId, theme.FieldStyle, Fixed.Field);
            GUILayout.Space(10f);
            if (GUILayout.Button("Save & Reconfigure", theme.Primary, Fixed.Action))
            {
                Reconfigure();
            }

            GUILayout.EndVertical();

            Gap();

            GUILayout.BeginVertical(theme.Card9);
            GUILayout.Label("Identity", theme.Heading, Fixed.Heading);
            GUILayout.Space(8f);
            userId = GUILayout.TextField(userId, theme.FieldStyle, Fixed.Field);
            GUILayout.Space(10f);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Set User ID", theme.Primary, Fixed.Action))
            {
                SetUser();
            }

            GUILayout.Space(10f);
            if (GUILayout.Button("Log out", theme.Secondary, Fixed.SideAction))
            {
                Logout();
            }

            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        private void DrawLiveTab()
        {
            DrawRunningCard();

            Gap();

            // One card, matching the native app's "Start by ID": the fields, the end-date stepper and the
            // button that starts it all live together. Update and End are not here — they act on a running
            // activity, and a running row is exactly where the native app puts them too.
            GUILayout.BeginVertical(theme.Card9);
            GUILayout.Label("Start by ID", theme.Heading, Fixed.Heading);
            GUILayout.Space(8f);
            GUILayout.Label("Live activity id", theme.Label, Fixed.Caption);
            activityId = GUILayout.TextField(activityId, theme.FieldStyle, Fixed.Field);
            GUILayout.Space(10f);
            GUILayout.Label("Screen (entry id)", theme.Label, Fixed.Caption);
            entryId = GUILayout.TextField(entryId, theme.FieldStyle, Fixed.Field);
            // StartLiveActivity's Seconds, entered as minutes — the native app's own stepper on this card,
            // step and cap alike: 1 minute at a time, up to 120.
            durationMinutes = Stepper(
                "Duration (minutes, 0 = the design's own)",
                durationMinutes == 0 ? "None" : $"{durationMinutes} min",
                durationMinutes,
                step: 1,
                cap: 120);

            GUILayout.Space(10f);
            if (GUILayout.Button("Start", theme.Primary, Fixed.Action))
            {
                StartActivity();
            }

            GUILayout.EndVertical();
        }

        /// <summary>
        /// A captioned minute count with <c>−</c>/<c>+</c> beside it, returning what it should now be.
        /// </summary>
        /// <remarks>
        /// The value sits left and the two controls right, which is a <c>Stepper</c>'s own arrangement and what
        /// the native rows use. <paramref name="display"/> is passed in rather than formatted here because the
        /// two callers phrase zero differently — "None" against a duration, "Immediately" against a dismissal.
        ///
        /// The value draws in <c>RowValue</c>, not <c>Value</c>: <c>Value</c> keeps <c>wordWrap</c> on for
        /// prose, and paired with the <c>FlexibleSpace</c> below it can squeeze even a single short word under
        /// its own natural width and break it mid-word — "None" as "Non"/"e" on two lines.
        /// </remarks>
        private int Stepper(string caption, string display, int minutes, int step, int cap)
        {
            GUILayout.Space(10f);
            GUILayout.Label(caption, theme.Label);
            GUILayout.Space(6f);

            GUILayout.BeginHorizontal();
            GUILayout.Label(display, theme.RowValue, Fixed.StepperValue);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("−", theme.Chip, Fixed.Chip))
            {
                minutes = Mathf.Max(0, minutes - step);
            }

            GUILayout.Space(8f);
            if (GUILayout.Button("+", theme.Chip, Fixed.Chip))
            {
                minutes = Mathf.Min(cap, minutes + step);
            }

            GUILayout.EndHorizontal();
            return minutes;
        }

        /// <summary>
        /// <paramref name="token"/> shortened from the middle until it fits <see cref="tokenWidth"/> on one line.
        /// </summary>
        /// <remarks>
        /// Cached, because the fit is found by measuring candidates and the inputs barely change — running that
        /// search every frame would be work no frame needs. Measuring at all is the price of IMGUI having no
        /// truncation mode of its own.
        ///
        /// Every caller is fitted to the same width, the narrowest of them: the token row keeps space for its copy
        /// control and a row's own id keeps space for its state badge, so one of them could in principle run a
        /// little wider. Sharing one width keeps the cache to a lookup per style, with no key to build per frame.
        ///
        /// Per style, because the same string measures differently in two of them, and this card draws its id in
        /// one and its subtitle in another.
        /// </remarks>
        private string Elided(string text, GUIStyle style)
        {
            if (!Mathf.Approximately(elidedAt, tokenWidth))
            {
                elided.Clear();
                elidedAt = tokenWidth;
            }

            if (!elided.TryGetValue(style, out var forStyle))
            {
                forStyle = new Dictionary<string, string>();
                elided[style] = forStyle;
            }

            if (forStyle.TryGetValue(text, out var fitted))
            {
                return fitted;
            }

            fitted = text;
            var keep = text.Length;
            while (keep > 8 && style.CalcSize(new GUIContent(fitted)).x > tokenWidth)
            {
                keep -= 4;
                var half = keep / 2;
                fitted = text.Substring(0, half) + "…" + text.Substring(text.Length - half);
            }

            forStyle[text] = fitted;
            return fitted;
        }

        /// <summary>
        /// A chip with a glyph and no label — the shape the native app's <c>.labelStyle(.iconOnly)</c> bordered
        /// buttons take.
        /// </summary>
        /// <remarks>
        /// <c>GUIContent.none</c>, then the glyph painted into the rect that comes back: a GUIStyle centres an
        /// <i>image</i> against its text padding rather than in the control, so an icon-only button styled that
        /// way sits off centre. Tinted here for the same reason the tab icons are — one white texture, drawn in
        /// whichever colour the control needs.
        /// </remarks>
        private bool IconChip(Texture2D icon)
        {
            const float glyph = 17f;

            var pressed = GUILayout.Button(
                GUIContent.none, theme.Chip, Fixed.Chip);

            var rect = GUILayoutUtility.GetLastRect();
            DrawTinted(
                new Rect(
                    rect.x + ((rect.width - glyph) / 2f),
                    rect.y + ((rect.height - glyph) / 2f),
                    glyph,
                    glyph),
                icon,
                ConsoleTheme.AccentText);

            return pressed;
        }

        /// <summary>A hairline between rows, the way the native card separates them.</summary>
        private void Divider()
        {
            GUILayout.Space(10f);
            DrawTinted(
                GUILayoutUtility.GetRect(0f, 1f, Fixed.Fill),
                Texture2D.whiteTexture,
                ConsoleTheme.Stroke);
            GUILayout.Space(10f);
        }

        /// <summary>
        /// Paints one texture in one colour, and puts <see cref="GUI.color"/> back.
        /// </summary>
        /// <remarks>
        /// Every glyph the console draws is a white texture tinted where it is used, so the save/tint/restore
        /// dance is the shape of all of them — worth one helper rather than four hand-balanced pairs, any of
        /// which could forget the restore and leak a colour into whatever drew next. Skips non-Repaint passes
        /// and a missing texture itself, since every caller needed both guards anyway.
        /// </remarks>
        private static void DrawTinted(Rect rect, Texture2D texture, Color tint)
        {
            if (Event.current.type != EventType.Repaint || texture == null)
            {
                return;
            }

            var previous = GUI.color;
            GUI.color = tint;
            GUI.DrawTexture(rect, texture);
            GUI.color = previous;
        }

        /// <summary>
        /// The list of activities the way the native test app shows it — asked of iOS, not remembered.
        /// </summary>
        /// <remarks>
        /// First in the tab: what is actually running is the thing worth seeing before another card invites
        /// you to start something new.
        /// </remarks>
        private void DrawRunningCard()
        {
            GUILayout.BeginVertical(theme.Card9);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Running", theme.Heading, Fixed.Heading);
            GUILayout.FlexibleSpace();
            if (IconChip(theme.RefreshIcon))
            {
                Reconcile();
            }

            GUILayout.EndHorizontal();
            GUILayout.Space(4f);

            if (running.Length == 0)
            {
                // The empty state says only that. What the card is *for* — that this asks iOS rather than
                // trusting anything this app remembers — is worth reading beside real rows and is noise beside
                // nothing, which is two lines of explanation to scroll past to learn there is nothing to see.
                GUILayout.Label("Nothing running", theme.Label);
            }
            else
            {
                GUILayout.Label(
                    "Asks iOS what is running, not this app. Force-quit, relaunch, then press Refresh — " +
                    "activities outlive the process that started them.", theme.Label);
                GUILayout.Space(10f);

                for (var i = 0; i < running.Length; i++)
                {
                    DrawRunningRow(running[i]);
                    if (i < running.Length - 1)
                    {
                        Divider();
                    }
                }
            }

            GUILayout.EndVertical();
        }

        /// <param name="info">
        /// Acts on <see cref="TappActivityInfo.Id"/> and <see cref="TappActivityInfo.EntryId"/> — the pair that
        /// addresses this card — not <see cref="TappActivityInfo.ActivityId"/>, the system's own identifier,
        /// which no Tapp call takes. Both halves come straight off the row, so the console keeps no note of what
        /// it started: a card discovered after a relaunch drives exactly like one this session put up.
        /// </param>
        private void DrawRunningRow(TappActivityInfo info)
        {
            // Both ids on one line each, elided in the middle. Wrapping is what cost this row its alignment
            // twice: a UUID takes two lines at this width, which left the state badge floating beside the first
            // of them, and the row's height disagreeing with what it drew. Neither id is read here anyway —
            // they get pasted somewhere — so the ends are the part worth keeping.
            GUILayout.BeginHorizontal();
            GUILayout.Label(Elided(info.Id, theme.RowValue), theme.RowValue, tokenWidthOption);
            GUILayout.FlexibleSpace();
            GUILayout.Label(
                info.State.RawValue.ToUpperInvariant(), StateBadge(info.State), Fixed.Hug);
            GUILayout.EndHorizontal();

            GUILayout.Space(2f);
            GUILayout.Label(Elided(info.ActivityId, theme.TokenValue), theme.TokenValue, tokenWidthOption);

            // The screen, which is half of this card's address rather than decoration: two rows of one campaign
            // differ only here, and it is what the buttons below pass. A card with none is a push-to-start that
            // named no screen — nothing app-side can address it, so this row says so instead of offering
            // controls that would all be rejected.
            var addressable = !string.IsNullOrEmpty(info.EntryId);
            GUILayout.Space(2f);
            GUILayout.Label(addressable ? info.EntryId : "no screen — cannot be addressed", theme.Label);

            GUILayout.Space(6f);
            DrawTokenRow(info);
            GUILayout.Space(10f);

            // Chip, not Secondary: these sit beside each other rather than stacked full-width, so each one is
            // sized to its own label — "End" no wider than it has to be — the way the native app's bordered
            // buttons sit packed to the left rather than split evenly across the row.
            var open = dismissalMinutes.TryGetValue(info.ActivityId, out var minutes);

            GUILayout.BeginHorizontal();
            GUI.enabled = addressable;
            if (GUILayout.Button("Update", theme.Chip))
            {
                UpdateActivity(info.Id, info.EntryId);
            }

            GUILayout.Space(8f);
            if (GUILayout.Button("End", theme.Chip))
            {
                EndActivity(info.Id, info.EntryId, open && minutes > 0 ? minutes * 60 : (int?)null);
            }

            GUI.enabled = true;

            GUILayout.Space(8f);
            if (GUILayout.Button("Settings", theme.Chip))
            {
                // Closing clears the delay, so what End does always matches what the row shows — the same
                // bargain the native disclosure makes.
                if (open)
                {
                    dismissalMinutes.Remove(info.ActivityId);
                }
                else
                {
                    dismissalMinutes[info.ActivityId] = 0;
                }

                open = !open;
                minutes = 0;
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            if (open)
            {
                // How long the card stays on screen after it ends — EndLiveActivity's own seconds, entered here
                // in minutes. A duration from the end rather than an instant, which is what removed the trap
                // this row used to print a wall-clock time to guard against: a dismissal date computed in the
                // wrong timezone was three hours out and looked exactly like an end that never landed. Still
                // capped at 240, because ActivityKit silently clamps anything past four hours and an end cannot
                // be revised once sent.
                dismissalMinutes[info.ActivityId] = Stepper(
                    "Dismiss in (minutes after the end, 0 = immediately)",
                    minutes == 0 ? "Immediately" : $"+{minutes} min",
                    minutes,
                    step: 15,
                    cap: 240);
            }
        }

        /// <summary>
        /// This card's APNs update token on one line, with an icon-only copy control and a green confirmation —
        /// the native app's <c>CopyableTokenRow</c>, in IMGUI.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The token is elided in the middle rather than truncated at the end: 64 hex characters never fit, and
        /// the ends are what a reader checks a pasted value against.
        /// </para>
        /// <para>
        /// <b>A row with no token still draws.</b> <see cref="TappActivityInfo.UpdateToken"/> is <c>null</c> for
        /// the second between a start and iOS issuing the token, and for good once a card has ended — so the
        /// line says which of those it is rather than vanishing, and the copy control goes with it. A row that
        /// disappeared and came back would read as the card flickering.
        /// </para>
        /// </remarks>
        private void DrawTokenRow(TappActivityInfo info)
        {
            if (string.IsNullOrEmpty(info.UpdateToken))
            {
                GUILayout.Label(
                    info.State.IsFinished ? "update token — released with the card" : "update token — not issued yet",
                    theme.TokenValue,
                    tokenWidthOption);
                return;
            }

            GUILayout.BeginHorizontal();
            GUILayout.Label(Elided(info.UpdateToken, theme.TokenValue), theme.TokenValue, tokenWidthOption);
            GUILayout.FlexibleSpace();

            if (copiedActivityId == info.ActivityId && Time.unscaledTime - copiedAt < CopiedFor)
            {
                GUILayout.Label("Copied", theme.Copied, Fixed.Hug);
                GUILayout.Space(6f);
            }

            if (IconChip(theme.CopyIcon))
            {
                // The whole token, not the elided form on screen — the ellipsis is a reading aid and pasting it
                // would produce a token that looks plausible and addresses nothing.
                GUIUtility.systemCopyBuffer = info.UpdateToken;
                copiedActivityId = info.ActivityId;
                copiedAt = Time.unscaledTime;
                Trace($"update token copied ({info.ActivityId})");
            }

            GUILayout.EndHorizontal();
        }

        /// <summary>
        /// The activity's lifecycle state, spelled out — the system keeps an ended activity listed until its
        /// dismissal date passes, up to four hours, so without this an end that worked looks the same as one
        /// that never landed.
        /// </summary>
        private GUIStyle StateBadge(TappActivityState state)
        {
            if (state.IsFinished)
            {
                return theme.NeutralBadge;
            }

            return state == TappActivityState.Active ? theme.GoodBadge : theme.WarnBadge;
        }

        /// <summary>
        /// The call log, laid out the way the native app's own Console tab draws its "Calls" card: one
        /// heading, then each entry as its call signature over a detail line, colour-coded by outcome — no
        /// line count, no Clear, because the native card has neither.
        /// </summary>
        private void DrawConsoleTab()
        {
            GUILayout.BeginVertical(theme.Card9);
            GUILayout.Label("Calls", theme.Heading, Fixed.Heading);
            GUILayout.Space(8f);

            if (log.Count == 0)
            {
                GUILayout.Label("Nothing called yet.", theme.Label);
            }
            else
            {
                for (var i = 0; i < log.Count; i++)
                {
                    DrawLogEntry(log[i]);
                    if (i < log.Count - 1)
                    {
                        GUILayout.Space(10f);
                    }
                }
            }

            GUILayout.EndVertical();
        }

        /// <summary>One call: its signature, coloured red on failure the way the native row tints its own
        /// header, and the outcome underneath it in the muted tone the native row's detail line uses.</summary>
        private void DrawLogEntry(LogEntry entry)
        {
            GUILayout.Label(entry.Call, entry.Failed ? theme.LogCallBad : theme.LogCallGood);
            GUILayout.Space(2f);
            GUILayout.Label(entry.Detail, theme.LogDetail);
        }

        private static void Gap() => GUILayout.Space(12f);

        // MARK: - The calls

        /// <summary>
        /// Re-runs <c>configure</c> with the app id typed above.
        /// </summary>
        /// <remarks>
        /// Builds a fresh settings object rather than editing the asset: in the Editor the asset is a real file,
        /// and mutating it here would quietly rewrite what the next build ships. Configure is local — it records
        /// the settings and reloads the extension, with no network — so calling it again is cheap and safe.
        /// </remarks>
        private void Reconfigure()
        {
            var asset = TappGoSettings.Load();
            if (asset == null)
            {
                Trace("no settings asset to reconfigure from");
                return;
            }

            // Cloned rather than field-by-field: a hand-written copy silently stops carrying whatever field the
            // settings gain next, and reconfigures with its default instead of the asset's value.
            var edited = Instantiate(asset);
            edited.liveActivityAppId = appId;

            var result = Tapp.Configure(edited);
            Destroy(edited);

            Remember(result);
            if (result.Ok)
            {
                PlayerPrefs.SetString(AppIdKey, appId);
            }

            Trace(result.Ok ? $"reconfigured with app id {appId}" : $"reconfigure failed — {result}");
        }

        /// <summary>Records what <c>configure</c> answered, for the SDK tab's badge to read.</summary>
        private void Remember(TappResult result) => configureError = result.Ok ? null : result.Code;

        /// <summary>
        /// One call, spelled the way the native app's own log line reads it: the signature, then the duration
        /// if there was one — omitted entirely rather than shown as <c>null</c>, so the line says only what was
        /// actually called.
        /// </summary>
        /// <remarks>
        /// Every Live Activity call now reads this way — start, update and end all take
        /// <c>(id, entryID, seconds)</c> — which is why the end no longer has a formatter of its own. The three
        /// <c>seconds</c> do not all mean the same thing: on a start and an update it runs the countdowns, on
        /// an end it defers the dismissal. The log prints the number, not an interpretation of it.
        /// </remarks>
        private static string CallLine(string name, string id, string entryId, int? seconds)
        {
            var screen = string.IsNullOrEmpty(entryId) ? "\"\"" : entryId;
            var duration = seconds.HasValue ? $", seconds: {seconds.Value}" : "";
            return $"{name}(id: {id}, entryID: {screen}{duration})";
        }

        /// <summary>
        /// The stepper's minutes as whole seconds, or <c>null</c> at zero — a duration, not an instant.
        /// </summary>
        /// <remarks>
        /// Read in minutes because the point on device is to watch a countdown run for a length this app named
        /// rather than the one the payload baked in, and a second-resolution stepper makes that tedious to set.
        /// </remarks>
        private int? DurationSeconds => durationMinutes > 0 ? durationMinutes * 60 : (int?)null;

        private void StartActivity()
        {
            PlayerPrefs.SetString(ActivityIdKey, activityId);

            var seconds = DurationSeconds;
            var request = new TappActivityRequest { Id = activityId, EntryId = entryId, Seconds = seconds };
            var call = CallLine("startLiveActivity", activityId, entryId, seconds);

            Tapp.StartLiveActivity(request, result =>
            {
                Record(call, result.Ok ? result.Value : $"{result}", !result.Ok);
                if (result.Ok)
                {
                    Reconcile();
                }
            });
        }

        /// <param name="id">A running row's <see cref="TappActivityInfo.Id"/>.</param>
        /// <param name="entryId">
        /// That row's <see cref="TappActivityInfo.EntryId"/> — which card to reload, not a screen to send it to.
        /// A card never leaves the screen it started on, so this is always the screen the row already shows.
        /// </param>
        /// <remarks>
        /// Sends the duration the stepper currently shows, like the native app: with it off this only re-resolves
        /// the card's content and leaves the clock alone, and with it set it is the one call that proves a clock
        /// can be <i>restarted</i> on a running card rather than only named at start — the countdown should jump
        /// back to the full duration, not to whatever is left of the start's.
        /// </remarks>
        private void UpdateActivity(string id, string entryId)
        {
            var seconds = DurationSeconds;
            var call = CallLine("updateLiveActivity", id, entryId, seconds);
            Tapp.UpdateLiveActivity(id, entryId, seconds, result =>
            {
                var detail = seconds.HasValue ? $"restarted at {seconds.Value}s" : "reloaded, clock unchanged";
                Record(call, result.Ok ? detail : $"{result}", !result.Ok);
                if (result.Ok)
                {
                    Reconcile();
                }
            });
        }

        /// <param name="id">A running row's <see cref="TappActivityInfo.Id"/>.</param>
        /// <param name="entryId">
        /// That row's <see cref="TappActivityInfo.EntryId"/>. Ends this one card and leaves any other screen of
        /// the same campaign running — there is no "end everything", deliberately.
        /// </param>
        /// <param name="seconds">
        /// How much longer the ended card stays on screen. <c>null</c> takes it off at once; a duration leaves
        /// it there — capped at four hours by iOS, and unrevisable once sent, which is exactly what the row's
        /// own dismissal stepper is for.
        /// </param>
        private void EndActivity(string id, string entryId, int? seconds)
        {
            var call = CallLine("endLiveActivity", id, entryId, seconds);
            Tapp.EndLiveActivity(id, entryId, seconds, result =>
            {
                Record(
                    call,
                    result.Ok
                        ? (seconds.HasValue
                            // The wall-clock time this lands at, worked out here rather than sent: the call
                            // carries a duration now, and "in 30 minutes" is harder to check against a Lock
                            // Screen than a time is.
                            ? $"ended now, leaves the screen at {DateTimeOffset.Now.AddSeconds(seconds.Value):HH:mm}"
                            : "ended and dismissed")
                        : $"{result}",
                    !result.Ok);
                if (result.Ok)
                {
                    Reconcile();
                }
            });
        }

        /// <summary>
        /// Asks iOS what is running, rather than trusting anything this app remembers, and is what the
        /// "Running" card in the Live tab draws from.
        /// </summary>
        /// <remarks>
        /// Logs nothing on success — the native app's own <c>refresh()</c> doesn't either, since the Running
        /// card already shows the result. Only a failure is worth a line in the Calls list.
        /// </remarks>
        private void Reconcile()
        {
            var active = Tapp.ActiveLiveActivities();
            if (!active.Ok)
            {
                Record("activeLiveActivities()", $"{active}", true);
                return;
            }

            running = active.Value;
        }

        private void SetUser()
        {
            var result = Tapp.SetUserId(userId);
            Trace(result.Ok ? $"user id set to {userId}" : $"set user failed — {result}");
        }

        private void Logout()
        {
            Trace("logout…");
            Tapp.Logout(result => Trace(result.Ok ? "logged out" : $"logout failed — {result}"));
        }

        /// <summary>
        /// Hands every incoming URL to the SDK, and carries on with the ones it doesn't claim.
        /// </summary>
        /// <remarks>
        /// This is the whole deep-link integration, and it is not optional: iOS reports the tap to the app and
        /// tells the SDK nothing, so an open that never reaches here is an open Tapp cannot see.
        /// </remarks>
        private void OnDeepLink(string url)
        {
            var handled = Tapp.HandleUrl(url);
            if (!handled.Ok)
            {
                Trace($"deep link failed — {handled}");
                return;
            }

            Trace(handled.Value
                ? $"deep link claimed by Tapp: {url}"
                : $"deep link is yours to route: {url}");
        }

        // MARK: - Console

        /// <summary>Adds one entry to the Calls list — the native app's own Console tab shows exactly this
        /// set of calls and nothing else, so this is the only thing that reaches it.</summary>
        private void Record(string call, string detail, bool failed)
        {
            log.Insert(0, new LogEntry(call, detail, failed));
            if (log.Count > MaxLines)
            {
                log.RemoveAt(log.Count - 1);
            }

            Debug.Log($"[Tapp demo] {call} — {detail}");
        }

        /// <summary>Everything the native app's Console tab has no line for either — SDK/session calls, not
        /// Live Activity ones — kept in the player log only, for a device test that has scrolled away.</summary>
        private static void Trace(string line) => Debug.Log($"[Tapp demo] {line}");
    }
}
