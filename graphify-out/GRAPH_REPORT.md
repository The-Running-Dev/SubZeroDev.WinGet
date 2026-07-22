# Graph Report - .  (2026-07-22)

## Corpus Check
- Corpus is ~25,959 words - fits in a single context window. You may not need a graph.

## Summary
- 846 nodes · 1857 edges · 39 communities (37 shown, 2 thin omitted)
- Extraction: 92% EXTRACTED · 8% INFERRED · 0% AMBIGUOUS · INFERRED: 155 edges (avg confidence: 0.81)
- Token cost: 62,283 input · 0 output

## Community Hubs (Navigation)
- CLI Shim Pins and Export
- Package Source Service Surface
- Namespaces and File Layout
- CI Pipeline and Architecture Docs
- CLAUDE.md Guidance and Constraints
- CLI Argument and Pin Parsing Tests
- Operation Results and Progress
- COM Activation Factory
- Dependencies and Target Frameworks
- Catalog Search and Composite Lookup
- Live Integration Tests
- Package Management Service API
- Package Examples
- Request Models and Enums
- Nuke Build Schema Fields
- Nuke CI Host Enum
- IWinGetClient Package Client
- Install Option Mapping
- Package Details and Manifest Metadata
- Mutating Source Operations Docs
- Model Record Tests
- DI-Registered Interface Docs
- Setup and Availability Troubleshooting
- Read-only Operations Docs
- Elevation Failures and Pinning Docs
- Nuke Verbosity Schema
- Nuke Build Targets
- Source Examples
- Example Runner Dispatch
- Nuke Build Schema Root
- Nuke Skip and Target Schema
- Release and Publishing Docs
- Nuke Profile Schema
- Platform and SDK Requirements
- Nuke Host Schema Reference
- Nuke Plan Schema
- Nuke Parameters File
- Bash Build Bootstrapper

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
- `build.ps1/build.sh bootstrappers are required, not optional` --semantically_similar_to--> `Install Nuke step (dotnet tool update, idempotent)`  [INFERRED] [semantically similar]
  SPECIFICATION.md → .github/workflows/build.yml
- `Feature overview (docs intro)` --semantically_similar_to--> `SubZeroDev.WinGet library`  [INFERRED] [semantically similar]
  docs/intro.md → README.md
- `Testing strategy (100 mocked unit + 12 explicit integration)` --semantically_similar_to--> `Building & testing guidance`  [INFERRED] [semantically similar]
  SPECIFICATION.md → README.md
- `Origin & lineage (WinUpdater to COM client)` --semantically_similar_to--> `Why this library (motivation)`  [INFERRED] [semantically similar]
  SPECIFICATION.md → docs/intro.md
- `Installing from GitHub Packages feed` --conceptually_related_to--> `CI release job (publish)`  [INFERRED]
  README.md → .github/workflows/build.yml

## Import Cycles
- None detected.

## Hyperedges (group relationships)
- **CI build-to-publish pipeline** — _github_workflows_build_build_job, _github_workflows_build_release_job, _github_workflows_build_tag_trigger, gitversion_next_version, gitversion_prerelease_stable_model, specification_nuke_build_orchestration [EXTRACTED 1.00]
- **Resilient COM activation fallback chain** — specification_wingetfactory, docs_architecture_activation_layer, specification_internal_default_interfaces, specification_winget_unavailable_exception, docs_architecture_hosting_caveats [EXTRACTED 1.00]
- **Verified COM API findings that shape the implementation** — specification_selectors_ord_filters_andd, specification_cswinrt_enumeration_bug, specification_extended_error_code_is_exception, specification_per_operation_progress_structs, specification_internal_default_interfaces, specification_pre_indexed_metadata_quirks, docs_architecture_verified_com_findings [EXTRACTED 1.00]
- **Service surface registered by AddPackageManagement()** — docs_getting_started_addpackagemanagement, docs_getting_started_ipackagemanagementservice, docs_getting_started_ipackagesourceservice, docs_getting_started_iwingetclient, docs_getting_started_iwingetsourceclient, docs_getting_started_iwingetcliclient [EXTRACTED 1.00]
- **CI build and release pipeline** — docs_testing_nuke_build, docs_testing_ci_workflow, docs_testing_gitversion, docs_testing_github_packages_publishing, docs_testing_nuget_org_publishing, docs_testing_stable_release_tag, docs_testing_dual_sdk_targeting [EXTRACTED 1.00]
- **Operation result record family (non-throwing failure reporting)** — docs_usage_packages_packageoperationresult, docs_usage_sources_sourceoperationresult, docs_usage_pins_export_import_clioperationresult, docs_getting_started_error_handling_contract [INFERRED 0.85]
- **Three-layer stack over Microsoft.Management.Deployment** — claude_service_layer, claude_client_layer, claude_activation_layer, claude_microsoft_management_deployment, claude_three_layer_architecture [EXTRACTED 1.00]
- **Five interfaces registered as singletons over one shared factory** — claude_addpackagemanagement, claude_packagemanagementservice, claude_packagesourceservice, claude_wingetclient, claude_wingetsourceclient, claude_wingetcliclient, claude_wingetfactory [EXTRACTED 1.00]
- **Release pipeline: build gate, versioning, and publishing targets** — claude_ci_build_job, claude_ci_release_job, claude_gitversion, claude_tag_trigger_requirement, claude_github_packages_publishing, claude_nuget_publishing, claude_protected_main [EXTRACTED 1.00]

