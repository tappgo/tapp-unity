# Tapp Unity SDK — Software Licence Agreement

Copyright (c) 2026 TAPP AI LTD ("Tapp"). All rights reserved.

READ THIS BEFORE USING THE SOFTWARE. By downloading, installing, importing into a
Unity project, building against, or otherwise using the Tapp Unity SDK (the
"Software"), you ("you" or "Licensee") agree to these terms. If you do not agree,
do not use the Software.

## 1. THE SOFTWARE

The Software is the Tapp Unity SDK, distributed as the Unity package
`com.tapp.go`. It comprises:

   (a) **Wrapper source.** C# source code, assembly definitions, editor tooling,
       and the iOS widget-extension templates. Unity compiles this source in your
       project, so it is necessarily readable by you. It is provided to you as
       source **for that purpose only**; it is not open source, and section 3
       applies to it in full.

   (b) **The native binary.** `TappGo.xcframework`, a compiled binary framework.
       Its source code is not distributed and is not part of the Software.

   (c) **Samples and documentation**, including the contents of `Samples~/` and
       `Documentation~/`.

Third-party components and their notices, if any, are listed in
`Third Party Notices.md`, and are licensed under the terms stated there rather
than under this file.

## 2. LICENCE GRANT

   2.1 **The Software is free to use.** Tapp grants you a non-exclusive,
       non-transferable, non-sublicensable licence, at no charge and with no time
       limit, to install and use the Software to develop, test, and distribute your
       own applications that use the Tapp service. No separate agreement is needed
       to obtain, integrate, evaluate, or ship the Software.

   2.2 **The Tapp service is not.** The Software does nothing without credentials
       issued by Tapp, and those are granted under a separate written agreement
       between you and Tapp (a "Commercial Agreement"), which is where fees, service
       levels, and permitted volumes live. Nothing in this file entitles you to the
       service, to credentials, or to any particular price.

   2.3 **Samples.** Notwithstanding section 3(c), you may copy, modify, and
       distribute the contents of `Samples~/` as part of your own application.
       Sample code is provided as illustration, without warranty, and is not
       supported.

   2.4 **No other rights.** All rights not expressly granted are reserved. This
       file grants no right to the Tapp service itself, which requires credentials
       issued by Tapp.

## 3. RESTRICTIONS

You may not:

   (a) redistribute, publish, sublicense, sell, rent, lease, or lend the Software,
       except as embedded in and not separable from your own application, and
       except as section 2.3 permits for samples;

   (b) reverse engineer, decompile, or disassemble the native binary described in
       section 1(b), or attempt to derive its source code, except and only to the
       extent that applicable law expressly permits despite this restriction;

   (c) modify the Software, create derivative works of it, or reuse the wrapper
       source described in section 1(a) — in whole or in substantial part — in any
       software other than an application that uses the Tapp service;

   (d) remove, obscure, or alter any copyright, trade mark, or other proprietary
       notice, or the native binary's code signature;

   (e) use the Software to build, or to assist anyone in building, a product or
       service that competes with the Tapp service; or

   (f) use the Software other than in accordance with applicable law and the terms
       of the platforms on which your application is distributed.

## 4. OWNERSHIP

The Software is licensed, not sold. Tapp and its licensors retain all right,
title, and interest in and to the Software, including all intellectual property
rights in it. Your readable access to the wrapper source does not transfer any
right in it.

## 5. DATA

The Software transmits data to Tapp in order to function, and reports product
analytics. What it sends is documented at <https://documentation.tappgo.com/v2/unity/overview>, and
the native binary ships an Apple privacy manifest declaring its data collection
and its use of required-reason APIs. You are responsible for your own
application's privacy disclosures, including any App Store privacy declarations
that follow from your use of the Software, and for having any consent or notice
your jurisdiction requires for the data your application causes to be sent.

## 6. UPDATES AND CHANGES

Tapp may issue new versions of the Software. Tapp is under no obligation to
maintain, support, or continue to make available any version, or to support any
particular version of the Unity Editor.

## 7. TERM AND TERMINATION

This licence takes effect when you first use the Software and continues until
terminated. It terminates automatically if you breach any term. **It does not end
when a Commercial Agreement does** — the Software is licensed separately from the
service (sections 2.1 and 2.2), so losing access to the service leaves you holding
a working licence to software that has nothing to talk to. On termination of this
licence you must stop using the Software and remove it from your development
environments and from applications you distribute thereafter. Sections 3, 4, 8, 9,
and 10 survive termination.

## 8. NO WARRANTY

THE SOFTWARE IS PROVIDED "AS IS" AND "AS AVAILABLE", WITHOUT WARRANTY OF ANY KIND,
EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE IMPLIED WARRANTIES OF
MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE, TITLE, AND NON-INFRINGEMENT.
TAPP DOES NOT WARRANT THAT THE SOFTWARE WILL BE UNINTERRUPTED OR ERROR-FREE, OR
THAT IT WILL OPERATE WITH ANY PARTICULAR HARDWARE, OPERATING SYSTEM VERSION, UNITY
VERSION, SCRIPTING BACKEND, OR THIRD-PARTY SOFTWARE.

