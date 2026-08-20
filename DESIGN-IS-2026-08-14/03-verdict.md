# Verdict: REDESIGN

REDESIGN — At 12/30, the current documentation has strong technical material and accessibility foundations, but its duplicated information architecture, broken first-install sequence, label/behavior mismatches, incomplete states, and excessive runtime weight prevent the primary adoption task from being direct and trustworthy.

## Highest-leverage moves

1. **Principle #2 — Useful:** Rebuild the onboarding path around the package channel that actually works: prerequisites, GitHub Packages source/authentication, package install, architecture choice, DI registration, then one verified read-only call. Evidence: [E2](01-evidence.md#e2---primary-task-and-onboarding-order).
2. **Principle #10 — As little design as possible:** Replace the README-derived omnibus homepage with a focused product proposition, one primary `Get started` action, a compact capability summary, and direct routes for evaluators versus maintainers; remove the duplicate documentation table and repeated onboarding material. Evidence: [E3](01-evidence.md#e3---structural-counts-and-duplication).
3. **Principles #4 and #6 — Understandable and honest:** Make every label map to its destination and behavior, replace generic `Read` labels, explain first-use terms, and narrow absolute claims to verifiable language. Evidence: [E5](01-evidence.md#e5---copy-clarity-and-behavior-mismatches).
4. **Principle #8 — Thorough:** Remove the broken comments integration from this path, define empty/loading/error/success/focus/disabled behavior, announce copy success, retain the skip link and focus visibility, and repair the failing code-token contrast. Evidence: [E4](01-evidence.md#e4---visual-system), [E7](01-evidence.md#e7---states-and-accessibility).
5. **Principle #9 — Environmentally friendly:** Set explicit budgets below 500 KB initial JS and 20 primary-view requests, remove irrelevant portfolio/auth/comments work, and preserve zero idle animation plus dark/reduced-motion support. Evidence: [E8](01-evidence.md#e8---weight-and-friction).