## Communities (39 total, 2 thin omitted)

### Community 0 - "CLI Shim Pins and Export"
Cohesion: 0.06
Nodes (29): CancellationToken, IReadOnlyList, Task, IWinGetCliClient, CliOperationResult, PackagePin, PackagePinKind, CancellationToken (+21 more)

### Community 1 - "Package Source Service Surface"
Cohesion: 0.06
Nodes (43): DateTimeOffset, PackageCatalogInfo, CancellationToken, IProgress, IReadOnlyList, Task, IPackageSourceService, CancellationToken (+35 more)

### Community 2 - "Namespaces and File Layout"
Cohesion: 0.05
Nodes (26): SubZeroDev.WinGet.Examples, SubZeroDev.WinGet.Models, SubZeroDev.WinGet.Com, SubZeroDev.WinGet, SubZeroDev.WinGet.Abstractions, SubZeroDev.WinGet.Tests, Exception, IServiceCollection (+18 more)

### Community 3 - "CI Pipeline and Architecture Docs"
Cohesion: 0.05
Nodes (59): CI build job (tests + coverage), Dual .NET SDK setup (8.0.x + 10.0.x), fetch-depth: 0 checkout policy, Install Nuke step (dotnet tool update, idempotent), NUKE_VERSION pin (10.1.0), push_to_nuget workflow_dispatch input, CI release job (publish), Single Nuke invocation for Test + Coverage (+51 more)

### Community 4 - "CLAUDE.md Guidance and Constraints"
Cohesion: 0.05
Nodes (52): Running CI locally with act (host mode), Three-Step COM Activation Fallback Chain, Activation Layer (internal COM activation), services.AddPackageManagement() DI registration, build.ps1 / build.sh bootstrappers are required, build/_build.csproj (Nuke build project, not in solution), CatalogPackage, CI build job (required status check) (+44 more)

### Community 5 - "CLI Argument and Pin Parsing Tests"
Cohesion: 0.15
Nodes (8): Test, WinGetCliClientTests, CancellationToken, IReadOnlyList, Lazy, List, Task, WinGetCliClient

### Community 6 - "Operation Results and Progress"
Cohesion: 0.14
Nodes (16): DownloadResultStatus, IAsyncOperationWithProgress, InstallProgress, InstallResult, InstallResultStatus, PackageDownloadProgress, RepairProgress, RepairResultStatus (+8 more)

### Community 7 - "COM Activation Factory"
Cohesion: 0.07
Nodes (19): ActivationMode, AddPackageCatalogOptions, CreateCompositePackageCatalogOptions, DllImport, DownloadOptions, EditPackageCatalogOptions, Guid, IntPtr (+11 more)

### Community 8 - "Dependencies and Target Frameworks"
Cohesion: 0.08
Nodes (24): coverlet.collector (6.0.2), FluentAssertions (6.12.0), Microsoft.Extensions.DependencyInjection.Abstractions (10.0.10), Microsoft.Extensions.Logging (10.0.10), Microsoft.Extensions.Logging.Abstractions (10.0.10), Microsoft.Extensions.Logging.Console (10.0.10), Microsoft.NET.Test.Sdk (17.12.0), Moq (4.20.70) (+16 more)

