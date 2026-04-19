# Architecture Research

**Domain:** Self-hosted .NET-native code quality platform for C#/.NET
**Researched:** 2026-04-19
**Confidence:** MEDIUM

## Standard Architecture

### System Overview

```text
┌───────────────────────────────────────────────────────────────────────┐
│                         Presentation Layer                           │
├───────────────────────────────────────────────────────────────────────┤
│  Browser                                                             │
│     │                                                                │
│     ▼                                                                │
│  CodePass.Web (Blazor Server)                                        │
│  ├── Project registration UI                                         │
│  ├── Rule authoring UI (schema-driven + JSON editor)                 │
│  ├── Manual run controls                                             │
│  └── Dashboard / reports                                             │
└───────────────┬───────────────────────────────────────────────────────┘
                │ commands / queries
                ▼
┌───────────────────────────────────────────────────────────────────────┐
│                         Application Layer                            │
├───────────────────────────────────────────────────────────────────────┤
│  Solution Registry   Rule Catalog   Run Orchestrator   Reporting     │
│  Coverage Config     Score Service  Result Queries                   │
└───────────────┬───────────────────────────────┬───────────────────────┘
                │ writes state                  │ enqueues work
                ▼                               ▼
┌──────────────────────────────┐   ┌───────────────────────────────────┐
│      Relational Database     │   │        Execution Boundary         │
│  solutions / rules / runs /  │   ├───────────────────────────────────┤
│  violations / coverage /     │   │  Job Queue + Background Worker    │
│  projections                 │   │  ├── Rule Analysis Pipeline       │
└──────────────────────────────┘   │  ├── Coverage Ingestion Pipeline  │
                                   │  └── Artifact / log capture       │
                                   └───────────────┬───────────────────┘
                                                   │
                      ┌────────────────────────────┼───────────────────────────┐
                      ▼                            ▼                           ▼
          ┌──────────────────────┐    ┌──────────────────────┐    ┌────────────────────┐
          │ Roslyn / MSBuild     │    │ dotnet test / tools  │    │ Local filesystem   │
          │ MSBuildWorkspace     │    │ coverlet / MTP /     │    │ .sln / projects /  │
          │ compilations         │    │ code coverage output │    │ coverage artifacts │
          └──────────────────────┘    └──────────────────────┘    └────────────────────┘
```

### Recommended Runtime Shape

**Recommendation:** build CodePass as a **modular monolith with an explicit worker boundary**.

That means:
- **One product / one solution / one database** for v1.
- **One web host** for Blazor Server and application APIs.
- **One analysis worker boundary** for long-running rule and coverage jobs.
- The worker can start **in-process** as a hosted background service for v1, but all analysis should sit behind interfaces so it can move to a **separate process/service** without changing the UI or application layer.

This is the right tradeoff for CodePass because:
- v1 is self-hosted and manually triggered, so a distributed system would be premature.
- Roslyn analysis and coverage collection are CPU/memory heavy and should not run inside Blazor request/circuit code.
- The biggest architectural need is **isolation of execution**, not microservices.

### Component Responsibilities

