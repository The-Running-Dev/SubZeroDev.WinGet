# Graph Report - .  (2026-07-22)

## Corpus Check
- Corpus is ~24,609 words - fits in a single context window. You may not need a graph.

## Summary
- 801 nodes · 1806 edges · 52 communities (47 shown, 5 thin omitted)
- Extraction: 93% EXTRACTED · 7% INFERRED · 0% AMBIGUOUS · INFERRED: 134 edges (avg confidence: 0.82)
- Token cost: 171,499 input · 0 output

## Community Hubs (Navigation)
- PackageManagementService Implementation
- Assemblies and Namespaces
- CLI Shim: Pins, Export, Import
- WinRT Operation Result Mapping
- COM Factory and Activation Options
- Source Catalog Options
- WinRT Package Enums and Options
- NuGet Dependencies and Targets
- Live Integration Tests
- Catalog Search and Composites
- Error Codes and Retry Policy
- Package Examples Console
- IPackageManagementService Contract
- PackageSourceService Unit Tests
- IWinGetClient Contract
- CLI Shim Design Rationale
- Nuke CI Host Providers
- Operation Requests and Enums
- Package Details Metadata
- Nuke Build Schema Properties
- PackageSourceService Implementation
- Layered Architecture Rationale
- Model Unit Tests
- IPackageSourceService Contract
- README Scope and Integration Rule
- IWinGetSourceClient Contract
- Source Service Edge Tests
- CI Build Pipeline and SDKs
- Public Surface Conventions
- Publishing and Versioning Docs
- Nuke Build Targets
- COM Interop Findings and References
- Source Examples Console
- Example Runner Harness
- Nuke Build Schema Definitions
- Nuke Target Schema Refs
- Core Client Design Decisions
- Nuke Verbosity Levels
- DI Registration and Activation
- Availability and Hosting Errors
- Release Triggers and GitVersion
- Nuke Profile Parameters
- Package Source Models
- Nuke Host Schema Ref
- Nuke NoLogo Parameter
- Nuke Plan Parameter
- Nuke Parameters File
- build.sh Bootstrapper
- Per-Operation Progress Mappers
- Unpin Operation
- GetSource Operation

## God Nodes (most connected - your core abstractions)
1. `WinGetClient` - 48 edges
2. `PackageOperationResult` - 31 edges
3. `PackageManagementServiceTests` - 28 edges
4. `SubZeroDev.WinGet.Models` - 27 edges
5. `PackageOperationProgress` - 26 edges
6. `PackageManagementService` - 24 edges
7. `WinGetFactory` - 23 edges
8. `SubZeroDev.WinGet.Abstractions` - 20 edges
9. `PackageInfo` - 20 edges
10. `SourceOperationResult` - 20 edges

## Surprising Connections (you probably didn't know these)
- `IPackageManagementService (consumer entry point)` --implements--> `PackageManagementService (validation, logging, retry)`  [INFERRED]
  README.md → SPECIFICATION.md
- `The one integration rule: direct ComInterop PackageReference` --conceptually_related_to--> `WinGetUnavailableException`  [AMBIGUOUS]
  README.md → SPECIFICATION.md
- `SubZeroDev.WinGet (README overview)` --references--> `Nuke build orchestration (build/Build.cs targets)`  [EXTRACTED]
  README.md → SPECIFICATION.md
- `IPackageSourceService (consumer entry point)` --implements--> `PackageSourceService`  [INFERRED]
  README.md → SPECIFICATION.md
- `Release to GitHub Packages step (nuke PublishGitHubPackages)` --implements--> `GitHub Packages publishing (prerelease on main, stable on v* tag)`  [INFERRED]
  .github/workflows/build.yml → README.md

## Import Cycles
- None detected.