## 9. LIMITATION OF LIABILITY

TO THE MAXIMUM EXTENT PERMITTED BY APPLICABLE LAW, TAPP SHALL NOT BE LIABLE FOR ANY
INDIRECT, INCIDENTAL, SPECIAL, CONSEQUENTIAL, OR PUNITIVE DAMAGES, OR FOR ANY LOSS
OF PROFITS, REVENUE, DATA, OR GOODWILL, ARISING OUT OF OR RELATING TO THE SOFTWARE.
TAPP'S TOTAL AGGREGATE LIABILITY ARISING OUT OF OR RELATING TO THE SOFTWARE SHALL
NOT EXCEED THE GREATER OF (A) THE AMOUNTS YOU PAID TO TAPP UNDER THE COMMERCIAL
AGREEMENT IN THE TWELVE (12) MONTHS PRECEDING THE CLAIM, AND (B) ONE HUNDRED US
DOLLARS (USD 100). NOTHING IN THIS SECTION LIMITS LIABILITY THAT CANNOT BE LIMITED
UNDER APPLICABLE LAW.

## 10. GENERAL

   10.1 **Precedence.** If you have a Commercial Agreement with Tapp, that
        agreement controls to the extent it conflicts with this file.

   10.2 **Governing law.** This licence is governed by the laws of Israel, without
        regard to its conflict-of-laws rules, and the courts of Tel Aviv have
        exclusive jurisdiction.

   10.3 **Entire agreement; severability.** This file is the entire agreement
        concerning the Software absent a Commercial Agreement. If any provision is
        held unenforceable, the rest remains in effect.

   10.4 **Assignment.** You may not assign this licence without Tapp's prior
        written consent. Tapp may assign it in connection with a merger,
        acquisition, or sale of assets.

Questions about licensing: contact@tappgo.com

<!--
Adapted from Tapp-iOS `dist/LICENSE`, and **not reviewed by counsel** — Tapp chose to
publish on it rather than wait. Recorded here rather than left to memory, because a
published version is immutable: these are that version's terms permanently.

If it is reviewed later, review the DELTA rather than the whole document — everything
not listed here is that file, verbatim:

  §1   Rewritten. The iOS version says "Source code is not distributed"; for a UPM
       package that is false — Unity compiles our C# in the customer's project, so
       the wrapper source is necessarily delivered readable. §1 now separates
       delivered source (a) from the binary (b), and §3(a)/(c) and §4 carry that
       through. This is the substantive difference between the two licences.
  §2.1 Rewritten, and this is the commercial decision, not a drafting one. The iOS
  §2.2 text gates *SDK use* on a Commercial Agreement and caps it at 30 days
       otherwise. That fits a binary handed to contracted customers; it does not fit
       a free SDK on a public registry, where it would make every installer a
       violator on day 31 and contradict calling the SDK free. Tapp's paywall is the
       **service** — the SDK is inert without credentials — so the grant is now free
       and perpetual, and §2.2 puts the commercial gate solely on the service, where
       §2.4 already pointed. Every protection that matters is in §3 and §2.4 and is
       untouched: no redistribution, no derivative works, no competing product, no
       reverse engineering, and no right to the service.
  §7   Follows from that: the licence no longer dies with the Commercial Agreement,
       because it no longer depends on one.
       Also dropped "revocable" from the §2.1 grant, deliberately and worth a second
       opinion: a licence that is free, perpetual *and* revocable at will is not
       meaningfully any of the three, and §7 already terminates it on breach. If Tapp
       wants a unilateral kill switch on the SDK itself — as opposed to on the service,
       which it controls outright — put it back explicitly rather than leaving one word
       to carry it.
  §2.3 New. Samples are meant to be copied into a customer's game; without a
       carve-out §3(c) forbids exactly the intended use.
  §3(b) Narrowed to the binary — there is nothing to reverse engineer in source.
  §3(c) Extended to cover reuse of the wrapper source outside a Tapp integration,
       which is what replaces the protection the iOS binary got from being compiled.
  §5   Points at documentation.tappgo.com; the iOS text names docs/ANALYTICS-EVENTS.md,
       which this package does not ship.
  §6/§8 Add Unity Editor version and scripting backend.
  §1   References `Third Party Notices.md`, which ships in this package.

`package.json` declares "SEE LICENSE IN LICENSE.md" and deliberately carries **no**
`licensesUrl`. This file is the only statement of terms, which is the point: Package
Manager renders `licensesUrl` as a link in the package details pane, and tappgo.com/legal/sdk
does not exist — a dead link there is worse than none, since without the field Package
Manager falls back to this file, which always ships.

It also removes a hazard. Two copies of the same terms, one in a tarball and one on a
website, have to be kept in agreement forever. And terms attached to an **immutable**
published version want to be version-pinned: this file states what com.tapp.go@X.Y.Z was
published under, permanently, where a web page can only ever show *now* — so a customer
still on an older version would read terms never offered to them.

If a canonical web copy is wanted later, `licensesUrl` is additive metadata and can come
back. It should not come back pointing at a page that 404s.
-->