| Component | Responsibility | Talks To | Typical Implementation |
|-----------|----------------|----------|------------------------|
| `CodePass.Web` | UI, user workflows, validation feedback, dashboards | Application layer only | ASP.NET Core + Blazor Server |
| Application layer | Commands/queries, orchestration, transaction boundaries | Web, DB, job queue, reporting services | Vertical slices or use-case handlers |
| Solution Registry | Registers `.sln` paths, validates accessibility, stores solution metadata | Application layer, filesystem adapter, DB | Application service + repository |
| Rule Catalog | Stores rule kinds, schemas, rule definitions, severity metadata | Application layer, DB, Roslyn executor registry | Schema registry + validators |
| Run Orchestrator | Creates run records, enqueues jobs, prevents invalid concurrent runs | Application layer, job queue, DB | Command handler + queue abstraction |
| Job Queue / Worker | Executes long-running jobs off the UI thread | Run orchestrator, Roslyn engine, coverage pipeline, DB | `BackgroundService` + `Channel<T>` queue |
| Roslyn Analysis Engine | Opens solution, builds compilations, executes rule definitions, emits normalized violations | Worker, filesystem, MSBuild/Roslyn, DB | `MSBuildWorkspace` + typed executors |
| Coverage Pipeline | Acquires or imports coverage data, parses report, maps to classes, stores aggregates | Worker, process runner/filesystem, DB | Adapter + parser + mapper pipeline |
| Reporting / Score Service | Reads current violations/coverage and computes pass/fail score | Application layer, DB | Query handlers + projection services |
| Infrastructure Adapters | Filesystem access, process execution, persistence, clock/logging | Application layer and worker | EF Core, CLI wrappers, path services |

## Recommended Project Structure

```text
src/
├── CodePass.Web/                 # Blazor Server host, components, pages, endpoints
│   ├── Components/
│   ├── Pages/
│   ├── Features/
│   └── Program.cs
├── CodePass.Application/         # Use cases, commands, queries, DTOs
│   ├── Solutions/
│   ├── Rules/
│   ├── Runs/
│   ├── Coverage/
│   └── Reporting/
├── CodePass.Domain/              # Core models and invariants
│   ├── Solutions/
│   ├── Rules/
│   ├── Analysis/
│   └── Coverage/
├── CodePass.Analysis/            # Execution-specific analysis logic
│   ├── Roslyn/
│   │   ├── Workspace/
│   │   ├── Sessions/
│   │   ├── Executors/
│   │   └── Diagnostics/
│   ├── Rules/
│   │   ├── Catalog/
│   │   ├── Schemas/
│   │   └── Validation/
│   └── Coverage/
│       ├── Acquisition/
│       ├── Parsers/
│       ├── Mapping/
│       └── Aggregation/
├── CodePass.Worker/              # Hosted services, queue consumers, job runtime
│   ├── Queue/
│   ├── Jobs/
│   └── HostedServices/
├── CodePass.Infrastructure/      # EF Core, filesystem, process runner, configuration
│   ├── Persistence/
│   ├── FileSystem/
│   ├── ProcessExecution/
│   └── DependencyInjection/
└── CodePass.Contracts/           # Cross-boundary contracts / result models
    ├── Runs/
    ├── Rules/
    └── Reporting/

tests/
├── CodePass.Application.Tests/
├── CodePass.Analysis.Tests/
├── CodePass.Infrastructure.Tests/
└── CodePass.Web.Tests/
```

### Structure Rationale

- **`CodePass.Web/`**: keep UI thin; components should trigger application commands/queries, not open solutions or parse XML directly.
- **`CodePass.Application/`**: central place for workflow orchestration and transaction boundaries.
- **`CodePass.Domain/`**: protects the important invariants: rule lifecycle, run lifecycle, solution registration, score semantics.
- **`CodePass.Analysis/`**: isolates Roslyn and coverage complexity from the rest of the app.
- **`CodePass.Worker/`**: makes long-running execution explicit from day one.
- **`CodePass.Infrastructure/`**: keeps process/file/DB concerns replaceable and testable.
- **`CodePass.Contracts/`**: avoids leaking EF entities or Roslyn types across layers.

## Architectural Patterns

### Pattern 1: Modular Monolith with Explicit Execution Boundary

**What:** one application, one database, but a clear internal split between UI/application code and analysis execution code.

**When to use:** exactly CodePass v1—self-hosted, manual execution, low operational complexity, but heavy analysis workloads.

**Trade-offs:**
- **Pros:** simplest deployment, easiest debugging, fastest to ship.
- **Cons:** requires discipline to stop analysis logic from leaking into UI code.

