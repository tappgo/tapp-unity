# Tapp for Unity — Core

The core of Tapp for Unity: **configuration and the player's identity**, shared by every Tapp product.

Tapp is **one package per product**, so a project installs what it uses and nothing else:

| Package | What it adds | Repo |
|---|---|---|
| `com.tapp.go` — **this one, always** | `Tapp`: configure, `SetUserId`, `Logout`, `HandleUrl` | [`tappgo/tapp-unity`](https://github.com/tappgo/tapp-unity) |
| `com.tapp.go.liveactivities` | `TappLiveActivities`: Live Activities on iOS | [`tappgo/tapp-liveactivities-unity`](https://github.com/tappgo/tapp-liveactivities-unity) |
| `com.tapp.go.widgets` | `TappWidgets`: home-screen widgets on iOS and Android, Picture in Picture on iOS | [`tappgo/tapp-widgets-unity`](https://github.com/tappgo/tapp-widgets-unity) |

No Tapp package ships native code. On every build, core asks Xcode to resolve the Tapp iOS SDK as a Swift
package and Gradle to resolve the Tapp Android SDK from Maven Central, and links **core plus the native module
of each product package you installed** — a product you did not install is not downloaded, linked, embedded or
callable. There is nothing to set up by hand in Xcode or Gradle.

---

## Requirements

| | |
|---|---|
| Unity | 2022.3 LTS or newer |
| Scripting backend | IL2CPP on iOS |
| iOS deployment target | **15.0** or newer to link, **17.2** to run — below 17.2 the SDK is inert (see below) |
| Xcode | 16.4 or newer |
| Android minimum API | **24** to link, **26** to run — below 26 the SDK is inert |
| Network at build time | The first iOS build resolves the Swift package from GitHub; the first Android build resolves the artifact from Maven Central |

### The two iOS floors

The Tapp iOS SDK **links** at iOS 15.0 and only **runs** at iOS 17.2, so your app target only has to name
15.0. Between 15.0 and 17.1 every entry point returns its neutral answer and logs one line saying it did
nothing — nothing throws, nothing crashes, and no widget or Live Activity appears.

The widget extension target is a separate matter: the build hook builds it at 17.2 and you do not set it.
It renders Home Screen widgets and Lock Screen cards, and there is no version of that worth running below
the runtime floor.

## Install

Add core **and each product you use** to `Packages/manifest.json`, all at the **same tag**:

```json
{
  "dependencies": {
    "com.tapp.go": "https://github.com/tappgo/tapp-unity.git#v2.1.0",
    "com.tapp.go.liveactivities": "https://github.com/tappgo/tapp-liveactivities-unity.git#v2.1.0",
    "com.tapp.go.widgets": "https://github.com/tappgo/tapp-widgets-unity.git#v2.1.0"
  }
}
```

Leave out the line of a product you don't use. **Core is never optional**: a Git install does not pull it in
for you, and a product without it fails to resolve with an error naming `com.tapp.go`.

**Keep every Tapp line on one tag.** The packages are released together, and Unity does not compare Git
dependencies against each other — so core checks instead: a mixed set shows in Project Settings → Tapp and
**fails the build**, naming each package and its version.

**No token and no login** — the repos are public. **`git` does have to be on your `PATH`**, because Unity
shells out to it, and the error when it is missing names neither git nor this package. Each repo is generated:
a tag holds exactly the package released at that version. They are not where the SDK is developed, and take no
issues or pull requests.

**Always name a tag.** A Git URL with no `#ref` follows the default branch, which moves on every release.

### Upgrading from the single 2.0 package

The 2.0 package was Live Activities only. Keep your `com.tapp.go` line, move it to the new tag, and add the
`com.tapp.go.liveactivities` line beside it — without that line the build links no Live Activities at all.
Your `TappGoSettings.asset` stays where it is. Open **Project Settings → Tapp** and press *Create Live
Activities settings*: the new asset is filled from the app id and push switch your old one held — do it before
you commit a re-saved `TappGoSettings.asset`, since only the machine that upgraded keeps those two values once
the old asset is saved. In code, the Live Activity calls moved to a class of their own —
`Tapp.StartLiveActivity` → `TappLiveActivities.Start` — and that package's README lists them. Widgets and
Picture in Picture are new in 2.1: add `com.tapp.go.widgets` to use them.

**Tapp no longer configures itself on launch.** The *Auto configure on launch* setting is gone: add one
`Tapp.Configure()` call to your startup code (see [Use](#use)). A build whose settings asset still has the old
switch on logs a warning as a reminder.

## Configure

**Tapp → Settings…** (or Project Settings → Tapp). One page for everything: core's settings on top, then a
section per installed product. Core needs your **App Group**; the panel tells you what's missing. Each
settings object is an asset under `Assets/Resources/` — commit them.

Two optional folders bundle your own fonts into the iOS widget extension, which is the only place a widget
or Live Activity can render one from — a font the SDK downloads at runtime never reaches WidgetKit's
renderer. Both take a **base folder** and look through every subfolder under it, and the panel lists what it
found in each:

- *Font Animations Folder* — one subfolder per animation, `Assets/TappAssets/FontAnimations/SlotFont/SlotFont_0.ttf`,
  `SlotFont_1.ttf`, …  The subfolders are for your tidiness: an animation is identified by the name its frame
  files share, so give two animations two different names.
- *Fonts Folder* — your ordinary fonts, arranged however you like. Ask for them in your Tapp design by the
  **PostScript name** the panel shows, not by the file name; they are often not the same (`Arial.ttf`
  registers as `ArialMT`), and a design that names the wrong one renders in a system font without complaining.

## Use

**Tapp does nothing in your game until you call `Tapp.Configure()`** — no native call, no network, no
session. Call it once at startup, or after your own login flow if Tapp should wait for it.

```csharp
using TappGo;

var configured = Tapp.Configure();      // reads the settings above; once, before any other Tapp call
if (!configured.Ok) Debug.LogError(configured);

Tapp.SetUserId(player.Id);              // safe on every launch; a different id ends the previous session
Tapp.Logout(result => { });             // back to a guest session
Debug.Log(Tapp.SdkVersion().Value);     // the first thing to check when an integration isn't working
```

Hand every URL your app opens with to `Tapp.HandleUrl` — it claims the ones Tapp sent and returns `false` for
yours, so your own routing carries on. Callbacks arrive on the **main thread**.

## What to expect where

- **In the Editor**, every call logs and returns its neutral answer without rendering. There is no native
  SDK in Play mode.
- **On the iOS Simulator**, widgets and Live Activities do not work. The Simulator doesn't enforce App Group
  entitlements, so a misconfigured build renders an empty placeholder instead of failing.
- **On a device** is the only place this is real. Verify there before you ship.
- **Building offline** fails at dependency resolution — the Swift package and the Maven artifact are fetched
  by Xcode and Gradle on the first build, and cached after.

## Errors

Every call reports a `TappResult`. Branch on `Code` (see `TappErrorCode`), log `Message`.

| Code | What to do |
|---|---|
| `invalidConfiguration` | The message says which setting — fix it in Project Settings → Tapp. |
| `notConfigured` | `Configure` wasn't called, or didn't succeed — call it at startup and check its result. |
| `invalidRequest` | The call was refused for what it was given — an empty id, a missing URL. The message names it; fix the call. If the message names nothing you passed, the package and the native SDK disagree: send us the log. |
| `unexpected` | A defect in Tapp, not in your integration. Send us the log. |

Each product's README lists the codes that are its own.

## Links

- [Documentation](https://documentation.tappgo.com/v2/unity/overview)
- [Changelog](CHANGELOG.md)
