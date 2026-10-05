# Changelog

All notable changes to `com.tapp.go`. Follows [Semantic Versioning](https://semver.org).

Each entry names the **native versions it resolves** — the package's version and the SDKs' are independent,
and a wrapper that silently changed which binary a build picks up is the bug this line exists to prevent.

## [2.1.0] — 2026-10-05

Widgets on both platforms, Android, and no native code in the package.

### Changed

- `docs/NATIVE-CONTRACT.md` §3b records how a host's R8 keeps the Android bridge from Tapp-Android D-144:
  by name only in a Unity player (`-if class com.unity3d.player.UnityPlayer`), which Unity's own ProGuard
  template satisfies in every minified build. No change to this package.
- **Breaking: Tapp no longer configures itself on launch.** Nothing of Tapp's runs in the game until the host
  calls `Tapp.Configure()` — no native call, no network, no session — exactly as on native iOS and Android, so
  the host decides when Tapp starts. The `autoConfigureOnLaunch` setting is removed with it. **Migration:** add
  one `Tapp.Configure()` call to your startup code (or after your login flow) and check its result. A build
  whose `TappGoSettings.asset` still has the old switch on logs a warning saying so. What still runs before
  the first scene is Unity plumbing only: the hidden `[Tapp]` object callbacks are delivered through, and
  each product package adding itself to an in-memory list — each package's Editor tests fail if anything
  else is added.
- **One package per product.** Live Activities and Widgets are now `com.tapp.go.liveactivities` and
  `com.tapp.go.widgets`, installed beside this package from mirrors of their own
  (`tappgo/tapp-liveactivities-unity`, `tappgo/tapp-widgets-unity`) at the same version. This mirror,
  `tappgo/tapp-unity`, now carries core only; a project that used the 2.0 package — which was Live Activities
  only — adds `com.tapp.go.liveactivities` beside it. Each package is signed with Unity's package signature.
- **Nothing native ships any more.** `TappGo.xcframework` (19 MB of a 20 MB package) is gone. The iOS build
  hook adds the public Swift package `https://github.com/tappgo/tapp-ios` to the Xcode project at an exact
  version and hangs its product on the app, `UnityFramework` and the extension; Xcode resolves and embeds it
  on the first build. The Android build hook writes `com.tappgo:tapp-core` into `unityLibrary/build.gradle` for
  Maven Central to resolve. The package is source, templates and editor UI — about a megabyte unpacked — and
  its size no longer moves with the native version. **The first build of each platform needs network.**
- **Tapp for Unity is one package per product.** This package is now **Tapp Core**: configuration and the
  player's identity. Live Activities moved to `com.tapp.go.liveactivities`; widgets — with Picture in Picture,
  `ValidateToken` and the pending link — are new in this version and live in `com.tapp.go.widgets`. Each is in
  a repo of its own, all released at one version. A project installs core plus the products it uses; a product
  that is not installed is not downloaded, linked, embedded or callable. **Breaking:** the Live Activity calls
  left `Tapp` for a class of their own — `Tapp.StartLiveActivity` → `TappLiveActivities.Start`; that package's
  README has the full table. `Tapp.Configure`, `SetUserId`, `Logout`, `HandleUrl` and `SdkVersion` did not
  move, and no wire payload changed.
- **The settings split with the packages, and an upgrade keeps what was set.** `TappGoSettings.asset` keeps
  the App Group, the extension's names and the font folders; each product has an asset of its own beside it,
  on the same Project Settings page, created from a button. Live Activities' is **filled from the app id and
  push switch the old asset held** — on the machine that upgraded the project, even if the old asset was saved
  again there first. Create the product settings before committing a re-saved `TappGoSettings.asset`: on
  another machine the saved asset no longer holds those two values.
- **The widget extension's entry point is generated**, from the lines each installed product contributes, so
  it imports exactly what the target links. With nothing installed that renders in one — core alone, or
  Widgets kept for Picture in Picture — no extension target is created, the extension's own settings (its
  names, the font folders) leave the settings page, and no build is refused over them.