**Example:**
```csharp
public interface IAnalysisJobQueue
{
    ValueTask EnqueueAsync(AnalysisJob job, CancellationToken cancellationToken);
}

public sealed record AnalysisJob(Guid RunId, AnalysisJobType JobType);
```

### Pattern 2: Schema-Driven Rule Catalog + Typed Executors

**What:** each rule kind has:
- a stable identifier,
- a schema,
- a persisted JSON definition,
- a validator,
- and a typed executor that knows how to evaluate it with Roslyn.

**When to use:** custom rule authoring where you want safety, UI rendering, validation, and versioning.

**Trade-offs:**
- **Pros:** rules are evolvable, validatable, and renderable in the UI.
- **Cons:** less flexible than arbitrary C# scripting.

**Recommendation:** for CodePass, this is the correct choice. Do **not** let admins upload arbitrary analyzer assemblies or C# snippets in v1.

**Example:**
```csharp
public interface IRuleExecutor<in TRuleDefinition>
{
    string RuleKind { get; }
    Task<IReadOnlyList<ViolationRecord>> ExecuteAsync(
        TRuleDefinition definition,
        AnalysisSession session,
        CancellationToken cancellationToken);
}
```

### Pattern 3: Per-Run Analysis Session

**What:** each manual run creates an in-memory session that owns the loaded solution, project index, and lazy semantic model access for that run only.

**When to use:** Roslyn analysis of real solutions.

**Trade-offs:**
- **Pros:** predictable memory lifetime, simpler cancellation, easier troubleshooting.
- **Cons:** less cache reuse across runs.

**Recommendation:** cache within a run, not globally forever. Roslyn solutions/compilations are immutable and thread-safe, but large solutions are memory expensive.

**Example:**
```csharp
public sealed class AnalysisSession : IAsyncDisposable
{
    public Solution Solution { get; init; } = default!;
    public IReadOnlyDictionary<ProjectId, ProjectAnalysisContext> Projects { get; init; } = default!;

    public ValueTask DisposeAsync()
    {
        // dispose workspace / cleanup temp artifacts
        return ValueTask.CompletedTask;
    }
}
```

### Pattern 4: Normalize External Formats into Internal Models

**What:** never let raw Roslyn diagnostics or raw coverage XML become your core reporting model. Convert them into stable internal records first.

**When to use:** always.

**Trade-offs:**
- **Pros:** UI and scoring stay stable even if upstream formats change.
- **Cons:** requires explicit mapping code.

**Example:**
```csharp
public sealed record ViolationRecord(
    Guid RuleId,
    string RuleCode,
    string Severity,
    string ProjectName,
    string FilePath,
    int? Line,
    string Message);
```

## Data Flow

### Request / Run Flow

```text
[User clicks Run]
    ↓
[Blazor page / command handler]
    ↓
[Create Run record: Pending]
    ↓
[Enqueue AnalysisJob]
    ↓
[Background Worker dequeues]
    ↓
[Roslyn or Coverage pipeline executes]
    ↓
[Store normalized results + summary]
    ↓
[Mark Run Completed / Failed]
    ↓
[Dashboard queries projections]
    ↓
[User sees current pass/fail state]
```

### Key Data Flows

#### 1. Solution registration

```text
User enters .sln path
    → Web validates basic input
    → Solution Registry checks path exists + accessible
    → Optional probe opens solution metadata
    → DB stores solution record
    → UI shows readiness / errors
```

#### 2. Rule authoring

```text
Admin selects rule kind
    → Rule Catalog returns schema + defaults
    → UI renders form from schema
    → User edits fields / raw JSON
    → Validator checks schema + semantic constraints
    → DB stores rule definition as versioned JSON
```

#### 3. Manual rule analysis

```text
User triggers rule analysis
    → Application creates AnalysisRun
    → Queue receives RuleAnalysis job
    → Worker creates AnalysisSession
    → MSBuild/Roslyn loads solution
    → Rule executors run against projects/documents/symbols
    → Violations normalized and persisted
    → Reporting projection updated
```

