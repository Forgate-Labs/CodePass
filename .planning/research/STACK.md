# Stack Research

**Domain:** Self-hosted .NET-native code quality platform for C#/.NET
**Researched:** 2026-04-19
**Confidence:** HIGH

## Recommended Stack

### Core Technologies

| Technology | Version | Purpose | Why Recommended | Confidence |
|------------|---------|---------|-----------------|------------|
| .NET SDK + ASP.NET Core + Blazor Web App (Interactive Server) | 10.0 LTS | Single codebase for UI, backend, auth, and job orchestration | In .NET 10, the modern Blazor model is the Blazor Web App with interactive server rendering. It gives you the Blazor Server programming model the product wants, without splitting the app into SPA + API. That is the cleanest fit for a self-hosted, .NET-first admin/reporting product. | HIGH |
| Entity Framework Core | 10.0.6 | ORM, migrations, query layer | EF Core 10 is the current LTS data stack for .NET 10. It keeps the app fully .NET-native, gives first-class migrations, and is enough for CodePass's relational/reporting workload. | HIGH |
| PostgreSQL + Npgsql provider | PostgreSQL 17.x default, Npgsql.EntityFrameworkCore.PostgreSQL 10.0.1 | Primary persistence store | PostgreSQL is the right default for a self-hosted product: open-source, cheap to distribute, cross-platform, and strong for JSON-heavy data. Npgsql 10 adds full EF 10 JSON complex-type support, which is ideal for rule DSL payloads, schema metadata, and flexible analysis result details. | HIGH |
| Built-in hosted services + Channels | .NET 10 / Microsoft.Extensions.Hosting 10.0.6 | Manual analysis and coverage jobs | v1 only needs manually-triggered jobs, not cron scheduling or distributed orchestration. A DB-backed run table plus an in-process `BackgroundService` consuming a bounded `Channel<T>` is the standard low-complexity .NET choice. It avoids Hangfire/Quartz overhead until scheduling becomes a real requirement. | HIGH |
| Roslyn workspace stack | Microsoft.CodeAnalysis.CSharp.Workspaces 5.3.0 + Microsoft.Build.Locator 1.11.2 | Load `.sln` files, inspect syntax/semantics, execute custom rules | This is the correct foundation for a Roslyn-first product. `Microsoft.Build.Locator` makes your process resolve the same MSBuild toolchain the SDK uses, and Roslyn workspaces let you analyze full solutions with real compilation context instead of text scanning. | HIGH |
| Structured DSL schema stack | Built-in `System.Text.Json.Schema` in .NET 10 + JsonSchema.Net 9.2.0 | Generate rule-kind schemas and validate rule instances | Use `System.Text.Json` schema export for schema generation from typed .NET contracts, then use JsonSchema.Net for runtime validation/evaluation. This keeps the DSL contract aligned with the app's serializer, avoids serializer drift, and gives you strong runtime validation for admin-authored JSON. | HIGH |
| Coverage ingestion stack | Cobertura XML as canonical format; collect via `coverlet.collector` 10.0.0 when available, merge with `dotnet-coverage` | Coverage collection and normalization by class | For a product that must ingest coverage from real solutions, Cobertura is the practical canonical interchange format. Coverlet remains the most common portable path in .NET test projects, official docs still center it, and `dotnet-coverage` can merge reports across test projects. Store normalized class-level coverage in your own tables. | MEDIUM |
| Native service hosting + optional OCI packaging | Microsoft.Extensions.Hosting.WindowsServices 10.0.6 / Systemd 10.0.6; SDK container publish via `dotnet publish /t:PublishContainer` | Self-hosted deployment | Because CodePass analyzes local filesystem paths and shells out to the local .NET toolchain, native host deployment should be the default. Container images are useful, but they should be a secondary distribution mode, not the only one. | HIGH |

## Prescriptive Notes by Concern

### UI
- Use **Blazor Web App on .NET 10 configured for Interactive Server render mode**.
- Add **MudBlazor 9.3.0** for tables, dialogs, forms, filters, chips, drawers, charts/cards, and dense admin screens.
- Rationale: CodePass is an operator dashboard, not a marketing site or public SPA. Blazor Server-style interactivity plus a mature component library is the shortest path to a productive .NET-native UI.

### Backend
- Build a **modular monolith**, not microservices.
- Keep the web app, orchestration layer, Roslyn executors, scoring engine, and persistence in one solution.
- Use **`System.Diagnostics.Process` / `ProcessStartInfo`** to invoke `dotnet build`, `dotnet test`, and coverage commands under a controlled working directory and service account.
- Rationale: the product runs on one machine against local paths. A networked analyzer fleet is complexity without benefit in v1.