## Hyperedges (group relationships)
- **Layered architecture: services over clients over the COM activation factory** — specification_packagemanagementservice, specification_packagesourceservice, specification_wingetclient, specification_wingetsourceclient, specification_wingetcliclient, specification_wingetfactory, specification_winget_com_api [EXTRACTED 1.00]
- **Release flow: build gate, GitVersion-derived version, GitHub Packages vs NuGet.org** — _github_workflows_build_build_job, _github_workflows_build_release_job, _github_workflows_build_publish_github_packages_step, _github_workflows_build_publish_nuget_step, gitversion_next_version, gitversion_prerelease_stable_behaviour, _github_workflows_build_tag_trigger [EXTRACTED 1.00]
- **Live-verified COM interop defects that shaped the implementation** — specification_selectors_ord_filters_andd, specification_cswinrt_enumeration_bug, specification_extendederrorcode_is_exception, specification_internal_projection_interfaces, specification_cominterop_package [EXTRACTED 1.00]
- **Service / client / activation three-layer architecture** — docs_architecture_service_layer, docs_architecture_client_layer, docs_architecture_activation_layer, docs_architecture_ipackagemanagementservice, docs_architecture_iwingetclient, docs_architecture_wingetfactory [EXTRACTED 1.00]
- **CLI-shim COM-gap feature set (pins, export, import)** — docs_intro_cli_shim_exception, docs_architecture_iwingetcliclient, docs_usage_pins_export_import_pin, docs_usage_pins_export_import_export, docs_usage_pins_export_import_import, docs_usage_pins_export_import_clioperationresult, docs_usage_pins_export_import_winget_exe_resolution [EXTRACTED 1.00]
- **Missing direct ComInterop reference failure chain** — docs_getting_started_direct_interop_reference_rule, docs_getting_started_platform_pinning, docs_troubleshooting_regdb_e_classnotreg, docs_examples_examples_project [EXTRACTED 1.00]

## Communities (52 total, 5 thin omitted)

### Community 0 - "PackageManagementService Implementation"
Cohesion: 0.07
Nodes (22): CancellationToken, int, IProgress, IReadOnlyList, Task, PackageManagementService, Mock, PackageManagementService (+14 more)

### Community 1 - "Assemblies and Namespaces"
Cohesion: 0.05
Nodes (26): SubZeroDev.WinGet.Examples, SubZeroDev.WinGet.Models, SubZeroDev.WinGet.Com, SubZeroDev.WinGet, SubZeroDev.WinGet.Abstractions, SubZeroDev.WinGet.Tests, Exception, IServiceCollection (+18 more)

### Community 2 - "CLI Shim: Pins, Export, Import"
Cohesion: 0.10
Nodes (15): CancellationToken, IReadOnlyList, Task, IWinGetCliClient, CliOperationResult, PackagePin, PackagePinKind, Test (+7 more)

### Community 3 - "WinRT Operation Result Mapping"
Cohesion: 0.19
Nodes (13): DownloadResultStatus, IAsyncOperationWithProgress, InstallProgress, InstallResult, InstallResultStatus, RepairResultStatus, PackageOperationResult, PackageOperationStatus (+5 more)

### Community 4 - "COM Factory and Activation Options"
Cohesion: 0.09
Nodes (16): ActivationMode, CreateCompositePackageCatalogOptions, DllImport, DownloadOptions, Guid, IntPtr, IReadOnlyDictionary, object (+8 more)

### Community 5 - "Source Catalog Options"
Cohesion: 0.14
Nodes (13): AddPackageCatalogOptions, DateTimeOffset, EditPackageCatalogOptions, PackageCatalogInfo, RemovePackageCatalogOptions, PackageSource, CancellationToken, IProgress (+5 more)

### Community 6 - "WinRT Package Enums and Options"
Cohesion: 0.10
Nodes (16): CatalogPackage, Error, Options, PackageDownloadProgress, PackageInstallerType, PackageInstallMode, PackageInstallScope, PackageUninstallScope (+8 more)

### Community 7 - "NuGet Dependencies and Targets"
Cohesion: 0.08
Nodes (24): coverlet.collector (6.0.2), FluentAssertions (6.12.0), Microsoft.Extensions.DependencyInjection.Abstractions (10.0.10), Microsoft.Extensions.Logging (10.0.10), Microsoft.Extensions.Logging.Abstractions (10.0.10), Microsoft.Extensions.Logging.Console (10.0.10), Microsoft.NET.Test.Sdk (17.12.0), Moq (4.20.70) (+16 more)

