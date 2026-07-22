# graphify-out

Generated knowledge graph for this repository, produced by
[graphify](https://github.com/safishamsi/graphify). Tracked in git so it travels between dev
machines instead of being rebuilt per clone.

Regenerate with `/graphify .`; refresh incrementally with `/graphify . --update`.

## What is tracked, and why

| Path | Why it's tracked |
|---|---|
| `GRAPH_REPORT.md` | Audit report: god nodes, community hubs, surprising connections |
| `.graphify_labels.json` | Hand-written community labels — not reproducible by re-running |
| `manifest.json` | Repo-relative per-file `ast_hash` / `semantic_hash`; drives `--update` |
| `cache/semantic/` | **The expensive one.** ~171k input tokens of LLM extraction |

## What is deliberately *not* tracked

Everything below is either keyed by, or embeds, an absolute machine-local path, so it is dead
weight on any other checkout — and all of it is cheap to rebuild. See `.gitignore`.

| Path | Why not |
|---|---|
| `.graphify_python` | Resolved interpreter path. Tracking it actively **breaks** graphify on a machine with different paths: the interpreter guard only re-resolves when the file is *missing*, not when it is stale |
| `.graphify_root` | Absolute scan root; re-derived on first use |
| `cache/stat-index.json` | Keyed by absolute path (60/60 entries), and stores mtimes — which a fresh clone resets anyway, so it can never hit on another machine |
| `cache/ast/` | Blobs embed the absolute source path. The AST pass is deterministic and needs no LLM, so re-extraction is seconds and costs nothing |
| `graph.json`, `graph.html` | Derived. ~2.2 MB rewritten wholesale on every build, and both regenerate from the tracked semantic cache in seconds |
| `cost.json` | Per-machine token tally; merges into noise across two machines |

Dropping `cache/ast/` does not affect incremental updates: `manifest.json` carries the
`ast_hash` per file, so unchanged files are still skipped without it.

## On a fresh checkout

`graph.json` and `graph.html` are not tracked, so run `/graphify .` once to build them. That
run spends ~10s on AST extraction, hits the tracked semantic cache for every file, and costs
no tokens.

Run it before `/graphify query` too: `.graphify_python` is untracked and only written by a
full run, and the skill's guard re-resolves it only when the file is *missing*, never when
it is stale.

Note that the generated `graph.html` loads `vis-network` from
`https://unpkg.com/vis-network@9.1.6/...`, so the viewer needs internet access and renders
blank behind a proxy that blocks unpkg.com. Vendor the script locally if that matters.