### Persistence
- Use **PostgreSQL** as the single source of truth.
- Model strongly relational entities normally: `Projects`, `Rules`, `RuleKinds`, `AnalysisRuns`, `Violations`, `CoverageRuns`, `CoverageByClass`, `Scores`.
- Store flexible rule payloads and per-run metadata in **JSONB** columns mapped with EF Core complex types.
- Rationale: CodePass has both relational reporting needs and JSON-heavy rule/config payloads. PostgreSQL handles both cleanly.

### Background Execution
- Create a **durable run record in the database** when a user clicks “Run analysis” or “Run coverage”.
- Enqueue only a lightweight work item in a bounded `Channel<T>`.
- A single `BackgroundService` (or small fixed pool) picks jobs, updates status, executes subprocesses, persists results, and emits logs/telemetry.
- Rationale: you need reliability and visibility, but not a separate job platform yet.

### Roslyn Integration
- Call **`MSBuildLocator.RegisterDefaults()` before any MSBuild API is touched**.
- Load solutions with **Roslyn workspaces**, not ad-hoc file parsing.
- Build the rule engine as a closed catalog of **typed executors** that consume Roslyn syntax/semantic models.
- Keep rule authoring data-only: JSON DSL in DB, executors in trusted product code.
- Rationale: this preserves safety, versionability, and predictable performance.

### Coverage Ingestion
- Use **Cobertura XML as the internal interchange format**.
- Preferred collection path for typical .NET test projects: `dotnet test --collect:"XPlat Code Coverage"`.
- If a controlled solution uses Microsoft Testing Platform, support its **Cobertura output path** too, but do not make that your only path.
- Merge per-project reports with **`dotnet-coverage merge -f cobertura`** before ingestion.
- Parse merged Cobertura into your own class-level tables. Do **not** treat HTML output as the source of truth.
- Rationale: CodePass needs stable machine-readable coverage data by class, not just pretty reports.

### Packaging and Deployment
- **Default distribution:** native self-hosted process.
  - Windows: support service install with `UseWindowsService()`.
  - Linux: support `UseSystemd()` and a documented unit file.
- **Secondary distribution:** OCI image published with the .NET SDK and a sample `docker-compose.yml` for app + PostgreSQL.
- Rationale: native hosting matches the “register a local `.sln` path on this machine” model best. Docker-only deployment makes filesystem access, SDK resolution, and test execution harder than they need to be.

## Supporting Libraries

| Library | Version | Purpose | When to Use | Confidence |
|---------|---------|---------|-------------|------------|
| MudBlazor | 9.3.0 | Blazor component library for admin UI | Use for the main dashboard, rule editor forms, tables, grouped violation views, and action dialogs. | HIGH |
| ASP.NET Core Identity | In-box with ASP.NET Core 10 | Basic auth, users, cookies, roles | Use if the self-hosted install needs login at all. Keep it simple: local accounts + cookie auth. | HIGH |
| Serilog.AspNetCore | 10.0.0 | Structured logging | Use for job lifecycle logs, subprocess output correlation, and audit-friendly event records. | HIGH |
| OpenTelemetry.Extensions.Hosting | 1.15.2 | Traces, metrics, exporter setup | Use for request latency, job duration, queue depth, failed runs, and DB/subprocess instrumentation. | HIGH |
| Microsoft.Extensions.Hosting.WindowsServices | 10.0.6 | Windows service integration | Use in native Windows deployments. | HIGH |
| Microsoft.Extensions.Hosting.Systemd | 10.0.6 | Linux systemd integration | Use in native Linux deployments. | HIGH |
| JsonSchema.Net | 9.2.0 | Runtime JSON Schema validation | Use to validate rule instances, schema-backed editor payloads, and import/export integrity. | HIGH |
| dotnet-coverage | Latest tool compatible with .NET 10 SDK | Merge/convert coverage reports | Use when multiple test projects produce multiple coverage files that must become one solution-level dataset. | HIGH |
| ReportGenerator.Core | 5.5.5 | Optional human-readable coverage reports | Use only if you want HTML exports or debugging views. It is optional, not the canonical ingestion path. | HIGH |

## Development Tools

| Tool | Purpose | Notes |
|------|---------|-------|
| `dotnet-ef` 10.0.6 | EF migrations and DB management | Install as a local or global tool; keep runtime and tooling on EF 10. |
| Docker Compose | Local dev DB and optional packaged install | Good for PostgreSQL in development and optional production packaging, but not as the only deployment story. |
| .NET SDK 10.0.x | Build/test/runtime baseline | CodePass must run on a machine that also has access to the SDK/MSBuild needed to analyze target solutions. |
| `dotnet test` | Test and coverage command execution | The app should wrap this, not re-implement test discovery/execution internally. |

