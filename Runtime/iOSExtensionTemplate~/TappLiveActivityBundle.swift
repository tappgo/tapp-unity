//
//  TappLiveActivityBundle.swift
//  Tapp Live Activity extension
//
//  GENERATED — do not edit in your Xcode project.
//
//  Unity regenerates the Xcode project on every Replace build, and the Tapp build hook re-stages this file
//  from the package each time. Anything you change here is lost on the next build. The design of what a
//  Live Activity looks like is configured server-side, not here.
//
//  Tokens substituted at stage time (the hook fails the build if any survives):
//    __TAPP_APP_GROUP__   the App Group shared with the host app
//
//  This target needs the App Group and nothing else — in particular NOT keychain-access-groups. A Live
//  Activity is rendered by a process the system launches for a Lock Screen presentation, and that process
//  cannot reliably reach the data-protection Keychain, so what it renders is deliberately stored
//  unencrypted. Adding the Keychain entitlement here would suggest otherwise and change nothing.
//

import SwiftUI
import WidgetKit
import TappGo

@main
struct TappLiveActivityBundle: WidgetBundle {

    /// Hands the SDK the App Group before anything renders.
    ///
    /// iOS offers no way to discover this identifier from inside an extension — the entitlement that
    /// declares it is unreadable at runtime — so it has to be passed in, and this is the only moment
    /// available: a pushed Live Activity is created by the system, so no app code and no widget setup runs
    /// in this process.
    ///
    /// The throw is swallowed on purpose. The SDK has already logged the reason through `TappLog`, and
    /// throwing out of a `WidgetBundle` initialiser kills the extension process — which the system reports
    /// as a blank card, with nothing to read anywhere.
    init() {
        try? Tapp.configureLiveActivity(appGroupIdentifier: "__TAPP_APP_GROUP__")
    }

    var body: some Widget {
        TappLiveActivity()
    }
}
