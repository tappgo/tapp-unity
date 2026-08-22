# Tapp for Unity

Server-configured **Live Activities** for iOS, from Unity — without shipping a new app build.

This package wraps the native `TappGo.xcframework` and adds the required widget extension to your Xcode
project on every iOS build, so there is nothing to set up by hand in Xcode.

---

## Requirements

| | |
|---|---|
| Unity | 2022.3 LTS or newer |
| Scripting backend | IL2CPP |
| iOS deployment target | **15.0** or newer to link, **17.2** to run — below 17.2 the SDK is inert (see below) |
| Xcode | 16 or newer |
| Platforms | iOS. Android is planned. |

### The two iOS floors

`TappGo.xcframework` **links** at iOS 15.0 and the SDK only **runs** at iOS 17.2, so your app target only has
to name 15.0. Between 15.0 and 17.1 every entry point returns its neutral answer and logs one line saying it
did nothing — nothing throws, nothing crashes, and no Live Activity appears. Live Activities are ActivityKit,
which does not exist down there.

The Live Activity extension target is a separate matter: the build hook builds it at 17.2 and you do not set
it. It renders Lock Screen cards and there is no version of that worth running below the runtime floor.

## Install

Add the package to `Packages/manifest.json`, naming the tag you want:

```json
{
  "dependencies": {
    "com.tapp.go": "https://github.com/tappgo/tapp-unity.git#v2.0.0"
  }
}
```

**No token and no login** — the repo is public. **`git` does have to be on your `PATH`**, because Unity
shells out to it, and the error when it is missing names neither git nor this package. Or add the same URL
through **Window → Package Manager → + → Install package from git URL**.

[`tappgo/tapp-unity`](https://github.com/tappgo/tapp-unity) is generated: each tag holds exactly the package
released at that version and nothing else. It is not where the SDK is developed, and it takes no issues or
pull requests.

**Always name a tag.** A Git URL with no `#ref` follows the default branch, which moves on every release — so
the version your project resolves would change under you, and `packages-lock.json` is the only thing that
would record when. Unity has no version ranges and a Git dependency shows no update indicator, so moving
version is an explicit edit; watch [the releases](https://github.com/tappgo/tapp-unity/releases).

## Configure

**Tapp → Settings…** (or Project Settings → Tapp). Fill in your App Group and Tapp app id; the panel tells
you what's missing. The settings live at `Assets/Resources/TappGoSettings.asset` — commit it.

Two optional folders bundle your own fonts into the widget extension, which is the only place a Live Activity
can render one from — a font the SDK downloads at runtime never reaches WidgetKit's renderer. Both take a
**base folder** and look through every subfolder under it, and the panel lists what it found in each:

- *Font Animations Folder* — one subfolder per animation, `Assets/TappAssets/FontAnimations/SlotFont/SlotFont_0.ttf`,
  `SlotFont_1.ttf`, …  The subfolders are for your tidiness: an animation is identified by the name its frame
  files share, so give two animations two different names.
- *Fonts Folder* — your ordinary fonts, arranged however you like. Ask for them in your Tapp design by the
  **PostScript name** the panel shows, not by the file name; they are often not the same (`Arial.ttf`
  registers as `ArialMT`), and a design that names the wrong one renders in a system font without complaining.

The **Tapp** menu also imports the Live Activity sample, and reports which package version you have.

Tapp configures itself before the first scene loads. If you need to configure after your own login flow,
turn off *Auto configure on launch* and call `Tapp.Configure()` yourself.

## Use

```csharp
using TappGo;

Tapp.StartLiveActivity(new TappActivityRequest {
    Id      = "match-4471",   // from your Tapp back office
    EntryId = "screen-1",
    Seconds = 90 * 60,        // how long the countdowns run — a duration, not a deadline
}, result => {
    if (!result.Ok) Debug.LogWarning(result.Code);
});

Tapp.UpdateLiveActivity("match-4471", "screen-1");   // reloads that card, or restates its clock
Tapp.EndLiveActivity("match-4471", "screen-1", 30 * 60);  // stays on screen half an hour more
```

**A card is the id and the screen together.** One activity id runs one card per screen, so starting
`"screen-2"` puts a second card up beside the first rather than moving it — a card never leaves the screen it
started on — and updates and ends name the pair. `Tapp.ActiveLiveActivities()` reports both halves for
everything that is up, which is what makes a card started before a relaunch drivable without your keeping
notes.

You never build the content, and you never hold an identifier you didn't choose: the pair addresses a card
for its whole life. Callbacks arrive on the **main thread**.

Hand every URL your app opens with to `Tapp.HandleUrl` — it claims the ones a Tapp Live Activity sent and
returns `false` for yours, so your own routing carries on. An open that skips it is one Tapp cannot see.

Import **Live Activity Demo** from the Package Manager: a console with every call as a button, and the same
lifecycle written the way a game would call it. Drop `LiveActivityConsole` on a GameObject and press Play —
it needs no prefab, no Canvas, and no package you don't already have.

## What to expect where

- **In the Editor**, every call logs and returns successfully without rendering. There is no native
  framework in Play mode.
- **On the Simulator**, Live Activities do not work. The Simulator doesn't enforce App Group entitlements,
  so a misconfigured build renders an empty placeholder instead of failing.
- **On a device** is the only place this is real. Verify start, update and end there before you ship.

## Errors

Every call reports a `TappResult`. Branch on `Code` (see `TappErrorCode`), log `Message`. Two of them are
routine rather than exceptional:

| Code | What to do |
|---|---|
| `liveActivitiesUnavailable` | The player switched Live Activities off. Carry on silently. |
| `liveActivityAlreadyRunning` | That id already has a card on that screen. Update it, end it, or start a different screen. |
| `notConfigured` | `Configure` hasn't succeeded — check Project Settings → Tapp. |
| `invalidRequest` / `unexpected` | A defect in Tapp, not in your integration. Send us the log. |

## Links

- [Documentation](https://documentation.tappgo.com/v2/unity/overview)
- [Changelog](CHANGELOG.md)
