# Third Party Notices

`com.tapp.go` contains no third-party code. It has no UPM dependencies, and no external JSON, networking or
utility library — which is why the package builds its request payloads by hand rather than taking a
dependency the host would have to resolve, or that could conflict with one they already use.

**Update this file in the same PR** as any new dependency, and whenever `TappGo.xcframework` starts carrying
third-party code. Enterprise procurement reads it, and an out-of-date notices file blocks adoption more
reliably than a missing feature does.
