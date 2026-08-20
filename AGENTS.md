@CLAUDE.md

Everything in `CLAUDE.md` applies. It is the single agent contract for this repo — the direction
is inverted from the kit's own arrangement because `CLAUDE.md` already held this repository's
build/architecture guidance before the kit was installed, and that is the smaller change to keep.
This file exists only so tools that read `AGENTS.md` by convention find the contract.

If `@CLAUDE.md` import is not resolving in your version, replace this file with a hardlink:

```powershell
# from repo root, PowerShell as admin not required for hardlinks on same volume
New-Item -ItemType HardLink -Path AGENTS.md -Target CLAUDE.md -Force
```