### Community 8 - "Live Integration Tests"
Cohesion: 0.16
Nodes (9): SetUp, Task, Test, WinGetCliClientIntegrationTests, WinGetClientIntegrationTests, WinGetSourceClientIntegrationTests, WinGetCliClient, WinGetClient (+1 more)

### Community 9 - "Catalog Search and Composites"
Cohesion: 0.16
Nodes (10): CompositeSearchBehavior, FindPackagesResult, MatchResult, PackageCatalog, PackageCatalogReference, PackageMatchFilter, FindPackagesOptions, PackageInfo (+2 more)

### Community 10 - "Error Codes and Retry Policy"
Cohesion: 0.15
Nodes (21): ExtendedErrorCode is an Exception, not an int, winget-cli return-code documentation, WinGetErrorCodes, Mutating examples, AccessDenied adding/removing sources, 0x8A150019 command requires admin, 0x8A150056 installer prohibits elevation, Download (+13 more)

### Community 11 - "Package Examples Console"
Cohesion: 0.31
Nodes (6): Progress, string, CancellationToken, IServiceProvider, Task, PackageExamples

### Community 12 - "IPackageManagementService Contract"
Cohesion: 0.27
Nodes (5): CancellationToken, IProgress, IReadOnlyList, Task, IPackageManagementService

### Community 13 - "PackageSourceService Unit Tests"
Cohesion: 0.20
Nodes (7): Mock, PackageSourceService, SetUp, Task, Test, TestCase, PackageSourceServiceTests

### Community 14 - "IWinGetClient Contract"
Cohesion: 0.34
Nodes (6): CancellationToken, IProgress, IReadOnlyList, Task, IWinGetClient, PackageOperationProgress

### Community 15 - "CLI Shim Design Rationale"
Cohesion: 0.17
Nodes (16): Composite catalogs everywhere, IWinGetCliClient, Unreachable-source resilience, Read-only examples, Live integration tests, ARP\ ids in the installed catalog, Export exits non-zero but the file exists, GetAvailableUpgrades (+8 more)

### Community 16 - "Nuke CI Host Providers"
Cohesion: 0.12
Nodes (16): enum, AppVeyor, AzurePipelines, Bamboo, Bitbucket, Bitrise, GitHubActions, GitLab (+8 more)

### Community 17 - "Operation Requests and Enums"
Cohesion: 0.21
Nodes (11): PackageRepairMode, PackageRepairScope, PackageUninstallMode, DownloadRequest, InstallRequest, RepairRequest, UninstallRequest, PackageArchitecture (+3 more)

### Community 18 - "Package Details Metadata"
Cohesion: 0.20
Nodes (8): Documentation, Icon, PackageAgreement, PackageAgreementInfo, PackageDetails, PackageDocumentation, PackageIconInfo, List

### Community 19 - "Nuke Build Schema Properties"
Cohesion: 0.13
Nodes (15): description, type, description, type, properties, description, type, Continue (+7 more)

### Community 20 - "PackageSourceService Implementation"
Cohesion: 0.35
Nodes (6): CancellationToken, IProgress, IReadOnlyList, Task, PackageSourceService, TestCase

### Community 21 - "Layered Architecture Rationale"
Cohesion: 0.21
Nodes (14): Activation layer, Client layer, Indexed for-loops over CsWinRT collections, IPackageManagementService, IPackageSourceService, IWinGetClient, IWinGetSourceClient, Selectors are OR'd, Filters are AND'd (+6 more)

### Community 22 - "Model Unit Tests"
Cohesion: 0.24
Nodes (3): Test, TestCase, ModelTests

### Community 23 - "IPackageSourceService Contract"
Cohesion: 0.35
Nodes (6): CancellationToken, IProgress, IReadOnlyList, Task, IPackageSourceService, SourceOperationResult

### Community 24 - "README Scope and Integration Rule"
Cohesion: 0.20
Nodes (12): Release to NuGet.org step (nuke PublishNuGet, NUGET_API_KEY), The one integration rule: direct ComInterop PackageReference, SubZeroDev.WinGet.Examples runnable examples, NuGet.org publishing (manual, opt-in), Read-only-by-default example safety policy, SubZeroDev.WinGet (README overview), Microsoft.WindowsPackageManager.ComInterop 1.29.280, No console output parsing (except the CLI shim) (+4 more)