#### 4. Manual coverage analysis

```text
User triggers coverage analysis
    → Application creates CoverageRun
    → Queue receives Coverage job
    → Coverage acquisition step either:
         a) runs configured test/coverage command, or
         b) imports an existing report file
    → Parser reads report format (start with Cobertura)
    → Mapper resolves report classes/files to solution types
    → Aggregates persisted by class / project / solution
    → Reporting projection updated
```

#### 5. Dashboard and score

```text
Violations + coverage aggregates + rule severity weights
    → Score service computes current status
    → Query layer returns read model
    → Blazor dashboard renders pass/fail + grouped findings
```

## Roslyn Execution Considerations

### Recommended Roslyn Pipeline

1. **Register MSBuild first** using `Microsoft.Build.Locator` before touching MSBuild types.
2. **Open the `.sln` with `MSBuildWorkspace`**.
3. Capture **workspace load diagnostics** separately from rule violations.
4. Build a **per-run analysis session** with project/document indexes.
5. Execute rules by cheapest viable level:
   - syntax-only rules first,
   - symbol rules second,
   - semantic rules only when required.
6. Normalize results into CodePass models.
7. Dispose the workspace/session after the run.

### Why this matters

- Official Roslyn docs position the **Workspace API** as the starting point for analysis over entire solutions.
- Official MSBuild docs and `Microsoft.Build.Locator` docs emphasize loading the correct MSBuild/SDK context, especially when SDKs and `global.json` are involved.
- This is the critical path for CodePass. If solution loading is flaky, the whole product feels unreliable.

### Specific implementation guidance

- **Do not** instantiate or reference MSBuild types before locator registration.
- **Do not** ship random `Microsoft.Build.*` runtime assemblies beside the app and hope project loading works.
- Treat **workspace load failures** as first-class run results, not unhandled exceptions.
- Model executor capabilities explicitly, for example:
  - `ISyntaxRuleExecutor`
  - `ISymbolRuleExecutor`
  - `ISemanticRuleExecutor`
- Persist both:
  - normalized violations,
  - raw run logs / workspace diagnostics for debugging.

## Coverage Ingestion Considerations

### Recommended Coverage Architecture

Split coverage into **four stages**:

1. **Acquisition** – get a report file or produce one via a command.
2. **Parsing** – read Cobertura/OpenCover/etc. into an intermediate model.
3. **Mapping** – resolve report classes/files to solution types/documents.
4. **Aggregation** – store class/project/solution coverage summaries.

### Opinionated v1 recommendation

- **Start with Cobertura import and normalization first.**
- Then add **command-based acquisition** once the normalized model and mapper are correct.

Why:
- Official .NET docs still show Coverlet producing `coverage.cobertura.xml`.
- Cobertura is human-readable, stable enough, and already contains file/class/line information.
- Parsing and mapping are the real product value; test-process orchestration is an extra layer of complexity.

### Important ecosystem nuance for .NET 10

Current docs show a split between **VSTest-based** and **Microsoft Testing Platform (MTP)** coverage workflows:
- `coverlet.collector` / `--collect:"XPlat Code Coverage"` for VSTest-style runs.
- `coverlet.MTP` / `dotnet test --coverlet` for MTP.
- Microsoft also documents `Microsoft.Testing.Extensions.CodeCoverage` for MTP.
- Coverlet’s official docs explicitly state that `coverlet.collector` and `coverlet.msbuild` are **not compatible with MTP**.

**Architectural implication:** CodePass must not hard-code “coverage means one dotnet test command”. Use a `ICoverageAcquisitionStrategy` boundary.

### Example boundary

```csharp
public interface ICoverageAcquisitionStrategy
{
    Task<CoverageArtifact> AcquireAsync(
        RegisteredSolution solution,
        CoverageRunConfiguration configuration,
        CancellationToken cancellationToken);
}
```

### Practical rules

