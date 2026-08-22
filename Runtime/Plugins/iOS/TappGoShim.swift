//
//  TappGoShim.swift
//  com.tapp.go
//
//  The C entry points Unity's `DllImport("__Internal")` resolves against.
//
//  Every function here is marshalling and nothing else: read a C string, call one `TappBridge` member,
//  hand back a C string. There is no branching, no state, no caching and no default-filling — if this file
//  ever needs to decide something, the decision belongs in the native SDK where it is tested.
//
//  Why the shim is a source file in the Unity package rather than a symbol inside TappGo.xcframework:
//  `__Internal` resolves against the binary Unity links, which is UnityFramework. A `@_cdecl` symbol
//  living in a prebuilt framework is at the mercy of dead-stripping and link flags in a host Xcode
//  project nobody controls. Compiled here, it is part of the build that needs it.
//
//  The symbol names are a published contract — see docs/NATIVE-CONTRACT.md. Renaming one breaks every
//  version of this package a customer has already shipped.
//

import Foundation
import TappGo

// MARK: - Strings across the ABI

/// The callback shape every asynchronous entry point takes.
///
/// `context` is opaque: it comes from the managed side, is never read here, and is handed back untouched.
/// The `char *` is valid only for the duration of the call — the managed side copies before returning.
public typealias TappGoReply = @convention(c) (UnsafeMutableRawPointer?, UnsafePointer<CChar>?) -> Void

/// Reads a NUL-terminated UTF-8 C string, treating absence as an empty payload.
///
/// A `nil` or malformed pointer deliberately produces `""` rather than an early return: `""` fails to
/// decode in `BridgeCodec` and comes back as a proper `invalidRequest` envelope naming the missing field.
/// Special-casing it here would mean inventing a second failure path outside the one the native side tests.
private func payload(_ cString: UnsafePointer<CChar>?) -> String {
    guard let cString else { return "" }
    return String(decoding: UnsafeRawBufferPointer(start: cString, count: strlen(cString)), as: UTF8.self)
}

/// Copies an envelope into a `malloc`'d C string the caller releases with `TappGo_FreeString`.
///
/// Returns `nil` only when the allocation itself failed, which is out-of-memory and the one case where no
/// envelope can be produced. The managed side reports that as `unexpected`, so the "always a valid
/// envelope" guarantee still holds where a host can observe it.
private func handOff(_ envelope: String) -> UnsafeMutablePointer<CChar>? {
    strdup(envelope)
}

/// The `context` + callback pair, carried across the `async` boundary as one value.
///
/// `@unchecked Sendable` is safe here because nothing ever reads through `context`. It is a token the
/// managed side minted, and the only thing done with it is passing it back, so there is no shared memory
/// for a second thread to race on. `@convention(c)` function pointers are already `Sendable`.
private struct ReplyTarget: @unchecked Sendable {
    let context: UnsafeMutableRawPointer?
    let reply: TappGoReply?

    func send(_ envelope: String) {
        envelope.withCString { reply?(context, $0) }
    }
}

/// Releases a string returned by any of the synchronous entry points.
///
/// Every `char *` this file returns is owned by the caller. Nothing is cached, nothing is static, and
/// there is no second way to free one.
@_cdecl("TappGo_FreeString")
public func TappGo_FreeString(_ value: UnsafeMutablePointer<CChar>?) {
    free(value)
}

// MARK: - Diagnostics

/// The version of the `TappGo` binary this package carries.
///
/// The cheapest proof the whole chain is wired: symbol resolved, string marshalled, envelope parsed. It
/// needs no configuration and cannot fail for anything a host did, which is what makes it the first thing
/// to call when an integration is new.
@_cdecl("TappGo_SDKVersion")
public func TappGo_SDKVersion() -> UnsafeMutablePointer<CChar>? {
    handOff(TappBridge.sdkVersion())
}

// MARK: - Configuration

@_cdecl("TappGo_Configure")
public func TappGo_Configure(_ json: UnsafePointer<CChar>?) -> UnsafeMutablePointer<CChar>? {
    handOff(TappBridge.configure(json: payload(json)))
}

// MARK: - Identity

@_cdecl("TappGo_SetUserID")
public func TappGo_SetUserID(_ json: UnsafePointer<CChar>?) -> UnsafeMutablePointer<CChar>? {
    handOff(TappBridge.setUserID(json: payload(json)))
}

/// Takes no payload, by design — a `(context, reply)` signature beats requiring an empty JSON object.
@_cdecl("TappGo_Logout")
public func TappGo_Logout(_ context: UnsafeMutableRawPointer?, _ reply: TappGoReply?) {
    let target = ReplyTarget(context: context, reply: reply)
    TappBridge.logout { target.send($0) }
}

// MARK: - Deep links

@_cdecl("TappGo_HandleURL")
public func TappGo_HandleURL(_ json: UnsafePointer<CChar>?) -> UnsafeMutablePointer<CChar>? {
    handOff(TappBridge.handleURL(json: payload(json)))
}

// MARK: - Live Activities

@_cdecl("TappGo_StartLiveActivity")
public func TappGo_StartLiveActivity(
    _ json: UnsafePointer<CChar>?,
    _ context: UnsafeMutableRawPointer?,
    _ reply: TappGoReply?
) {
    let target = ReplyTarget(context: context, reply: reply)
    TappBridge.startLiveActivity(json: payload(json)) { target.send($0) }
}

@_cdecl("TappGo_UpdateLiveActivity")
public func TappGo_UpdateLiveActivity(
    _ json: UnsafePointer<CChar>?,
    _ context: UnsafeMutableRawPointer?,
    _ reply: TappGoReply?
) {
    let target = ReplyTarget(context: context, reply: reply)
    TappBridge.updateLiveActivity(json: payload(json)) { target.send($0) }
}

@_cdecl("TappGo_EndLiveActivity")
public func TappGo_EndLiveActivity(
    _ json: UnsafePointer<CChar>?,
    _ context: UnsafeMutableRawPointer?,
    _ reply: TappGoReply?
) {
    let target = ReplyTarget(context: context, reply: reply)
    TappBridge.endLiveActivity(json: payload(json)) { target.send($0) }
}

@_cdecl("TappGo_ActiveLiveActivities")
public func TappGo_ActiveLiveActivities() -> UnsafeMutablePointer<CChar>? {
    handOff(TappBridge.activeLiveActivities())
}