- The Android dependency is `com.tappgo:tapp-core` (the SDK was split there too), one `implementation` line
  per installed product inside the one block Tapp owns. A host still declaring the retired `com.tappgo:sdk`
  fails the build with the rename.
- The native pin is spelled in three places that must agree — `docs/NATIVE-CONTRACT.md`,
  `Runtime/Internal/NativePin.cs` and `Editor/TappGoDependencies.xml` — and `Tools/native-pin.py --check`
  fails CI when they don't. `make check-native-pin` replaces `fetch-native-release`: it proves both pinned
  versions exist, signed and published, and that the iOS asset's SHA-256 matches the `Package.swift`
  checksum at that tag, which is the check SPM makes on a customer's machine.
- Exporting, verifying and linking a whole project moved to the `TappDemo-Unity` repo, which installs every
  package: `make build-ios verify-ios link-ios TAPP_MODULES=core|live-activities|widgets|all`. The hook still
  refuses local frameworks under `TAPP_RELEASE_BUILD=1`.
- `TappGoSettings.Validate` is per platform — an App Group is required on iOS and meaningless on Android —
  and then asks every installed product about its own settings. The settings page and the Welcome window
  judge both platforms and say which build would fail; their colour follows the platform the Editor is set
  to build.
- The tarball audit forbids every native binary and trips above 2 MB unpacked. A developer's leftover
  framework is the new way a tarball goes wrong.

### Added

- **`TappGo.Modules` and `TappGo.Editor.Modules` — the seam a product package plugs into.** Public, and for
  Tapp's own packages: a product registers a module that validates its settings, adds its `configure` keys
  and describes — as data — what it needs from the iOS and Android builds. A new Tapp product needs no change
  to this package.
- **A version-lockstep guard.** A Git install lets a project name a different tag per Tapp package and Unity
  does not compare them, so core does: a mixed set shows in Project Settings → Tapp and fails the build.

- `TappErrorCode.SurfaceNotLinked` — what a call answers on iOS when the native module it belongs to is not
  linked into the app: a Live Activity call without `TappLiveActivities.framework` in the process; a widget,
  `ValidateToken`, `PendingLink` or Picture in Picture call without `TappWidgets.framework`. Installing the
  product package is what links its module, so a Unity build does not normally see it — it means the Xcode
  project was edited after export. Answered before `NotConfigured`, and the message names the module.
- **Android.** `Runtime/Internal/NativeBridge.Android.cs` calls `com.tappgo.sdk.bridge.TappBridge` through
  JNI; `Editor/TappGoAndroidBuildProcessor.cs` declares the Maven dependency of core and of each installed
  product, runs each product's own staging step, and refuses a Minimum API Level below 24.
  `Editor/TappGoDependencies.xml` for hosts with the External Dependency Manager.
- **Widgets, Picture in Picture and widget pinning are new in this version, in `com.tapp.go.widgets`** — its
  changelog lists them. Core's part is the seam: the generated extension entry point hosts what each installed
  product contributes, the app target compiles the sources a product hands over, and the four
  `pictureInPicture*` codes sit in `TappErrorCode` beside the Live Activity ones. On the platform a product
  does not exist on, its calls answer a neutral result (`liveActivitiesUnavailable` and
  `pictureInPictureUnsupported` on Android; the pin calls `false`/`0` on iOS). One API, no `#if`.