### Community 9 - "Catalog Search and Composite Lookup"
Cohesion: 0.16
Nodes (13): CompositeSearchBehavior, FindPackagesResult, MatchResult, PackageCatalog, PackageCatalogReference, PackageMatchFilter, FindPackagesOptions, PackageInfo (+5 more)

### Community 10 - "Live Integration Tests"
Cohesion: 0.16
Nodes (9): SetUp, Task, Test, WinGetCliClientIntegrationTests, WinGetClientIntegrationTests, WinGetSourceClientIntegrationTests, WinGetCliClient, WinGetClient (+1 more)

### Community 11 - "Package Management Service API"
Cohesion: 0.27
Nodes (6): CancellationToken, IProgress, IReadOnlyList, Task, IPackageManagementService, PackageOperationProgress

### Community 12 - "Package Examples"
Cohesion: 0.31
Nodes (6): Progress, string, CancellationToken, IServiceProvider, Task, PackageExamples

### Community 13 - "Request Models and Enums"
Cohesion: 0.18
Nodes (12): PackageRepairMode, PackageRepairScope, PackageUninstallMode, PackageUninstallScope, DownloadRequest, InstallRequest, RepairRequest, UninstallRequest (+4 more)

### Community 14 - "Nuke Build Schema Fields"
Cohesion: 0.12
Nodes (16): description, type, description, type, description, type, properties, description (+8 more)

### Community 15 - "Nuke CI Host Enum"
Cohesion: 0.12
Nodes (16): enum, AppVeyor, AzurePipelines, Bamboo, Bitbucket, Bitrise, GitHubActions, GitLab (+8 more)

### Community 16 - "IWinGetClient Package Client"
Cohesion: 0.34
Nodes (5): CancellationToken, IProgress, IReadOnlyList, Task, IWinGetClient

### Community 17 - "Install Option Mapping"
Cohesion: 0.14
Nodes (9): CatalogPackage, Error, Options, PackageInstallerType, PackageInstallMode, PackageInstallScope, PackageVersionId, ProcessorArchitecture (+1 more)

### Community 18 - "Package Details and Manifest Metadata"
Cohesion: 0.20
Nodes (8): Documentation, Icon, PackageAgreement, PackageAgreementInfo, PackageDetails, PackageDocumentation, PackageIconInfo, List

### Community 19 - "Mutating Source Operations Docs"
Cohesion: 0.20
Nodes (14): Mutating Examples, AccessDenied Adding/Removing Sources, Install, PackageOperationResult, Repair, Uninstall, Import, AddPackageSourceRequest (+6 more)

### Community 20 - "Model Record Tests"
Cohesion: 0.24
Nodes (3): Test, TestCase, ModelTests

### Community 21 - "DI-Registered Interface Docs"
Cohesion: 0.26
Nodes (12): Low-level IWinGetClient Example, Progress Reporting (IProgress), AddPackageManagement() DI Registration, IPackageManagementService, IPackageSourceService, IWinGetCliClient (winget.exe shim), IWinGetClient, IWinGetSourceClient (+4 more)

### Community 22 - "Setup and Availability Troubleshooting"
Cohesion: 0.18
Nodes (11): Ctrl+C Cooperative Cancellation, SubZeroDev.WinGet.Examples Console Project, Direct ComInterop PackageReference Rule, Error Handling Contract (throw vs result record), Installing from GitHub Packages, WinGetUnavailableException, COMException 0x80040154 (REGDB_E_CLASSNOTREG), Running Under SYSTEM / Windows Service (+3 more)

### Community 23 - "Read-only Operations Docs"
Cohesion: 0.29
Nodes (11): Read-only Examples, Live Integration Tests, ARP\ Prefixed Ids in Results, Export Non-zero Exit With Valid File, Non-WinGet (ARP) Installed Entries, GetInstalled, Search, CliOperationResult (+3 more)

### Community 24 - "Elevation Failures and Pinning Docs"
Cohesion: 0.18
Nodes (11): Install Failure 0x8A150056 (installer prohibits elevation), Install Failure 0x8A150019 (command requires admin), GetAvailableUpgrades, GetDetails, GetPackage, InstallRequest, Update, Pin (+3 more)

