# Observation plan — FC24 and FC25

This is a plan, not evidence and not a design document. It exists because
[`20-contract.md`](20-contract.md) rows FC24 and FC25 and
[`10-design.md`](10-design.md) § *Open questions* state that slicing does not begin until both are
**observed**, and neither is answerable by reading anything. Nothing here amends the contract, the
design, or the brief; it names the smallest concrete steps that would produce an observation, so that
executing it is the only work left. Once an observation is made, it is recorded per *Recording an
observation* below and reported to the maintainer — a negative result is adjudicated by the
maintainer, per the escalation each contract row already states, and is not resolved by whoever runs
this plan.

## Why a minimal harness, not a full deployment

Neither question depends on the manifest tree, the artifact store, the offsite backup, or the pinned
runtime's own behaviour beyond the one endpoint every WinGet REST client calls first. Building the
full `rewinged` deployment before asking these two questions would spend the FC2–FC23 work before
knowing whether the brief's own requirements (disposable client, Entra guard) are jointly satisfiable
at all — which is the tension FC24 names and the reason `10-design.md` says slicing should wait.

What both questions need is:

1. An Entra app registration the maintainer's tenant owns, exposing a resource (an Application ID URI
   / custom scope) — this is FC25's subject.
2. A WinGet REST source's `/information` endpoint advertising that resource with `AuthenticationInfo`
   type `microsoftEntraId` — this is the one HTTP response the client reads before it ever attempts a
   token acquisition, and it can be served by a static file or a two-line handler. It does not need
   TLS from a real certificate, a manifest tree, or a running `rewinged` — a source registered against
   `http://` or a self-signed endpoint on a LAN address is sufficient to exercise the authenticator,
   though `winget.exe` may require HTTPS to register the source at all, in which case a self-signed
   certificate trusted locally is enough; this is a throwaway harness, not the production endpoint.
3. A client machine (FC25) and, separately, a Windows Sandbox instance (FC24) that can reach that
   endpoint.

This harness does **not** produce evidence for FC6, FC13, FC19, FC20, or any other row that depends on
the served manifest set, the `no-store` header, or the production certificate. It exists only to
answer the two Unresolved questions.

## Shared prerequisites

- **The exact client id literal.** `10-design.md`'s citation names
  `src/AppInstallerCommonCore/Authentication/WebAccountManagerAuthenticator.cpp:20,21` in the
  `winget-cli` working tree at `5c88b96f` as the source of the fixed, Microsoft-owned client id, under
  authority `organizations`. Read it from that file at that commit rather than from a value typed
  here or recalled from memory — the citation is the source of truth, and this plan does not restate
  a GUID it has not verified against the tree.
- **An Entra tenant the maintainer administers**, matching the brief's single-maintainer-tenant
  premise.
- **An app registration in that tenant** exposing at least one custom scope, so it has an Application
  ID URI to use as the advertised resource. No redirect URI, client secret, or certificate is needed
  on this registration — the client authenticating against it is the fixed Microsoft-owned client id,
  not an app the maintainer registers as a confidential client.
- **A reachable HTTP(S) endpoint** the maintainer controls, able to serve a static or near-static
  `/information` response. A laptop on the same network, a temporary cloud VM, or a small local
  process forwarded through a tunnel all satisfy this — nothing here requires the production host.

## FC25 — will the tenant admit the fixed client id for a custom resource?

**Setup**

1. In the app registration from *Shared prerequisites*, note the Application ID URI (the resource
   identifier WinGet will request a token for).
2. Serve `/information` advertising that resource under `AuthenticationInfo.MicrosoftEntraIdAuthenticationInfo.Resource`,
   matching the shape the WinGet REST source API defines. No manifest tree or search/show endpoints
   need to work yet — FC25 is answered by the authentication step alone, before any package query.
3. Register the endpoint as a WinGet source on a machine that is signed in to the maintainer's tenant.

**Procedure**

4. From an **interactive** session with no cached WAM account for the fixed client id, run a client
   operation against the registered source (`winget search <anything> --source <name>` is sufficient —
   the search need not return results, only trigger the authentication attempt).
