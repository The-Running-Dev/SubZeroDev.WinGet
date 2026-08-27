# Preliminary proposal — self-hosted WinGet repository

This directory is exploratory design context for a self-hosted WinGet package source.
It is not part of the v0.2 evidence-bound support-claims design in `design/`, does not
amend that design, and creates no public `SubZeroDev.WinGet` API commitment.

The proposal exists here because this library already owns the client-side WinGet source
integration context. The proposed repository service remains an independently deployed
runtime, not a new responsibility of the existing C# client library.

Read `context.md` for established constraints and `handoff-to-opus.md` before turning
this proposal into a maintainer-approved brief or a binding design.