- (A `debugLogging` setting was added and removed again during this version's development; no released
  version had it. `configure` does not send the key, which Android SDK `2.1.0` accepts and ignores — its
  console log is a feature of the SDK's own debug build, Tapp-Android D-142.)
- The first `Tapp.Configure` logs the native version (`[Tapp] native …`) and warns when it differs from the
  pinned one.

### Native versions

iOS **2.2.1** (`v2.2.1` on `tappgo/tapp-ios`, resolved as a Swift package) · Android
**`com.tappgo:tapp-core:2.1.0`** (Maven Central; `com.tappgo:tapp-widgets` at the same version when the Widgets
package is installed). Both pins resolve — Android reached Maven Central on 2026-10-04 and iOS reached its tag
on 2026-10-05 — and `make check-native-pin` proves it before a tag.

## [2.0.0] — 2026-09-01

The first release, and the one that makes the C# surface a contract: from here on it is additive only, and
anything removed or renamed costs a major.

**Installed by Git URL**, from the generated public repo
[`tappgo/tapp-unity`](https://github.com/tappgo/tapp-unity) — no registry, no scope, no token. A run of
versions went to npmjs while that channel was being trialled; they were withdrawn, and nothing here depends
on a registry any more.

**Numbered to match the native `TappGo` it embeds**, which is `2.0.0` — so "which binary is in here" is
answerable from the package version alone, and a support question stops needing two lookups. Nothing was ever
published as 1.0.0; the number is skipped rather than retired.

The alignment is a convention, not a mechanism: nothing enforces it, and the day this wrapper needs a release
native did not drive, the two lines part company. **The `Native version` note at the end of every entry is the
authority** — read that rather than inferring the framework from the package version.

### Added

- **Git-URL distribution.** `"com.tapp.go": "https://github.com/tappgo/tapp-unity.git#v2.0.0"` in
  `Packages/manifest.json`. It does not make the development repo installable: a release packs, signs, and
  pushes the *packed tarball's contents* into `tappgo/tapp-unity`, so `.npmignore` remains the only thing
  deciding what a customer can see. Two things to know — `git` must be on your `PATH`, and a Git dependency
  resolves to the ref you name, so the Package Manager offers no update indicator.
- **Signed with Unity's package signature**, attributed to the Tapp organization. Since Unity 6.3 the Package
  Manager warns about any package without one. The signature is an `.attestation.p7m` built by `upm pack`;
  both tarballs are built on every release and their file lists compared, because `upm pack` applies its own
  undocumented exclusions on top of `.npmignore` and drops files *silently* — a case of it dropping
  `*.zip.bytes` for another publisher is on record. Measured at 123 files identical.
- The public C# surface — `Tapp` in namespace `TappGo`: `Configure`, `SetUserId`, `Logout`, `HandleUrl`,
  `StartLiveActivity`, `UpdateLiveActivity`, `EndLiveActivity`, `ActiveLiveActivities`, `SdkVersion`. The type is `Tapp` and the namespace is `TappGo`, mirroring native's `import TappGo` /
  `Tapp` — and they have to differ: a type named `Tapp` inside a namespace rooted at `Tapp` is unreachable,
  because the namespace shadows it for every consumer.
- `EndLiveActivity`'s dismissal is a **duration in whole seconds from the end**, following contract
  revision 8 — how much longer the ended card stays on the Lock Screen, replacing a `DateTimeOffset?
  dismissalDate` that carried an epoch instant. `null` still removes it at once, and so does `0`. The wrapper
  no longer sends a date on any call, which is why `JsonBuilder.AddDate` is gone: three fields are now named
  `seconds`, and they mean different things — countdown length on a start and an update, dismissal delay on
  an end, which ends nothing because the call already did.
- **A Live Activity card is its id *and* its screen**, following contract revision 7. `EndLiveActivity` now
  takes the `entryId` too, and `TappActivityInfo` reports the one each running card is on. One id runs a card
  per screen, so starting a second `EntryId` adds a card rather than being refused, and
  `LiveActivityAlreadyRunning` now means "that id is already up on that screen". Two consequences worth
  reading before upgrading a call site: an update **reloads the card its `entryId` names and never moves one**
  — a card's screen is fixed for life, so showing another screen is a start — and there is no "end
  everything", because a campaign can legitimately have several screens up at once. Nothing has to be
  remembered between launches: `ActiveLiveActivities` hands back both halves of every card's address.
- `TappActivityInfo.UpdateToken` — the APNs update token addressing each running card, lowercase hex, and the
  sample's Live tab shows it with a copy control per row. **Reported only by a Debug-built native framework,
  so it is always `null` in a shipped build** — a push token is diagnostics, not API, so native gates it the
  way it gates its `Tapp+Debug` hooks: the field is absent from a Release framework rather than present and
  empty, and the reply never carries the key. The C# property stays either way, because a wrapper cannot know
  which framework it was built against. Pushes are unaffected either way: the SDK registers every
  token with the back office itself and nothing is forwarded. `null` is ordinary even against a Debug
  framework — iOS issues the token a moment *after* a start, and releases it when a card ends while the system
  goes on listing it — so the row says which of those it is rather than reporting a fault. The value
  **rotates**, so read it rather than storing it. Sourced natively from the App Group token outbox rather than
  from `Tapp.onLiveActivityTokenIssued`, which is an in-memory per-launch Swift closure that cannot cross the
  bridge and would miss any card that outlived a relaunch.
- `TappActivityRequest.Seconds` and `UpdateLiveActivity`'s `seconds` — **how long the design's countdowns
  run, in whole seconds from the activity's start**, not a deadline. It replaces a `DateTimeOffset? EndDate`
  that tracked the native `endDate` through contract revision 5; revision 6 dropped absolute deadlines from
  the entry schema, so a countdown now survives a re-render instead of restarting. Omitted leaves
  whatever clock is running alone, including one an earlier update set — so `0` is a real value ("already
  over"), not a stand-in for absence. On an update it counts from *now*, which makes "five more minutes" one
  call. It still ends nothing.
- **Fonts bundled into the extension**, from the **Fonts Folder** — a base folder, subfolders as you like,
  every `.ttf`/`.otf` under it bundled and declared in `UIAppFonts`. The settings page reads each file and
  lists it by its **PostScript name**, because that is the name a Tapp design has to ask for and it is very
  often not the file name: `Arial.ttf` registers as `ArialMT`. The SDK remaps a declared name to the
  registered one only for fonts it downloaded itself, so for a bundled font the design must name it exactly —
  get it wrong and the text renders in a system font, on a device, with nothing logged. Files carrying no
  PostScript name, or two carrying the same one, are called out.
- **Font animations bundled into the extension**, from the **Font Animations Folder** — the second of the two
  optional font folders in **Project Settings → Tapp**. Frame fonts only render in a Live Activity when they
  are bundled and declared in the extension's `UIAppFonts` — a font downloaded at runtime never reaches
  WidgetKit's out-of-process renderer — so the build hook stages both folders and writes that key. It is a
  **base folder holding one subfolder per animation** (`FontAnimations/SlotFont/SlotFont_0.ttf`,
  `FontAnimations/CoinSpin/CoinSpin_0.ttf`), and those subfolders are for your own tidiness alone: Xcode
  flattens folders into the bundle root, so **an animation is identified by the name its frame files share** —
  `SlotFont`, the name a Tapp design refers to it by — and two animations need two different file-name stems.
  The settings page lists what it found, name and frame count, and warns about the three things that are
  otherwise silent all the way to a device: a frame number two files claim, a font that isn't numbered at all,
  and a gap in the numbering — frames render in order, so files that went missing shorten the animation
  without failing anything. Tapp ships the shared mask font every animation uses inside the framework, so
  that one needs nothing.
- `TappGo.xcframework` and the `@_cdecl` Swift shim, so calls reach the native SDK on device. Unity
  compiles the shim into `UnityFramework`, which is what makes `DllImport("__Internal")` resolve.
- An iOS build hook that embeds and signs the framework in the app bundle, and refuses a project whose
  deployment target is below iOS 15.0.
- **Two iOS floors, and your app target only has to clear the lower one.** `TappGo.xcframework` links at
  **15.0** while the SDK runs at **17.2**: between them every call returns its neutral answer and logs one
  line saying it did nothing, so a game shipping iOS 15 integrates and ships rather than being unable to
  resolve the dependency at all. The Live Activity **extension** target is built at 17.2 and you never set
  it — it names `TappLiveActivity`, which native gates `@available(iOS 17.2, *)`, and ActivityKit does not
  exist below the runtime floor. `make verify-ios` asserts both floors, from both directions.
- The **Live Activity extension target**, created in the generated Xcode project on every build: its
  sources staged from the package, the framework linked to it, and the App Group given to both targets —
  plus `keychain-access-groups` and `NSSupportsLiveActivities` on the app, which the extension
  deliberately does not get. Nothing to add by hand in Xcode.
- Its name and Lock Screen display name come from **Project Settings → Tapp**.
- `TappGoSettings` with a **Project Settings → Tapp** panel that validates the App Group before a build can
  fail on it.
- A **first-run window**, opened once per project when the package is first resolved: what the wrapper does,
  the two settings an iOS build is refused without, and the current state of both. Reopen from
  **Tapp → Welcome**. Editor-only, and suppressed in batch mode so it can never appear in a CI build.
- `TappActivityState` as an open set of string constants rather than an enum, so a state introduced by a
  newer iOS round-trips instead of throwing.
- Editor stub, so Play mode logs rather than throwing `DllNotFoundException`. **A shipped non-iOS player is
  silent instead**: no auto-configure, no per-call lines, nothing at launch. Android is not part of this
  version, and a player that logged a native version and "Configured on launch" would be describing work it
  did not do, in someone else's product. The package still compiles for every platform — only iOS does
  anything.
- **Live Activity Demo** sample: a console with every call as a button and the answer printed, plus the
  same lifecycle written the way a game would call it. No prefab, no Canvas, and no package you don't
  already have — a sample that pulled in uGUI or TextMeshPro would force them on every project.
- Deep-link handling in the sample, including the cold-start URL that the `deepLinkActivated` event
  never fires for.

### Known limitations

- **iOS builds must use *Replace*, not *Append*.** An Append build fails with an explanation: the hook's
  edits are additive and cannot be applied to a project that already has them.

### Published

- **`tappgo/tapp-unity`, tag `v2.0.0`** — public, generated, and holding the published tarball unpacked at
  the repo root. Written only by `Tools/push-dist-repo.sh` from the release workflow; a direct push of the
  development repo would publish `docs/`, `Tools/`, `TestProject~/` and `CLAUDE.md`, which starting from the
  packed artifact makes unavailable rather than merely discouraged.

### Verified on a device

- A Live Activity **ran on an iPhone**, against the signed Release `TappGo` 2.0.0 embedded in an arm64
  device build: `[Tapp] native 2.0.0` at launch, the card rendered, and start → update → end all worked.
  The rendering matters as much as the calls — a card that appears blank is how a wrong App Group presents
  itself, and the Simulator cannot tell you, because it does not enforce entitlements.
- **And again from a *Replace* build** — a project regenerated over an existing one and never opened or
  edited by hand, which is what proves the integration needs no manual Xcode step. Worth stating because the
  build hook is the entire integration: if a card only rendered after someone fixed something in Xcode, every
  customer would have to find that fix themselves, and nothing in the package would tell them so.

### Not yet here

- **A signature that is checked.** The package *is* signed — but Unity verifies a package signature on a
  registry resolve, and this one installs by Git URL. Expect Unity 6.3's "unsigned packages … potentially
  unsafe" notice anyway; it is not a sign of a bad download. See `docs/DISTRIBUTION-PLAN.md` §8.
- **A counsel-reviewed licence.** `LICENSE.md` ships as authored, by decision, and these are this version's
  terms permanently. The two open questions are recorded in the file.
- Android.

**Native version:** `2.0.0`, tag `v2.0.0` (`2141757`) — a clean tree at a tag, so the bytes are ones anyone
can rebuild. Staged with `TAPP_NATIVE_STRICT=1`, which is what refuses anything less.