### Community 25 - "IWinGetSourceClient Contract"
Cohesion: 0.35
Nodes (5): CancellationToken, IProgress, IReadOnlyList, Task, IWinGetSourceClient

### Community 26 - "Source Service Edge Tests"
Cohesion: 0.29
Nodes (6): Mock, PackageSourceService, SetUp, Task, Test, PackageSourceServiceEdgeTests

### Community 27 - "CI Build Pipeline and SDKs"
Cohesion: 0.25
Nodes (11): CI build job (tests + coverage, required status check), Dual SDK setup (8.0.x for the product, 10.0.x for Nuke), Install Nuke step (dotnet tool update --global, NUKE_VERSION 10.1.0), CI release job (needs: build), nuke Test Coverage step (single invocation for de-duplication), net10 quarantined to build/ (Nuke.Common 10.x is net10-only), net8.0-windows10.0.26100 product target decision, build.ps1/build.sh bootstrappers required by the Nuke global tool (+3 more)

### Community 28 - "Public Surface Conventions"
Cohesion: 0.24
Nodes (10): SubZeroDev.WinGet.Examples console project, The one integration rule (direct ComInterop reference), x64 platform pinning requirement, The one deliberate CLI exception, No Async method-name suffix convention, No COM/WinRT types in the public surface, SubZeroDev.WinGet (library overview), COMException 0x80040154 (REGDB_E_CLASSNOTREG) (+2 more)

### Community 29 - "Publishing and Versioning Docs"
Cohesion: 0.24
Nodes (10): GitHub Packages NuGet feed, GitHub Actions build workflow, Coverage reporting (coverlet + ReportGenerator), GitVersion versioning, Nuke build orchestration, PublishGitHubPackages target, PublishNuGet target (manual), Running CI locally with act (+2 more)

### Community 30 - "Nuke Build Targets"
Cohesion: 0.20
Nodes (10): enum, Clean, Compile, Coverage, IntegrationTest, Pack, PublishGitHubPackages, PublishNuGet (+2 more)

### Community 31 - "COM Interop Findings and References"
Cohesion: 0.24
Nodes (10): Three-step COM activation fallback chain, Documented auto-retry policy for recoverable WinGet HRESULTs, Finding: *Result.ExtendedErrorCode is an Exception, not an int, Finding: selectors are OR'd, filters are AND'd, UniGetUI (reference codebase: COM usage and elevation workarounds), Unreachable-source recovery (probe and rebuild composite), Winget-AutoUpdate (reference codebase: enterprise operational lessons), winget-cli (reference codebase: authoritative IDL and CLSIDs) (+2 more)

### Community 32 - "Source Examples Console"
Cohesion: 0.53
Nodes (4): CancellationToken, IServiceProvider, Task, SourceExamples

### Community 33 - "Example Runner Harness"
Cohesion: 0.25
Nodes (6): Example, CancellationToken, IServiceProvider, Task, Example, ExampleRunner

### Community 34 - "Nuke Build Schema Definitions"
Cohesion: 0.22
Nodes (8): allOf, definitions, ExecutableTarget, Host, NukeBuild, type, type, $schema

### Community 35 - "Nuke Target Schema Refs"
Cohesion: 0.22
Nodes (9): $ref, Skip, Target, description, items, type, description, items (+1 more)

### Community 36 - "Core Client Design Decisions"
Cohesion: 0.25
Nodes (9): Composite catalogs everywhere (CreateCompositePackageCatalog), Finding: CsWinRT-projected IReadOnlyList foreach/LINQ throws, No Async method-name suffix convention, Public surface never leaks a COM/WinRT type, PackageManagementService (validation, logging, retry), PackageOperationProgress (unified progress record over four WinRT structs), ParsePinList runtime column-offset table parsing, Test suites: 100 mocked unit tests + 12 explicit live integration tests (+1 more)

### Community 37 - "Nuke Verbosity Levels"
Cohesion: 0.25
Nodes (8): Verbosity, description, enum, type, Minimal, Normal, Quiet, Verbose