## Installation

```bash
# Create the app (modern Blazor Server-style setup)
dotnet new blazor -n CodePass

# Data access
dotnet add package Npgsql.EntityFrameworkCore.PostgreSQL --version 10.0.1
dotnet add package Microsoft.EntityFrameworkCore.Design --version 10.0.6

# Roslyn + solution loading
dotnet add package Microsoft.CodeAnalysis.CSharp.Workspaces --version 5.3.0
dotnet add package Microsoft.Build.Locator --version 1.11.2

# UI
dotnet add package MudBlazor --version 9.3.0

# DSL / schema validation
dotnet add package JsonSchema.Net --version 9.2.0

# Logging + telemetry
dotnet add package Serilog.AspNetCore --version 10.0.0
dotnet add package OpenTelemetry.Extensions.Hosting --version 1.15.2

# Native service hosting (only add what you ship)
dotnet add package Microsoft.Extensions.Hosting.WindowsServices --version 10.0.6
dotnet add package Microsoft.Extensions.Hosting.Systemd --version 10.0.6

# Tooling
dotnet tool install --global dotnet-ef --version 10.0.6
dotnet tool install --global dotnet-coverage
```

### Coverage packages for solutions you control

```bash
# Most common path in real-world test projects
dotnet add <test-project> package coverlet.collector --version 10.0.0

# Optional human-readable report generation
dotnet add package ReportGenerator.Core --version 5.5.5
```

## Alternatives Considered

| Recommended | Alternative | When to Use Alternative |
|-------------|-------------|-------------------------|
| Blazor Web App + Interactive Server | React/Next.js + ASP.NET Core API | Use React only if a separate frontend team, public web UX, or offline-heavy SPA behavior becomes a core product requirement. That is not CodePass v1. |
| PostgreSQL + Npgsql | SQL Server + EF Core SQL Server provider | Use SQL Server if the target buyers are already standardized on Microsoft database infrastructure and licensing cost is irrelevant. |
| Built-in `BackgroundService` + `Channel<T>` + DB run records | Hangfire or Quartz.NET | Use Hangfire/Quartz only when recurring schedules, retries, operator dashboards, and job orchestration become first-class product requirements. |
| Roslyn workspaces + MSBuildLocator | Buildalyzer or raw CLI parsing | Use Buildalyzer only if you mostly need project evaluation/build metadata and not a long-lived semantic analysis workspace. |
| Cobertura as canonical coverage format | Visual Studio `.coverage` binary as canonical format | Use `.coverage` only in a tightly controlled Microsoft-only environment. For a self-hosted product that must ingest portable results, Cobertura is better. |
| Native service first, container optional | Docker-only deployment | Use Docker-only if every analyzed solution will be bind-mounted into containers and you can standardize the SDK/toolchain inside the container image. |

## What NOT to Use

| Avoid | Why | Use Instead |
|-------|-----|-------------|
| Docker-only as the initial deployment model | It fights the product's core workflow: local `.sln` paths, host filesystem access, and host SDK/tooling resolution. | Native Windows service/systemd first; offer Docker as an optional packaging mode. |
| Hangfire/Quartz on day one | v1 has manual runs only. Adding a scheduler and job framework now creates operational overhead with little product value. | DB-backed run records + `BackgroundService` + `Channel<T>`. |
| SQLite as the primary production DB | It is attractive for demos, but weaker for concurrent background jobs, richer reporting queries, and JSON-heavy growth. | PostgreSQL. |
| Free-form user-authored C# scripts for custom rules | Arbitrary code execution is a security and maintenance trap, especially in a self-hosted admin tool. | Closed rule catalog + typed JSON DSL + trusted executor implementations. |
| SPA + API + worker microservice split | It adds deployment and debugging complexity before the product has proven scale needs. | Modular monolith with optional later worker extraction. |
| HTML coverage reports as the canonical stored artifact | They are presentation outputs, not stable ingestion formats. | Cobertura XML normalized into your own schema. |

## Stack Patterns by Variant

**If this is a single-user or founder-operated install:**
- One ASP.NET Core 10 app
- In-process queue worker
- PostgreSQL on the same machine or local network
- Native Windows service or systemd deployment
- Because this matches the local-path analysis model with minimal operational overhead

**If this grows into a small team install with heavier scans:**
- Keep the same UI and DB stack
- Split analysis execution into a separate Worker Service process that shares the same database and rule engine library
- Because long-running Roslyn and test execution workloads can then be isolated without changing the product model

**If you must support containerized installs:**
- Publish an OCI image with `dotnet publish /t:PublishContainer`
- Run PostgreSQL alongside it in Compose
- Require explicit bind mounts for solution roots and ensure the container has the required .NET SDK/toolchain visibility
- Because containerization is viable, but only when the filesystem and SDK assumptions are made explicit

