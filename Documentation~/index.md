# Tapp for Unity

Package Manager shows this when `documentationUrl` is unreachable, so it has to stand alone offline.

- **Getting started, API reference, error codes** → [README](../README.md)
- **What changed, and which native version each release carries** → [CHANGELOG](../CHANGELOG.md)

## Where this package comes from

`com.tapp.go` is installed by **Git URL**, from the public repo
[`tappgo/tapp-unity`](https://github.com/tappgo/tapp-unity). Your project's `Packages/manifest.json` names
the tag:

```json
"dependencies": {
  "com.tapp.go": "https://github.com/tappgo/tapp-unity.git#v2.0.0"
}
```

That repo is generated. Each tag holds exactly the package that was released at that version and nothing
else — it is not where the SDK is developed, and it takes no issues or pull requests. Four things that are
otherwise learned the hard way:

- **No credentials, but `git` must be on your `PATH`.** The repo is public, so there is no token and no
  login. Unity does shell out to `git`, and the error when it is missing names neither git nor this package.
- **Always name a tag.** A Git URL with no `#ref` follows the default branch, which moves on every release —
  so the version your project resolves would change under you, and `packages-lock.json` is the only thing
  that would record when.
- **Unity has no version ranges**, and a Git dependency has no update indicator at all. Moving version is
  editing the tag by hand. Read the CHANGELOG first — a major means something public changed.
- **The package is signed**, so it does not trigger Unity 6.3's "unsigned packages … potentially unsafe"
  warning where that signature is checked. The `.attestation.p7m` is verified on a registry resolve, so do
  not be surprised if a Git-URL install shows the notice anyway; it is not a sign of a bad download.

### Build machines

The first resolve needs network access to `github.com` and a `git` binary. After that the package is
extracted into your project's `Library/PackageCache` and builds work offline, so a cold CI checkout is the
only place network matters. Nothing needs a secret, which means nothing breaks when one rotates.

## The one step Tapp cannot do for you

Tapp adds the widget extension to your Xcode project on every iOS build. It **cannot sign it**: a second
target needs its own bundle id and provisioning profile.

- **Xcode, automatic signing** — nothing to do. Xcode provisions both targets on first build.
- **CI** — pass `-allowProvisioningUpdates` to `xcodebuild`, or set `PROVISIONING_PROFILE_SPECIFIER` and
  `CODE_SIGN_IDENTITY` on the extension target as well as the app.

A missing extension profile is the most common cloud-build failure for any SDK that ships a widget. If your
build fails there, this is why.

The extension is named from **Project Settings → Tapp**, and its bundle id is your app's with that name
appended. Changing the name after a release orphans the extension your users already have installed, so
pick it once.

## Build iOS with *Replace*, not *Append*

Tapp refuses an Append build, with an error saying so. Appending gives the hook a project it has already
modified, and every edit it makes is additive — a second extension target with the same bundle id, the
same source compiled twice, the App Group listed twice. Some of those fail in Xcode; the rest fail at
submission. Replace regenerates the project, which is what the whole integration is built on.

If you have hand-made Xcode changes you were relying on Append to preserve, they need to move into a build
hook of your own. That is also true of every other SDK that touches the project.

## Why the Simulator isn't proof

The Simulator does not enforce App Group entitlements. A build with the wrong App Group renders an empty
Live Activity there instead of failing, so it looks like a design problem rather than a configuration one.

Verify start, update and end **on a device** — then once more after a *Replace* build, which is what proves
nothing depended on a manual Xcode edit.