5. Observe what happens at the consent step:
   - The interactive WAM window appears, the maintainer signs in and consents, and the client proceeds
     to send an authenticated request. This is the **positive** outcome.
   - The tenant or Entra refuses to issue a token for that resource to that client id — an
     admin-consent requirement the tenant will not grant, a conditional-access policy blocking it, or
     an outright refusal. This is the **negative** outcome, and FC25 says explicitly what it does and
     does not license: it returns the 2026-08-29 Entra ID decision to the maintainer, and does not
     license anonymous public read, a custom-header shared secret, or any other substitute.
6. If consent succeeds only after a specific tenant configuration step (an admin-consent grant, a
   specific API permission shape, a conditional-access exclusion), record exactly what that step was —
   it is what a later slice's deployment configuration must reproduce.

## FC24 — does WAM interactive authentication complete inside the disposable client?

This question presupposes FC25's positive outcome for the same resource, so run it second, against the
same registered source once FC25 is satisfied — there is no separate resource needed.

**Setup**

1. Windows Sandbox available on a Windows 11 Pro/Enterprise/Education machine, virtualization enabled.
2. The same registered source from FC25, reachable from inside the sandbox (a sandbox has network
   access by default; confirm the endpoint is reachable from a fresh sandbox instance, not just the
   host).

**Procedure**

3. Start a fresh Windows Sandbox instance. It is not Entra-joined and holds no WAM state — this is the
   environment the question is about, not an approximation of it.
4. Inside the sandbox, register the same source and attempt the same client operation as FC25 step 4.
5. Observe:
   - The interactive WAM window appears inside the sandbox, the maintainer signs in, and the operation
     proceeds. This is the **positive** outcome — a non-joined, state-discarding session can still
     complete interactive acquisition for this client id and resource.
   - WAM cannot present an account picker, cannot complete the flow, or the sandbox's lack of Entra
     join blocks it outright. This is the **negative** outcome, and FC24 says what it does and does
     not license: it does not license a weaker disposable client, a substituted validation, or a quiet
     redefinition of "disposable" — it returns to the maintainer as a tension between the brief's
     disposable-client requirement and its authentication requirement, one of which gives.
6. Repeat once more from a second fresh sandbox instance before treating either outcome as settled —
   a single run does not distinguish a transient failure (network timing, a Windows Sandbox update) from
   the structural answer the question asks for.

## Recording an observation

Neither observation is a Gate in `design/10-design.md` § *Gate*'s sense — nothing here is invoked by a
workflow, and none of it becomes a CI status. It is still Evidence in that document's sense: it records
that a check (the procedure above) ran, in an Environment (FC15's **client** environment: Entra-joined
or not, WAM account cached for the fixed client id or not, interactive, source registered `Explicit`
or not — Sandbox is additionally *not* Entra-joined by construction, which is the fact under test), for
an exact commit (this plan's commit, since no manifest or deployment commit exists yet).

Write the result as a dated file, one per question, under `design/self-hosted-source/observations/`
(create the directory with the first record), named `YYYY-MM-DD-fc24.md` or `YYYY-MM-DD-fc25.md`,
carrying:

- **Outcome** — positive or negative, stated as plainly as FC24/FC25's own text.
- **Run** — date, who ran it, which machine/sandbox instance.
- **Environment facts** — the client-environment fields above, as actually observed, not assumed.
- **Assertions** — exactly what was exercised (e.g. "interactive token acquisition for the fixed
  client id against resource `<App ID URI>`, from a freshly started Windows Sandbox instance").
- **Non-assertions** — everything this harness does not prove (see *Why a minimal harness*).
- For FC25 specifically, any tenant configuration step step 6 found necessary.

Then report the outcome to the maintainer. A positive answer to a question changes `30-slices.md`'s
shape once it exists; a negative answer to either does not — it stops, per the 2026-09-07 decision
already adjudicating this reading, and is not narrowed back into a slicing detail by the session that
observed it.
