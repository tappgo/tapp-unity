# Live Activity Demo

Two scripts. Neither needs a prefab, a Canvas, or any package you don't already have.

| | |
|---|---|
| `LiveActivityConsole.cs` | Every call as a button, with the answer printed. Drop it on a GameObject and press Play. |
| `ActivityDemo.cs` | The same lifecycle written the way a game would call it — start at kick-off, swap to the results card at full time, end after. Read this one; run the other. |

## Running it

1. Add an empty GameObject to a scene and put `LiveActivityConsole` on it.
2. Fill in **Activity id** with an id from your Tapp back office, and **Screen** with one of its screens
   (`screen-1` by default). Start it twice with different screens to see two cards of one campaign.
3. Set your App Group in **Project Settings → Tapp**, and build to a **device**.

In the Editor every call answers `editor-stub` and nothing renders — Live Activities exist only on real
hardware, and the Simulator does not enforce the App Group entitlement that makes them work. A build that
looks fine in either proves nothing.

## What the console is showing you

- **A row is a card, and a card is the id plus the screen.** Starting a second screen of the same id adds a
  row rather than replacing one — nothing moves a card between screens — and each row's **Update** and
  **End** address that card alone. You never build the content; the design is resolved by the SDK, which is
  what lets it change without a new build.
- **Update** with the duration stepper at 0 only re-resolves the card's content; set it and the countdown
  restarts from the full duration, which is the one thing about a running card a host usually changes.
- **End now** versus **End, dismiss in 30 min** is the distinction worth seeing once: ending stops the
  activity immediately either way, while the dismissal delay only governs how much longer it stays on the
  Lock Screen. iOS caps that at four hours without saying so, and it **cannot be revised** once sent.
- **Active activities** asks iOS, not the app. Force-quit, relaunch, and press it — the activity is still
  listed, because activities outlive the process that started them, and each row carries the screen it is
  on, so the buttons work on a card this launch never started.
- The deep-link lines appear when a tap on the activity opens the app. Handing every URL to
  `Tapp.HandleUrl` is the whole integration, and an open that skips it is one Tapp cannot see.