- Keep raw coverage files as run artifacts for troubleshooting.
- Normalize paths because report output often differs by OS and working directory.
- Map coverage primarily by **document path + fully qualified type name**.
- Expect VSTest to place files under GUID-based `TestResults` folders.
- Keep the acquisition step cancellable and timeout-aware.

## Suggested Build Order

### Dependency Chain

```text
Solution registration
    → Roslyn solution loading spike
    → Rule catalog + schema validation
    → Rule execution pipeline
    → Result persistence
    → Reporting/dashboard
    → Coverage parsing + mapping
    → Coverage command acquisition
    → Unified score/pass-fail model
```

### Recommended Build Sequence

1. **Foundation: solution registry + run model + persistence**
   - Create entities for solutions, rules, runs, violations, coverage summaries.
   - Add path validation and basic run lifecycle states.
   - Reason: every later slice depends on stable identifiers and run tracking.

2. **Technical spike: open real `.sln` files with Roslyn/MSBuild**
   - Prove `MSBuildLocator` + `MSBuildWorkspace` against representative solutions.
   - Capture load diagnostics and environment edge cases.
   - Reason: this is the highest-risk dependency in the system.

3. **Rule catalog skeleton**
   - Implement rule kind registry, schemas, JSON persistence, validators.
   - Keep the DSL closed and typed from day one.
   - Reason: executors depend on stable rule definitions.

4. **Rule execution pipeline**
   - Add worker queue, analysis session, one or two rule executors, normalized violations.
   - Start with syntax/symbol rules before deep semantic rules.
   - Reason: proves the core product loop early.

5. **Manual run UI + results pages**
   - Trigger runs, show run status, show grouped violations, show failures clearly.
   - Reason: turns the backend spike into an end-to-end usable slice.

6. **Coverage parsing + mapping**
   - First support ingesting Cobertura from a known file path or artifact.
   - Build type/document mapping and class-level summaries.
   - Reason: hardest part is normalization and mapping, not the button click.

7. **Coverage acquisition strategies**
   - Add command execution for supported coverage workflows.
   - Separate VSTest and MTP strategies.
   - Reason: tool ecosystem is fragmented; keep this isolated.

8. **Unified dashboard + score model**
   - Combine violation severity + coverage summaries into a single pass/fail view.
   - Reason: scoring only makes sense after both pipelines are trustworthy.

### Build Order Implications

- **Do not build the polished rule-authoring UI before proving solution loading.**
- **Do not build scoring before normalized violation and coverage models exist.**
- **Do not couple coverage implementation to one test runner.**
- **Do not leave the worker boundary for later**; even if v1 is in-process, define it now.

## Scaling Considerations

| Scale | Architecture Adjustments |
|-------|--------------------------|
| 1-10 solutions, mostly one user | Single DB, single worker, concurrency 1 is fine |
| 10-100 solutions or larger codebases | Separate worker process, bounded queue, artifact retention policy, per-solution run locks |
| CI/pipeline usage or many concurrent runs | Multiple workers, external queue if needed, stronger artifact storage, dedicated database tuning |

### Scaling Priorities

1. **First bottleneck: Roslyn solution loading and semantic analysis memory**
   - Fix with bounded concurrency, per-run disposal, and worker isolation.
2. **Second bottleneck: test execution / coverage acquisition time**
   - Fix with cancellable jobs, strategy-specific timeouts, and cached tool detection.

## Anti-Patterns

### Anti-Pattern 1: Running analysis inside Blazor components or request handlers

**What people do:** call Roslyn or `dotnet test` directly from UI event handlers.

**Why it's wrong:** blocks the UI, hurts SignalR responsiveness, and makes cancellation/error handling poor.

**Do this instead:** always create a run record and hand execution to the worker boundary.

### Anti-Pattern 2: Treating custom rules as arbitrary code execution

**What people do:** allow raw C# scripts, uploaded analyzers, or unbounded expression languages too early.

**Why it's wrong:** security risk, versioning nightmare, and very hard to support in a self-hosted admin UI.