## Version Compatibility

| Package A | Compatible With | Notes |
|-----------|-----------------|-------|
| `net10.0` | ASP.NET Core 10 / EF Core 10.0.6 | Stay on the .NET 10 LTS line across runtime and framework packages. |
| `Npgsql.EntityFrameworkCore.PostgreSQL 10.0.1` | EF Core 10.0.x | Matches the EF 10 generation and adds EF 10 JSON complex-type support. |
| `MudBlazor 9.3.0` | .NET 8/9/10 | NuGet lists full support for .NET 10. |
| `Microsoft.CodeAnalysis.CSharp.Workspaces 5.3.0` | `Microsoft.Build.Locator 1.11.2` | Register MSBuild before touching MSBuild APIs. |
| `Microsoft.Extensions.Hosting.WindowsServices 10.0.6` | `Microsoft.Extensions.Hosting` 10.0.6 | Keep hosting package versions aligned. |
| `Microsoft.Extensions.Hosting.Systemd 10.0.6` | `Microsoft.Extensions.Hosting` 10.0.6 | Keep hosting package versions aligned. |
| `coverlet.collector 10.0.0` | `.NET SDK v8.0.414+`, `Microsoft.NET.Test.Sdk >= 17.13.0` | Good default for Cobertura collection in existing test projects. |

## Sources

- [HIGH] https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-10/overview — verified .NET 10 is current LTS and SDK/container/testing updates
- [HIGH] https://learn.microsoft.com/en-us/aspnet/core/blazor?view=aspnetcore-10.0 — verified Blazor Web App + interactive server model and Blazor Server behavior
- [HIGH] https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity?view=aspnetcore-10.0 — verified Blazor Web App + Individual Accounts / interactive server template guidance
- [HIGH] https://learn.microsoft.com/en-us/ef/core/what-is-new/ef-core-10.0/whatsnew — verified EF Core 10 is the .NET 10 LTS release
- [HIGH] https://www.npgsql.org/efcore/release-notes/10.0.html — verified Npgsql 10 full support for EF 10 JSON complex types
- [HIGH] https://www.npgsql.org/efcore/ — verified provider usage and EF integration model
- [HIGH] https://learn.microsoft.com/en-us/aspnet/core/fundamentals/host/hosted-services?view=aspnetcore-10.0 — verified built-in hosted service and queued background task guidance
- [HIGH] https://learn.microsoft.com/en-us/dotnet/core/extensions/queue-service — verified `Channel<T>` queue-service pattern
- [HIGH] https://www.nuget.org/packages/Microsoft.CodeAnalysis.CSharp.Workspaces/ — verified current Roslyn workspace package version and .NET 10 compatibility
- [HIGH] https://www.nuget.org/packages/Microsoft.Build.Locator/ — verified current package version and MSBuild registration guidance
- [HIGH] https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/extract-schema — verified built-in JSON schema export from .NET types
- [HIGH] https://www.nuget.org/packages/JsonSchema.Net/ — verified current validator package version and .NET 10 compatibility
- [HIGH] https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-code-coverage — verified Coverlet + Cobertura + ReportGenerator guidance
- [HIGH] https://learn.microsoft.com/en-us/dotnet/core/additional-tools/dotnet-coverage — verified `dotnet-coverage` merge/collect capabilities and Cobertura support
- [HIGH] https://learn.microsoft.com/en-us/dotnet/core/testing/microsoft-testing-platform-code-coverage — verified Microsoft Testing Platform coverage options and alternative path
- [HIGH] https://www.nuget.org/packages/coverlet.collector — verified current collector version and minimum test SDK note
- [HIGH] https://www.nuget.org/packages/MudBlazor/ — verified current package version and .NET 10 support
- [HIGH] https://www.nuget.org/packages/Serilog.AspNetCore/ — verified current package version
- [HIGH] https://www.nuget.org/packages/Microsoft.Extensions.Hosting.WindowsServices/ — verified Windows service package/version
- [HIGH] https://www.nuget.org/packages/Microsoft.Extensions.Hosting.Systemd/ — verified systemd package/version
- [HIGH] https://learn.microsoft.com/en-us/dotnet/core/containers/sdk-publish — verified SDK-native container publishing for .NET apps
- [HIGH] https://learn.microsoft.com/en-us/dotnet/core/diagnostics/observability-with-otel — verified OpenTelemetry guidance in .NET
- [HIGH] https://opentelemetry.io/docs/languages/dotnet/getting-started/ — verified current OpenTelemetry .NET setup guidance

---
*Stack research for: self-hosted .NET-native code quality platform for C#/.NET*
*Researched: 2026-04-19*
