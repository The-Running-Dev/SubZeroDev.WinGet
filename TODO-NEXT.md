# TODO-NEXT — follow-up work from PR #11

Findings from the Qodo review of [PR #11](https://github.com/The-Running-Dev/SubZeroDev.WinGet/pull/11)
("Harden package consumers and WinGet COM threading"), plus an independent pass over the merged
code.

**PR #11 is already merged** (`7f3c9c0`) and its branch is deleted, so everything here needs a new
branch and PR — nothing can be amended in place. Qodo's review landed ~3 minutes *after* the merge
completed, which is why none of it gated the merge.

Each item below records a verdict, because **not all three review findings are valid**: one is a
real bug, one is a genuine inconsistency in our own written convention, and one is a false positive
that must not be "fixed" or it will break ARM64 support.

---

## 1. ~~Dispose the COM context's owned synchronization primitives~~ — ✅ DONE

**Verdict: real bug. Confirmed — and fixed.** Qodo finding #3 (`comment_id 3659760726`).

`Dispose()` now releases all four owned primitives after the owner thread is provably dead,
serialized behind a dedicated `_disposeGate` so concurrent disposers can't wait on or join against
primitives another disposer already released. `_queue` is released under `_gate`, since `TryPost`
adds to it while holding that lock. Regression tests added for idempotency, concurrent disposal,
repeated context lifetimes, and the post-disposal contract.

**Fixing it surfaced a second hazard, also fixed:** `Enqueue` and `RegisterCancellation` both read
`_shutdown.Token` *before* taking `_gate`. Once `_shutdown` is disposed that read throws
synchronously, which would have broken the faulted-task contract the `_gate` check already
establishes for work submitted after disposal. Both now bail early on `_disposing` —
`Enqueue` returns a faulted task, `RegisterCancellation` returns a no-op registration.

**Known remaining limitation (deliberate, documented in code):** the self-disposal path — `Dispose()`
called *from* the owner thread, e.g. inside a COM callback — still leaves the primitives to
finalization. It cannot join itself, so the pump is still running on that very thread and the
primitives are not safe to release. Every other path (DI provider disposal, a directly constructed
client, the constructor's startup-failure cleanup) reaches the join and cleans up. Revisit only if
self-disposal ever becomes a common pattern; today it's an edge case.

<details>
<summary>Original analysis (kept for context)</summary>

`WinGetComContext` owns four `IDisposable` fields and disposes none of them:

| Field | Line | Type |
|---|---|---|
| `_queue` | 13 | `BlockingCollection<Action>` |
| `_started` | 15 | `ManualResetEventSlim` |
| `_stopped` | 16 | `ManualResetEventSlim` |
| `_shutdown` | 17 | `CancellationTokenSource` |

`Dispose()` (line 225) stops and joins the owner thread but never touches them. Note the class
*does* correctly dispose its per-work-item linked sources and registrations (lines 109–110,
148–149, 255–256) — it's only the context's own fields that leak.

**Impact is bounded but real.** For the DI singleton it's one context per process, reclaimed at
exit. It matters for code that constructs `WinGetClient`/`WinGetSourceClient` directly in a loop:
each instance owns a context, and each leaks a `BlockingCollection` (which holds `SemaphoreSlim`s),
two `ManualResetEventSlim`s (each of which may have allocated a kernel event handle), and a
`CancellationTokenSource` with linked registrations.

### The trap that makes this non-trivial

Don't just append disposal to the end of `Dispose()`. There are two paths through it:

- **Normal path** (called from any other thread): waits `_stopped`, joins the thread. After the
  join the pump is provably dead, so disposal is safe here.
- **Self-disposal path** (called *from* the owner thread, e.g. inside a COM callback): deliberately
  skips the wait and join — see the comment at line 237 — and returns while the pump is still
  draining. Disposing the primitives here would pull them out from under the running pump.

There's also a third-party race: a second thread sitting in `_stopped.Wait()` while the first
thread joins and disposes `_stopped` gets an `ObjectDisposedException`.

**Requirements for the fix:**
- Dispose only after the owner thread is provably stopped.
- Make `Dispose()` idempotent — today repeat calls are harmless because `_stopped.Wait()`/`Join()`
  on a dead thread are no-ops, but that stops being true once the primitives are disposed.
- Keep the self-disposal path working (it must still not join itself).
- Add a regression test: construct and dispose a context repeatedly and assert no
  `ObjectDisposedException`, plus a test for disposal from within a queued callback.

</details>

---

## 2. Decide the scope of the "no `Async` suffix" convention — `S`

**Verdict: valid inconsistency, but not in the code — in our written rule.** Qodo finding #1
(`comment_id 3659760711`), flagging `WinGetComContext.InvokeAsync`.

`CLAUDE.md` and `SPECIFICATION.md` §2 state the no-`Async`-suffix convention **unscoped**, which
reads as applying to every method. But the codebase has never worked that way: `WinGetClient`
carried a private `FindPackagesAsync` helper *before* PR #11, on `main`, and it survives today. The
convention has in practice always governed the **public API surface**, where the suffix would be
consumer-visible noise.

So `InvokeAsync` — an `internal` method on an `internal` class — is consistent with existing
practice and inconsistent with the letter of the rule. Pick one:

- **(a) Scope the rule to the public surface** (recommended). One sentence in `CLAUDE.md` and
  `SPECIFICATION.md` §2: the convention governs public API; private/internal helpers wrapping
  genuinely async plumbing may keep the suffix for clarity. Zero code churn, and it matches what
  we already do.
- **(b) Apply it strictly.** Rename `InvokeAsync` → `Invoke` *and* `FindPackagesAsync` →
  `FindPackages` for consistency. Cheap (both internal/private), but `Invoke` returning `Task<T>`
  arguably reads worse than the thing it replaced.

Either is defensible; the thing not to do is leave the rule saying one thing while the code does
another, because that's what generated this finding and will regenerate it on every future review.

---

## 3. Do NOT revert the library to `PlatformTarget=x64` — update the Qodo rule instead — `S`

**Verdict: false positive. Do not action as written.** Qodo finding #2 (`comment_id 3659760716`).

Qodo flags `<PlatformTarget>AnyCPU</PlatformTarget>` in `SubZeroDev.WinGet.csproj` as violating a
configured compliance rule requiring AnyCPU builds to be redirected to x64.

That rule was correct for the *old* design and PR #11 deliberately obsoleted it. The managed
library is now **IL-only AnyCPU on purpose**, so one managed assembly serves both x64 and ARM64
consumers, with the correct native `Microsoft.Management.Deployment.dll` selected at consumer build
time by `buildTransitive/…/SubZeroDev.WinGet.targets`. This is the documented platform contract —
`CLAUDE.md:65`: *"the managed library is IL-only AnyCPU, while executable/test hosts explicitly
select x64 or ARM64."*

Forcing the library back to x64 would defeat the entire packaging change and break ARM64 support.
The invariant is already enforced in CI by the `ArchitectureTest` target (library AnyCPU;
executables/test hosts x64 or ARM64, verified against emitted PE headers).

**Action: update the Qodo compliance rule** (one of the 25 configured for this repo) so it exempts
the library project, or restate it as "executables and test hosts must not build AnyCPU". No code
change.

---

## 4. Decide where the plan documents live — `S`

Carried over from my review of PR #11; not a defect, a preference call that was left open.

`HIGH-VALUE-IMPLEMENTATION-PLAN.md` (10.7 KB) and `PACKAGING-TARGETS-PLAN.md` (16.8 KB) sit at the
repo root. They're cross-linked and referenced from `ROADMAP.md`, so they aren't orphaned, but they
overlap ROADMAP's purpose and are largely process artifacts of a change that has now shipped. With
this file added there are four overlapping planning documents at root.

Options: fold the still-relevant parts into `ROADMAP.md` and delete; move them under `docs/` (they
would then also publish to the docs site, which may not be wanted); or keep as-is and accept the
clutter. Worth deciding deliberately rather than by accretion.

---

## 5. Low-priority polish — `S`

- **Cancellation on shutdown surfaces as a faulted task, not a canceled one.**
  `WinGetComContext.Enqueue` (line ~122) filters `OperationCanceledException` on the *caller's*
  token only. When `_shutdown` fires instead, the OCE falls through to the generic handler and
  becomes `TrySetException`. In practice this is nearly always masked — `Dispose()` already invokes
  the `_outstanding` cancel callbacks first, so the `TaskCompletionSource` is usually already
  canceled and the later `TrySet*` is a no-op — but the fallback path is still inconsistent with
  the canceled-task contract the rest of the class honors.

---

## Status

| # | Item | State |
|---|---|---|
| 1 | COM context primitive disposal | ✅ done |
| 2 | Scope of the no-`Async`-suffix convention | ⬜ needs a decision |
| 3 | `PlatformTarget=AnyCPU` — update the Qodo rule, not the code | ⬜ needs a rule change (outside the repo) |
| 4 | Where the plan documents live | ⬜ needs a decision |
| 5 | Cancellation surfaces as faulted, not canceled | ⬜ open, low priority |

Items 2, 3 and 4 are all decisions rather than code, and 3 is partly outside the repo entirely
(Qodo's configured rules). Item 5 is the only remaining code change, and it's cosmetic — the path
is almost always masked in practice.

## Not in scope here

`ROADMAP.md` still tracks **27 open items** across correctness, packaging hygiene, CI, API, and new
capability (7 now closed). This file covers only the fallout from PR #11; the roadmap remains the
backlog of record.
