# graphify-out

Generated knowledge graph for this repository, produced by
[graphify](https://github.com/safishamsi/graphify). Tracked in git so it travels between dev
machines instead of being rebuilt per clone.

Regenerate with `/graphify .`; refresh incrementally with `/graphify . --update`.

## What is tracked, and why

| Path | Why it's tracked |
|---|---|
| `graph.json` | The graph itself — 801 nodes, 1,806 edges, 52 communities |
| `graph.html` | Interactive viewer (see the caveat below) |
| `GRAPH_REPORT.md` | Audit report: god nodes, community hubs, surprising connections |
| `.graphify_labels.json` | Hand-written community labels — not reproducible by re-running |
| `manifest.json` | Repo-relative per-file `ast_hash` / `semantic_hash`; drives `--update` |
| `cache/semantic/` | **The expensive one.** ~171k input tokens of LLM extraction |
| `cost.json` | Cumulative token spend across runs |

## What is deliberately *not* tracked

Everything below is either keyed by, or embeds, an absolute machine-local path, so it is dead
weight on any other checkout — and all of it is cheap to rebuild. See `.gitignore`.

| Path | Why not |
|---|---|
| `.graphify_python` | Resolved interpreter path. Tracking it actively **breaks** graphify on a machine with different paths: the interpreter guard only re-resolves when the file is *missing*, not when it is stale |
| `.graphify_root` | Absolute scan root; re-derived on first use |
| `cache/stat-index.json` | Keyed by absolute path (60/60 entries), and stores mtimes — which a fresh clone resets anyway, so it can never hit on another machine |
| `cache/ast/` | Blobs embed the absolute source path. The AST pass is deterministic and needs no LLM, so re-extraction is seconds and costs nothing |

Dropping `cache/ast/` does not affect incremental updates: `manifest.json` carries the
`ast_hash` per file, so unchanged files are still skipped without it.

## Caveat: `graph.html` is not fully self-contained

It loads `vis-network` from a public CDN at runtime:

```
https://unpkg.com/vis-network@9.1.6/standalone/umd/vis-network.min.js
```

So it needs no local server, but it **does** need internet access, and will render blank
behind a proxy that blocks unpkg.com. If that becomes a problem, vendor
`vis-network.min.js` next to the HTML and repoint the `<script src>` at the local copy, or
just open `graph.json` directly.