### Community 25 - "Nuke Verbosity Schema"
Cohesion: 0.20
Nodes (10): Verbosity, Verbosity, description, enum, $ref, type, Minimal, Normal (+2 more)

### Community 26 - "Nuke Build Targets"
Cohesion: 0.20
Nodes (10): enum, Clean, Compile, Coverage, IntegrationTest, Pack, PublishGitHubPackages, PublishNuGet (+2 more)

### Community 27 - "Source Examples"
Cohesion: 0.53
Nodes (4): CancellationToken, IServiceProvider, Task, SourceExamples

### Community 28 - "Example Runner Dispatch"
Cohesion: 0.25
Nodes (6): Example, CancellationToken, IServiceProvider, Task, Example, ExampleRunner

### Community 29 - "Nuke Build Schema Root"
Cohesion: 0.22
Nodes (8): allOf, definitions, ExecutableTarget, Host, NukeBuild, type, type, $schema

### Community 30 - "Nuke Skip and Target Schema"
Cohesion: 0.22
Nodes (9): $ref, Skip, Target, description, items, type, description, items (+1 more)

### Community 31 - "Release and Publishing Docs"
Cohesion: 0.36
Nodes (8): Running CI Locally with act, GitHub Actions CI Workflow, Code Coverage (coverlet + ReportGenerator), GitHub Packages Publishing, GitVersion Versioning, NuGet.org Publishing (manual), Cutting a Stable Release via v* Tag, CsWinRT Projected Collection Enumerator Bug

### Community 32 - "Nuke Profile Schema"
Cohesion: 0.40
Nodes (5): type, description, items, type, Profile

### Community 33 - "Platform and SDK Requirements"
Cohesion: 0.67
Nodes (4): x64 Platform Pinning, Runtime Requirements (Windows, .NET 8+, x64), Dual SDK Targeting (net8 product, net10 Nuke), Nuke Build Orchestration

### Community 34 - "Nuke Host Schema Reference"
Cohesion: 0.67
Nodes (3): description, $ref, Host

### Community 35 - "Nuke Plan Schema"
Cohesion: 0.67
Nodes (3): description, type, Plan

## Ambiguous Edges - Review These
- `Code Coverage (coverlet + ReportGenerator)` → `CsWinRT Projected Collection Enumerator Bug`  [AMBIGUOUS]
  docs/testing.md · relation: conceptually_related_to

## Knowledge Gaps
- **107 isolated node(s):** `$schema`, `type`, `AppVeyor`, `AzurePipelines`, `Bamboo` (+102 more)
  These have ≤1 connection - possible missing edges or undocumented components.
- **2 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **What is the exact relationship between `Code Coverage (coverlet + ReportGenerator)` and `CsWinRT Projected Collection Enumerator Bug`?**
  _Edge tagged AMBIGUOUS (relation: conceptually_related_to) - confidence is low._
- **Why does `SubZeroDev.WinGet.Models` connect `Namespaces and File Layout` to `CLI Shim Pins and Export`, `Package Source Service Surface`, `Operation Results and Progress`, `Request Models and Enums`, `Package Details and Manifest Metadata`?**
  _High betweenness centrality (0.125) - this node is a cross-community bridge._
- **Why does `WinGetClient` connect `Catalog Search and Composite Lookup` to `Namespaces and File Layout`, `Operation Results and Progress`, `COM Activation Factory`, `Request Models and Enums`, `IWinGetClient Package Client`, `Install Option Mapping`, `Package Details and Manifest Metadata`?**
  _High betweenness centrality (0.093) - this node is a cross-community bridge._
- **Why does `SubZeroDev.WinGet.Abstractions` connect `Namespaces and File Layout` to `Package Source Service Surface`?**
  _High betweenness centrality (0.073) - this node is a cross-community bridge._
- **What connects `$schema`, `type`, `AppVeyor` to the rest of the system?**
  _107 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `CLI Shim Pins and Export` be split into smaller, more focused modules?**
  _Cohesion score 0.06184291898577613 - nodes in this community are weakly interconnected._
- **Should `Package Source Service Surface` be split into smaller, more focused modules?**
  _Cohesion score 0.05605124685426676 - nodes in this community are weakly interconnected._