**Do this instead:** use a closed rule catalog with schemas and typed executors.

### Anti-Pattern 3: Binding the product model directly to external formats

**What people do:** let raw Roslyn `Diagnostic` objects or coverage XML shape the database and UI.

**Why it's wrong:** leaks tool details everywhere and makes future format/tool changes painful.

**Do this instead:** normalize into stable internal models first.

### Anti-Pattern 4: One global forever-cached workspace

**What people do:** keep one huge loaded solution/workspace alive across all runs.

**Why it's wrong:** stale state, high memory use, hard-to-reason lifecycle.

**Do this instead:** per-run sessions with controlled cache lifetime.

## Integration Points

### External Dependencies

| Service / Dependency | Integration Pattern | Notes |
|----------------------|---------------------|-------|
| Local filesystem | Adapter service | Required for `.sln`, project files, raw artifacts |
| MSBuild / .NET SDK | `Microsoft.Build.Locator` + `MSBuildWorkspace` | Must be initialized correctly before project loading |
| `dotnet test` / coverage tools | Process runner + acquisition strategies | Keep VSTest and MTP logic separate |
| Database | Repository / EF Core projections | Store normalized state, not raw tool objects |

### Internal Boundaries

| Boundary | Communication | Notes |
|----------|---------------|-------|
| Web ↔ Application | Direct DI / command-query calls | Keep UI dumb |
| Application ↔ Worker | Queue abstraction | Allows in-process now, separate process later |
| Worker ↔ Roslyn engine | Interface boundary | Keeps execution testable |
| Worker ↔ Coverage pipeline | Interface boundary | Necessary because tooling varies |
| Reporting ↔ Persistence | Read models / projections | Avoid rebuilding dashboards from raw artifacts every time |

## Sources

- Roslyn overview and Workspace API (official): https://github.com/dotnet/roslyn/blob/main/docs/wiki/Roslyn-Overview.md **HIGH**
- Code analysis in .NET (official): https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/overview **HIGH**
- Analyzer authoring tutorial (official): https://learn.microsoft.com/en-us/dotnet/csharp/roslyn-sdk/tutorials/how-to-write-csharp-analyzer-code-fix **HIGH**
- MSBuild locator guidance (official): https://learn.microsoft.com/en-us/visualstudio/msbuild/find-and-use-msbuild-versions?view=vs-2022 **HIGH**
- MSBuild API overview (official): https://learn.microsoft.com/en-us/visualstudio/msbuild/msbuild-api?view=vs-2022 **HIGH**
- ASP.NET Core hosted services (official): https://learn.microsoft.com/en-us/aspnet/core/fundamentals/host/hosted-services?view=aspnetcore-10.0 **HIGH**
- Queue service / channels (official): https://learn.microsoft.com/en-us/dotnet/core/extensions/queue-service **HIGH**
- Channels in .NET (official): https://learn.microsoft.com/en-us/dotnet/core/extensions/channels **HIGH**
- Blazor project structure (official): https://learn.microsoft.com/en-us/aspnet/core/blazor/project-structure?view=aspnetcore-10.0 **HIGH**
- Unit testing code coverage in .NET (official): https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-code-coverage **HIGH**
- Microsoft Testing Platform code coverage (official): https://learn.microsoft.com/en-us/dotnet/core/testing/microsoft-testing-platform-code-coverage **HIGH**
- Coverlet README and integration docs (official project): https://github.com/coverlet-coverage/coverlet/blob/master/README.md **HIGH**
- Coverlet VSTest integration (official project): https://github.com/coverlet-coverage/coverlet/blob/master/Documentation/VSTestIntegration.md **HIGH**
- Coverlet MSBuild integration (official project): https://github.com/coverlet-coverage/coverlet/blob/master/Documentation/MSBuildIntegration.md **HIGH**

---
*Architecture research for: CodePass*
*Researched: 2026-04-19*
