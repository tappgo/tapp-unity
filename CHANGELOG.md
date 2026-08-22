# Changelog

All notable changes to `com.tapp.go`. Follows [Semantic Versioning](https://semver.org).

Each entry names the **native `TappGo` version it embeds** — the package's version and the framework's are
independent, and a wrapper that silently changed which binary it carries is the bug this line exists to
prevent.

## [2.0.0] — unreleased

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
