using System.Runtime.CompilerServices;

// Nothing in Editor/ is public — it is build tooling, not API a host calls (see CLAUDE.md). Which leaves the
// tests unable to reach any of it without this, and the build hook is the part of this package most worth
// testing: it runs once, in a headless build, and its failures show up as an Xcode error or a blank Lock
// Screen card rather than as a stack trace.
[assembly: InternalsVisibleTo("TappGo.Editor.Tests")]
