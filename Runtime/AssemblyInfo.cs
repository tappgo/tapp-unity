using System.Runtime.CompilerServices;

// The Editor and test assemblies are separate compilation units, so `internal` doesn't reach them without
// this. That's the point of keeping them separate — the Editor assembly never ships to players, and the test
// assembly is compiled out of a release entirely — but both legitimately need the internals: the settings
// panel validates through `TappGoSettings.Validate()`, and the tests drive `JsonBuilder`, `BridgeReply` and
// the main-thread pump directly.
//
// Nothing here widens the *public* surface. Adding a name to this list is not an API decision; adding a
// `public` member is.
[assembly: InternalsVisibleTo("TappGo.Editor")]
[assembly: InternalsVisibleTo("TappGo.Tests")]
[assembly: InternalsVisibleTo("TappGo.Editor.Tests")]
