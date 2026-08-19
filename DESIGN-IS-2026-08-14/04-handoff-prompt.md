```text
/make-plan Redesign the SubZeroDev.WinGet documentation homepage and primary Getting Started path. Current design failed audit at 12/30 with critical gaps in principles #2 useful, #4 understandable, #6 honest, and #10 as little design as possible.

Verdict paragraph (quoted from 03-verdict.md):
> REDESIGN — At 12/30, the current documentation has strong technical material and accessibility foundations, but its duplicated information architecture, broken first-install sequence, label/behavior mismatches, incomplete states, and excessive runtime weight prevent the primary adoption task from being direct and trustworthy.

Why redesign and not refine: The total is below 20 and the task sequence, page purpose, and navigation hierarchy must change together; isolated styling or copy edits cannot make the onboarding path direct.

Preserve from current design:
- The SubZeroDev.WinGet name and concise product boundary: a C# client over the Windows Package Manager COM API with plain .NET types and only documented CLI-backed exceptions (`docs/docusaurus.config.ts:13,39`; `docs/docs/index.md:10-25`).
- The explicit compatibility, package-channel, architecture, validation, and project-reference disclosures (`docs/docs/getting-started.md:9-50`).
- The working Docusaurus accessibility foundations: semantic heading hierarchy from `docs/docs/getting-started.md:7-109`, keyboard-reachable native controls, skip link, visible focus, dark mode, and reduced-motion token support.

Discard:
- The README-derived omnibus homepage that mixes adopter onboarding, maintainer build/release content, documentation-system internals, specification, roadmap, and license. Evidence: `docs/docs/index.md:27-160`. Caused failure on principles #2 and #10.
- The duplicated destination table and repeated onboarding content across homepage and Getting Started. Evidence: `docs/docs/index.md:133-148`, `docs/docs/index.md:27-105`, `docs/docs/getting-started.md:9-92`. Caused failure on principles #4 and #10.
- The install-first sequence that presents `dotnet add package` before the currently required GitHub Packages source and authentication. Evidence: `docs/docs/getting-started.md:15-32`. Caused failure on principles #2 and #6.

Top 3-5 moves from the audit (verbatim):
1. **Principle #2 — Useful:** Rebuild the onboarding path around the package channel that actually works: prerequisites, GitHub Packages source/authentication, package install, architecture choice, DI registration, then one verified read-only call. Evidence: [E2](01-evidence.md#e2---primary-task-and-onboarding-order).
2. **Principle #10 — As little design as possible:** Replace the README-derived omnibus homepage with a focused product proposition, one primary `Get started` action, a compact capability summary, and direct routes for evaluators versus maintainers; remove the duplicate documentation table and repeated onboarding material. Evidence: [E3](01-evidence.md#e3---structural-counts-and-duplication).
3. **Principles #4 and #6 — Understandable and honest:** Make every label map to its destination and behavior, replace generic `Read` labels, explain first-use terms, and narrow absolute claims to verifiable language. Evidence: [E5](01-evidence.md#e5---copy-clarity-and-behavior-mismatches).
4. **Principle #8 — Thorough:** Remove the broken comments integration from this path, define empty/loading/error/success/focus/disabled behavior, announce copy success, retain the skip link and focus visibility, and repair the failing code-token contrast. Evidence: [E4](01-evidence.md#e4---visual-system), [E7](01-evidence.md#e7---states-and-accessibility).
5. **Principle #9 — Environmentally friendly:** Set explicit budgets below 500 KB initial JS and 20 primary-view requests, remove irrelevant portfolio/auth/comments work, and preserve zero idle animation plus dark/reduced-motion support. Evidence: [E8](01-evidence.md#e8---weight-and-friction).

Redesign principles in priority order:
1. Principle #2 — Useful — A first-time .NET developer reaches one successful read-only API call by following a single linear sequence whose first install instruction works with the currently published package channel.
2. Principle #10 — As little design as possible — The homepage has one primary action and no duplicate route table or repeated onboarding; evaluator and maintainer material are clearly separated.
3. Principles #4 and #6 — Understandable and honest — Every label predicts its behavior and destination, technical terms are explained at first use, and every claim is bounded by evidence and known limitations.

Constraints:
- Keep Docusaurus and the existing containerized docs build unless evidence in the plan proves a migration is necessary.
- Preserve the product name, core technical boundaries, and all compatibility/validation disclosures.
- Target WCAG 2.2 AA, less than 500 KB initial compressed JavaScript, fewer than 20 primary-view requests, zero idle animation, working dark mode, and respected reduced motion.
- Treat `docs/docs/index.md` as generated from the README: the plan must identify the authoritative source and generator changes needed to avoid drift.

Deliverables for the plan:
- New information architecture not derived from the old omnibus page
- New primary flow, low-fi and labeled, compared side-by-side to current
- Concrete source-of-truth strategy for README versus docs homepage
- Target files and migration steps for the Docusaurus/container setup
- States checklist: empty, loading, error, success, focus, disabled
- Accessibility checks for semantics, keyboard order, focus, announcements, and contrast
- Performance-budget measurement method and acceptance thresholds
- Migration path for users currently following the old install instructions
- Cutover criteria: the first-time install path works from a clean supported machine, all labels resolve correctly, the Giscus error is absent, WCAG checks pass, and performance budgets pass

Anti-patterns to guard against (specific to REDESIGN):
- Porting the old structure under new styling
- Keeping both designs behind a flag indefinitely
- Redesigning to follow a trend rather than the principles above
- Treating the Preserve list as optional — it must be filled before this handoff is valid
```