### Community 38 - "DI Registration and Activation"
Cohesion: 0.25
Nodes (8): AddPackageManagement() DI registration, IPackageManagementService (consumer entry point), IPackageSourceService (consumer entry point), Finding: projection default interfaces are internal; IIDs resolved by reflection, PackageSourceService, WinGetFactory (resilient COM activation chain), WinGetSourceClient (source management via COM), WinGetUnavailableException

### Community 39 - "Availability and Hosting Errors"
Cohesion: 0.38
Nodes (7): Resilient COM activation chain, Hosting caveats (elevation, SYSTEM, concurrency), WinGetUnavailableException, Error handling model (throw vs result), Running under SYSTEM / as a Windows Service, WinGetUnavailableException causes, winget.exe resolution strategy

### Community 40 - "Release Triggers and GitVersion"
Cohesion: 0.47
Nodes (6): Release to GitHub Packages step (nuke PublishGitHubPackages), push trigger declaring branches: [main] and tags: ['v*'], GitVersion next-version: 0.1.0 pin, Untagged main = prerelease, v-tag = stable version behaviour, GitHub Packages publishing (prerelease on main, stable on v* tag), fetch-depth: 0 and eager [GitVersion] injection behaviour

### Community 41 - "Nuke Profile Parameters"
Cohesion: 0.40
Nodes (5): type, description, items, type, Profile

### Community 42 - "Package Source Models"
Cohesion: 0.67
Nodes (3): AddPackageSourceRequest, PackageSourceOrigin, PackageSourceTrustLevel

### Community 43 - "Nuke Host Schema Ref"
Cohesion: 0.67
Nodes (3): description, $ref, Host

### Community 44 - "Nuke NoLogo Parameter"
Cohesion: 0.67
Nodes (3): description, type, NoLogo

### Community 45 - "Nuke Plan Parameter"
Cohesion: 0.67
Nodes (3): description, type, Plan

## Ambiguous Edges - Review These
- `The one integration rule: direct ComInterop PackageReference` → `WinGetUnavailableException`  [AMBIGUOUS]
  README.md · relation: conceptually_related_to

## Knowledge Gaps
- **98 isolated node(s):** `$schema`, `type`, `AppVeyor`, `AzurePipelines`, `Bamboo` (+93 more)
  These have ≤1 connection - possible missing edges or undocumented components.
- **5 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **What is the exact relationship between `The one integration rule: direct ComInterop PackageReference` and `WinGetUnavailableException`?**
  _Edge tagged AMBIGUOUS (relation: conceptually_related_to) - confidence is low._
- **Why does `SubZeroDev.WinGet.Models` connect `Assemblies and Namespaces` to `CLI Shim: Pins, Export, Import`, `WinRT Operation Result Mapping`, `Package Source Models`, `Operation Requests and Enums`, `Package Details Metadata`, `IPackageSourceService Contract`, `IWinGetSourceClient Contract`?**
  _High betweenness centrality (0.139) - this node is a cross-community bridge._
- **Why does `WinGetClient` connect `WinRT Package Enums and Options` to `Assemblies and Namespaces`, `WinRT Operation Result Mapping`, `COM Factory and Activation Options`, `Catalog Search and Composites`, `IWinGetClient Contract`, `Operation Requests and Enums`, `Package Details Metadata`?**
  _High betweenness centrality (0.103) - this node is a cross-community bridge._
- **Why does `SubZeroDev.WinGet.Abstractions` connect `Assemblies and Namespaces` to `IWinGetSourceClient Contract`, `CLI Shim: Pins, Export, Import`, `IPackageSourceService Contract`?**
  _High betweenness centrality (0.082) - this node is a cross-community bridge._
- **What connects `$schema`, `type`, `AppVeyor` to the rest of the system?**
  _98 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `PackageManagementService Implementation` be split into smaller, more focused modules?**
  _Cohesion score 0.07277701778385773 - nodes in this community are weakly interconnected._
- **Should `Assemblies and Namespaces` be split into smaller, more focused modules?**
  _Cohesion score 0.053075396825396824 - nodes in this community are weakly interconnected._