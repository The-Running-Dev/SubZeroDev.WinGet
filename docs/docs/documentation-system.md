---
id: documentation-system
title: Documentation System
sidebar_position: 8
---

# Documentation System

Installed from the published container image — no Node install, no template checkout to keep in
sync.

:::note Not yet adopted by this repository
This page documents the containerised `docs-template` system. This repository has **not** migrated
to it yet — it currently builds its docs site from `website/` via `.github/workflows/docs.yml`, and
the site serves from the root rather than `/docs`. Treat this as the reference for the target
system, not a description of the current build.
:::

## Install

```bash
docker run --rm -v "$PWD:/work" -w /work --user "$(id -u):$(id -g)" \
  ghcr.io/the-running-dev/docs-template:latest \
  Invoke-SetupDocs -ProjectDir /work -Title 'My Project' -SiteUrl 'https://docs.example.com/'
```

- **Mount the whole project, including `.git`.** The gate finds the project root by walking up for
  a `.git` marker and fails without it.
- **`--user "$(id -u):$(id -g)"`** matters on Linux hosts; without it the container writes
  root-owned files into the repository.
- **`-ProjectDir` must point at the mount.** It defaults to `.`, and the image's own working
  directory is `/template`. Omit both and the command refuses to run rather than installing into
  the image itself.

Re-run with `-Overwrite` to pick up upstream fixes. `-BaseImage` pins a specific tag instead of
tracking `:latest`.

| Command | Purpose |
|---|---|
| `Invoke-SetupDocs` | Install or update the whole system. |
| `Invoke-SetupDocsWorkflow` | Install only the two workflows. |
| `Invoke-DocsBuild` | Build the static site, the same way CI does. |

`Invoke-DocsBuildImage` and `Invoke-PreviewDocs` drive Docker themselves, so they only run on a
host — not inside the image.

## Deploying

Two things must be set up once, by hand:

**1. Enable GitHub Pages.** *Settings* → *Pages* → *Source*: **GitHub Actions**. Without this the
deploy job fails at `configure-pages`.

**2. Make the checks required** on the default branch, or a red run reports but does not block a
merge:

```text
Documentation links and terminology
Verify Documentation Build
Build and Deploy Documentation
```

No registry credentials are needed — `ghcr.io/the-running-dev/docs-template` is a public package,
so the `github.token` the workflows already fall back to is enough. `REGISTRY_TOKEN` is only
required if `-BaseImage` points at a private fork or mirror.

Then it is automatic:

- **Pull request** → gate runs, site builds, Pages artifact archived. Nothing is published.
- **Push to `main`** → `docs-deploy.yml` builds and deploys to Pages.

The published URL is `url` + `baseUrl` in `docs/docusaurus.config.ts`, set by `-SiteUrl` at install
time.

To reproduce the CI build without pushing:

```bash
docker run --rm -v "$PWD:/work" -w /work --user "$(id -u):$(id -g)" \
  ghcr.io/the-running-dev/docs-template:latest \
  Invoke-DocsBuild -SourceDocs /work/docs -OutputPath /work/artifacts/docs
```

## Local preview

```bash
./docs.ps1              # build and serve
./docs.ps1 -Live        # bind-mount docs/ for hot reload
./docs.ps1 -BuildOnly   # build only; regenerates the homepage
```

## The homepage is generated

`docs/docs/index.md` comes from `README.md`. Edit the README, run `./docs.ps1 -BuildOnly`, and
commit both together — the gate fails if the committed copy differs.

The generator rewrites the site origin to `/` but **not** relative links, so `docs/guide.md` in the
README becomes `docs/docs/docs/guide.md` and fails the gate. Prefer absolute links to the published
site.

## The gate

```bash
./build/Test-Documentation.ps1
```

Errors (broken relative links, bad heading anchors, generated-file drift) fail the run. **Warnings
(terminology) do not block CI** — `docs-ci.yml` runs the gate without `-TreatWarningsAsErrors`. Add
that switch if they should.

## Serving path

Docs serve under `/docs` (`routeBasePath: 'docs'`), so `docs/docs/index.md` is the section landing
page, not the site root. Setting `routeBasePath: '/'` makes the generated homepage the root, at the
cost of moving every URL.
