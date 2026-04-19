# Pitfalls Research

**Domain:** Self-hosted .NET-native code quality platform for C#/.NET (Roslyn analysis + coverage + custom DSL rules)
**Researched:** 2026-04-19
**Confidence:** MEDIUM

## Critical Pitfalls

### Pitfall 1: Assuming `MSBuildWorkspace` means “build parity”

**What goes wrong:**
The platform loads a `.sln` or `.csproj`, gets a Roslyn `Compilation`, and assumes that is equivalent to what `dotnet build` or Visual Studio sees. It is not. Design-time build behavior, generated files, SDK resolution, WPF/XAML, WinForms generators, source generators, multi-targeting, and environment differences can all make the analysis graph incomplete or materially different from a real build.

**Why it happens:**
Roslyn makes project loading look deceptively simple. But `MSBuildWorkspace` is a project-loading abstraction over MSBuild/design-time build, not a guaranteed “exact build output” API. Real projects depend on SDK tasks, generated code, analyzer references, and host/runtime details.

**How to avoid:**
- Treat project loading as a **fidelity problem**, not just a file-open problem.
- Define a supported v1 matrix explicitly: SDK-style C# projects first, then specific special cases.
- Persist and inspect `workspace.Diagnostics` for every load.
- Model analysis status as `Loaded`, `PartiallyLoaded`, `Unsupported`, `Failed`.
- Keep target framework and project type visible in result records.
- Validate early against real-world repos that include: source generators, multi-targeting, WPF/WinForms, `Directory.Build.*`, `global.json`, custom analyzers.
- Do not ship a score if any required project in the solution was only partially loaded.

**Warning signs:**
- “Build works in Visual Studio, but CodePass reports missing symbols.”
- Errors like `InitializeComponent` or `ApplicationConfiguration` missing.
- Generated code appears absent from analysis.
- One machine loads the solution, another does not.
- `workspace.Diagnostics` contains warnings that are ignored by the app.

**Phase to address:**
**Phase 2 — Roslyn loading fidelity & evidence model** (with Phase 1 support for environment capture).

---

### Pitfall 2: Letting SDK/MSBuild environment drift change results

**What goes wrong:**
The same repository produces different findings depending on machine state: installed SDKs, `global.json`, selected MSBuild instance, host runtime, workloads, targeting packs, or container image. Users lose trust because analysis is not reproducible.

**Why it happens:**
.NET SDK resolution is environment-sensitive. If the repo does not pin enough, the highest compatible SDK may be selected. If the host/runtime differs, analyzer references and project load behavior can change. `MSBuildWorkspace` is especially sensitive to the host/runtime/MSBuild combination.

**How to avoid:**
- Record an **analysis environment fingerprint** per run:
  - solution path
  - SDK version selected
  - `global.json` presence and path
  - MSBuild instance/path
  - target frameworks discovered
  - OS / container image / architecture
- Show that fingerprint in the UI for every run.
- Fail fast when required SDKs/workloads/targeting packs are missing.
- Prefer isolated worker execution over loading projects in the web process.
- Test CodePass against repos with and without `global.json`.
- Make “unsupported environment” a first-class run outcome, not a hidden warning.

**Warning signs:**
- Findings differ between two machines for the same solution.
- Logs show a newer SDK being chosen than the repo expects.
- `The SDK 'Microsoft.NET.Sdk' specified could not be found` or similar load errors.
- Analyzer reference lists differ unexpectedly between runs.
- Users say “it passes on my machine, fails on the server.”

**Phase to address:**
**Phase 1 — Secure execution boundary & project registration** and **Phase 2 — Roslyn loading fidelity & evidence model**.

---

### Pitfall 3: Treating build/test execution as harmless local analysis

**What goes wrong:**
CodePass runs `dotnet build`/`dotnet test`-adjacent workflows with full trust inside the main app process or under an over-privileged account. A registered solution can execute custom MSBuild tasks, `Exec` commands, test code, or package-provided targets that modify the host, exfiltrate data, or destabilize the server.

**Why it happens:**
“Self-hosted” and “local path” create a false sense of safety. But MSBuild tasks are executable .NET code, and the MSBuild `Exec` task can run arbitrary commands. Test runs execute user code by definition.

**How to avoid:**
- Never run solution build/test/coverage work inside the Blazor Server process.
- Use a dedicated worker process or worker service with least privilege.
- Restrict accessible paths to an explicit allowlist root.
- Normalize and validate registered paths; reject suspicious symlink/UNC/out-of-root cases in v1.
- Use per-run working directories and clean them up.
- Apply timeouts, cancellation, and max concurrent run limits.
- Prefer read-only mounts/access for source when possible; separate writable temp/artifact directories.
- Log exactly which commands and tools were invoked.

**Warning signs:**
- A solution registration can point anywhere on disk.
- Analysis workers can read secrets outside the intended repo root.
- Builds leave modified files in the repo.
- A bad analysis run can crash or freeze the web UI.
- The architecture diagram shows the web app directly invoking build/test logic.

**Phase to address:**
**Phase 1 — Secure execution boundary & project registration**.

---

### Pitfall 4: Ignoring generated code, analyzer config scope, and target-framework context

**What goes wrong:**
The product floods users with noise from generated code, or misses real issues because it ignores `.editorconfig` / analyzer configuration, `generated_code` markers, per-project settings, or target-framework-specific behavior. Multi-targeted projects get merged into one ambiguous result set.

**Why it happens:**
In .NET, diagnostics are shaped by more than syntax trees. Analyzer severities, generated code treatment, SDK defaults, implicit imports, `Directory.Build.props/targets`, and target framework all affect what “correct” analysis means.

**How to avoid:**
- Load and honor analyzer config / `.editorconfig` inputs.
- Respect generated-code exclusions and add CodePass-specific generated-code filters for user-defined patterns.
- Exclude `obj/` and `bin/` from user-facing findings unless explicitly needed for diagnostics.
- Store findings with `TargetFramework` where applicable.
- Make result grouping aware of `Project + Document + TargetFramework + Rule`.
- Add integration tests covering:
  - `.designer.cs` / `.generated.cs`
  - source-generated files
  - multi-targeted projects
  - solution-wide and directory-scoped config files

**Warning signs:**
- Violations appear primarily in generated files.
- Users ask why the same file has duplicated findings.
- Findings differ from IDE severity without explanation.
- Results collapse multiple target frameworks into one “project result.”
- `obj` output or generated code dominates the issue list.

**Phase to address:**
**Phase 2 — Roslyn loading fidelity & evidence model** and **Phase 3 — Rule DSL & authoring UX**.

---

### Pitfall 5: Treating coverage as simple percentage plumbing

**What goes wrong:**
CodePass ingests coverage and confidently shows 0%, `NaN`, or misleading per-class numbers because the underlying coverage run was incomplete, nondeterministic, or broken by deterministic builds, SourceLink, test host shutdown timing, assembly resolution, huge reports, or parallel file contention.

**Why it happens:**
Coverage looks simpler than static analysis, but .NET coverage tooling has real edge cases. Coverlet’s own docs call out early test process termination, deterministic build complications, unresolved assemblies during instrumentation, report-size failures, and parallel execution issues.

**How to avoid:**
- Support **one known-good coverage path first**; do not promise “any .NET test setup works.”
- Prefer the collector-based path for v1 if it gives better reliability for your supported scenarios.
- Persist raw coverage artifacts and run logs before summarizing.
- Add explicit run states: `Succeeded`, `Incomplete`, `Invalid`, `Unsupported`.
- Never coerce coverage tool failure into `0%`.
- Test against:
  - deterministic builds
  - SourceLink
  - multi-targeting
  - parallel test execution
  - large solutions / large reports
- Exclude known-noise generated code before score calculation.

**Warning signs:**
- Coverage is 0% only on some machines.
- `NaN%` appears in logs or reports.
- “Hits file not found”, “Failed to instrument modules”, or PDB lock errors appear.
- Coverage succeeds locally but fails in container/CI-like environments.
- Class-level coverage is missing for only some assemblies.

**Phase to address:**
**Phase 4 — Coverage pipeline, scoring & dashboard trust**.

---

### Pitfall 6: Computing a score from partial, stale, or non-comparable evidence

**What goes wrong:**
The dashboard shows a crisp pass/fail score even when the underlying rule run partially failed, some projects did not load, coverage is invalid, or the result was produced under a different rule catalog/environment. The UI looks trustworthy while the data is not.

**Why it happens:**
Scoring is tempting to build early, but score compression hides uncertainty. In code quality products, the score is only as trustworthy as the evidence model and provenance behind it.

**How to avoid:**
- Make the score depend on a validated evidence bundle, not ad hoc latest rows.
- Store provenance with each run:
  - rule catalog version
  - rule instance versions
  - environment fingerprint
  - solution snapshot metadata
  - coverage run status
  - project load completeness
- Block score publication when required inputs are incomplete.
- Show “Incomplete analysis” instead of a number when evidence is unreliable.
- Keep raw findings and raw coverage artifacts so the score is explainable.
- Version the scoring formula.

**Warning signs:**
- The score changes but users cannot tell why.
- A run with partial failures still updates the dashboard as normal.
- Re-running the same solution with unchanged code yields materially different scores.
- The system stores only final percentages, not raw findings.

**Phase to address:**
**Phase 2 — Roslyn loading fidelity & evidence model** and **Phase 4 — Coverage pipeline, scoring & dashboard trust**.

---

### Pitfall 7: Building a custom-rule system that can create invalid or unmaintainable rules

**What goes wrong:**
The rule DSL starts simple, then grows into a half-scriptable object bag with rule-specific exceptions, UI-only constraints, backend-only semantics, and no migration path. Rules become hard to validate, hard to render, hard to execute efficiently, and impossible to evolve safely.

**Why it happens:**
Custom rules are the differentiator, so teams over-flex the model too early. The ecosystem trend is the opposite: strong schemas, structured authoring, version bounds, and validation to prevent invalid rules and reduce authoring friction.

**How to avoid:**
- Keep the rule catalog **closed and explicit** in v1.
- Give every rule kind a JSON schema and a `schemaVersion`.
- Validate in the backend, not only the UI.
- Ensure UI round-trips losslessly to raw JSON.
- Add migration infrastructure before adding many rule kinds.
- Store execution semantics separately from form layout metadata.
- Allow extensibility by adding new rule kinds, not by adding arbitrary scripting hooks.
- Provide “preview against sample solution/repo” before rule publication.

**Warning signs:**
- Product discussions keep ending with “just add a custom JSON field.”
- The UI cannot reopen rules it previously saved.
- Rules serialize differently after editing even when logically unchanged.
- A rule kind requires code changes in many unrelated places.
- You need manual exceptions to keep old rules working.

**Phase to address:**
**Phase 3 — Rule DSL & authoring UX**.

---

### Pitfall 8: Making every run a cold, full-solution recomputation

**What goes wrong:**
Analysis becomes painfully slow even for medium solutions because every manual run fully reloads the solution, rebuilds semantic state, re-evaluates rules one-by-one without reuse, and reparses large artifacts in memory. Users stop trusting the tool because it feels heavy—the exact problem CodePass is supposed to avoid.

**Why it happens:**
MSBuild/design-time build is expensive. Roslyn project loading is not free. Coverage reports can be large. If the product stores only final results and not reusable intermediate artifacts, it has to redo everything every time.

**How to avoid:**
- Separate discovery, load, analysis, and summarization stages.
- Cache stable inputs where safe: solution metadata, project graph, rule compilation/plans.
- Reuse compilations/semantic state across rules within a run.
- Add bounded concurrency and cancellation support.
- Prevent overlapping runs for the same solution unless explicitly queued.
- Stream/process large coverage artifacts instead of loading everything blindly into memory.
- Instrument duration by stage from day one.

**Warning signs:**
- Solution open time dominates the run.
- Medium repos take tens of seconds or minutes before first findings appear.
- Running more rules scales linearly in a visibly bad way.
- Multiple runs for the same solution fight over temp files or artifacts.
- Memory spikes when parsing coverage output.

**Phase to address:**
**Phase 5 — Performance hardening & operational safety** (with basic instrumentation added in earlier phases).

## Technical Debt Patterns

Shortcuts that seem reasonable but create long-term problems.

| Shortcut | Immediate Benefit | Long-term Cost | When Acceptable |
|----------|-------------------|----------------|-----------------|
| Store only the final score and summary counts | Fast to demo | Impossible to explain, audit, or recompute results when scoring changes | Never |
| Run analysis/coverage inside the web app process | Fewer moving parts | Security risk, poor isolation, UI instability, memory leaks | Never |
| Treat unsupported project types as “best effort success” | Fewer blocked runs | Silent false confidence and bad scores | Never |
| Make the DSL a generic JSON blob with optional fields everywhere | Faster initial authoring | Rule sprawl, fragile UI, impossible migrations | Only for throwaway internal prototypes, not product code |
| Ignore environment fingerprinting | Less metadata to store | Non-reproducible results and endless support/debug pain | Never |
| Merge findings across TFMs into one flat result row | Simpler UI | Ambiguous diagnostics, duplicates, wrong comparisons | Only if v1 explicitly supports single-target projects only |
| Convert failed coverage runs into 0% | Clean dashboard | Misleading quality signal, destroys trust | Never |

## Integration Gotchas

Common mistakes when connecting to external services and tooling.

| Integration | Common Mistake | Correct Approach |
|-------------|----------------|------------------|
| `MSBuildWorkspace` | Assuming `OpenSolutionAsync` + `GetCompilationAsync` gives full build parity automatically | Treat it as design-time build loading; inspect diagnostics; validate generators/generated files explicitly |
| `Microsoft.Build.Locator` / MSBuild | Registering too late or shipping conflicting `Microsoft.Build.*` assemblies | Register MSBuild before creating the workspace; keep assembly-loading behavior controlled |
| `.NET SDK` / `global.json` | Ignoring solution-relative SDK resolution rules | Detect and persist the resolved SDK and `global.json` context for every run |
| Coverlet / coverage collection | Supporting multiple drivers immediately and assuming they are equivalent | Pick one reliable v1 path and model invalid/incomplete coverage explicitly |
| Local filesystem solution registration | Accepting any path string at face value | Normalize, validate, restrict to allowed roots, and detect symlink/UNC edge cases |
| Analyzer config / `.editorconfig` | Reading source files without config scope | Load analyzer config context and generated-code settings with the project graph |

## Performance Traps

Patterns that work at small scale but fail as usage grows.

| Trap | Symptoms | Prevention | When It Breaks |
|------|----------|------------|----------------|
| Cold-opening the full solution for every run | Long startup before any result | Cache project graph metadata; reuse within a run; measure stage timings | Medium repos (10s+ projects) |
| Running each rule as a separate semantic walk with no reuse | Rule count directly multiplies runtime | Build reusable execution plans and share semantic state | Dozens of rules |
| Recomputing coverage summaries from huge raw XML in memory | High RAM and slow UI | Stream or preprocess artifacts in a worker | Large enterprise solutions / integration test suites |
| Allowing concurrent runs against the same solution/temp paths | File locks, corrupted artifacts, flaky coverage | Add per-solution run locks/queues and per-run artifact folders | As soon as two users or repeated clicks happen |
| No cancellation or timeout boundaries | Hung runs and zombie workers | Add cancellation tokens, watchdogs, and cleanup hooks | First bad custom task or hanging test suite |

## Security Mistakes

Domain-specific security issues beyond general web security.

| Mistake | Risk | Prevention |
|---------|------|------------|
| Running build/test/coverage inside the main web app | Remote code execution impact reaches the whole product host | Use isolated workers with least privilege |
| Allowing unrestricted local path registration | Reading arbitrary files, scanning outside intended repos, host compromise surface expansion | Enforce allowed roots, canonicalize paths, reject suspicious traversal/symlink cases |
| Storing raw source snippets and full absolute paths carelessly in UI/logs | Secret leakage and infrastructure disclosure | Redact where possible; separate diagnostic logs from user-visible results |
| Reusing a shared writable artifact directory across runs | Cross-run contamination and tampering | Use per-run isolated directories and cleanup policies |
| Assuming “self-hosted” means trusted code | Underestimating malicious or simply dangerous build/test logic | Document trust boundary clearly and design for hostile workloads anyway |

## UX Pitfalls

Common user experience mistakes in this domain.

| Pitfall | User Impact | Better Approach |
|---------|-------------|-----------------|
| Showing one score without explaining what contributed to it | Users distrust or ignore the score | Make score drill down to rule violations, coverage inputs, and run completeness |
| Hiding partial failures behind empty results | “No issues” is confused with “analysis failed” | Show explicit incomplete/failed states and required remediation |
| DSL authoring without schema validation and preview | Rule authors create broken or surprising rules | Validate live and offer preview on a sample solution before publish |
| Not showing target framework / environment used for a run | Users cannot reproduce findings | Surface TFM, SDK, and environment fingerprint in run details |
| Letting stale results look current | Users act on outdated quality status | Show freshness, last-run time, and whether repo/solution changed since the run |

## "Looks Done But Isn't" Checklist

Things that appear complete but are missing critical pieces.

- [ ] **Solution registration:** Often missing SDK/workload validation — verify the registered solution can be loaded with captured environment diagnostics.
- [ ] **Roslyn analysis:** Often missing generated code, `.editorconfig`, and target-framework handling — verify results match representative real projects, not just toy solutions.
- [ ] **Coverage:** Often missing invalid/incomplete states — verify failures never become 0% silently.
- [ ] **Dashboard score:** Often missing provenance — verify every score can be traced to exact findings, rule versions, and coverage status.
- [ ] **Custom rules:** Often missing schema versioning and migrations — verify old rules can still load after rule-kind evolution.
- [ ] **Execution safety:** Often missing worker isolation — verify build/test logic never runs in the web process.

## Recovery Strategies

When pitfalls occur despite prevention, how to recover.

| Pitfall | Recovery Cost | Recovery Steps |
|---------|---------------|----------------|
| Build-parity mismatch in Roslyn loading | HIGH | Mark affected project types/scenarios unsupported, capture diagnostics, add repro repos, separate project-loading fidelity work before expanding rule surface |
| Environment drift causing inconsistent findings | MEDIUM | Compare run fingerprints, pin SDK/MSBuild selection, rerun with captured environment, add missing validation rules |
| Unsafe execution boundary | HIGH | Immediately disable analysis workers, rotate credentials/service account, audit artifact/temp directories, tighten path policy, move execution out of the web process |
| Invalid coverage trusted as real coverage | MEDIUM | Invalidate affected runs, preserve raw artifacts, fix collection path, rerun with supported driver/settings, backfill score rules to require valid coverage |
| DSL schema drift | HIGH | Freeze rule editing, add schema migrations, backfill version fields, write one-time migration tool, restore round-trip tests |
| Performance collapse from cold recomputation | MEDIUM | Add stage timing, identify dominant step, introduce run locks/caching, defer expensive summaries, reduce rule fan-out until reuse is implemented |

## Pitfall-to-Phase Mapping

How roadmap phases should address these pitfalls.

| Pitfall | Prevention Phase | Verification |
|---------|------------------|--------------|
| Assuming `MSBuildWorkspace` means build parity | Phase 2 — Roslyn loading fidelity & evidence model | Representative solutions with generators/WPF/WinForms/multi-targeting load with explicit completeness status |
| Letting SDK/MSBuild environment drift change results | Phase 1 + Phase 2 | Every run stores and displays SDK/MSBuild/environment fingerprint; mismatched environments fail clearly |
| Treating build/test execution as harmless local analysis | Phase 1 — Secure execution boundary & project registration | Architecture proves worker isolation; path policy and run cleanup are covered by tests |
| Ignoring generated code/config/TFM context | Phase 2 + Phase 3 | Findings align with `.editorconfig`/generated-code expectations and preserve target-framework context |
| Treating coverage as simple percentage plumbing | Phase 4 — Coverage pipeline, scoring & dashboard trust | Broken/incomplete coverage runs are marked invalid, never silently summarized as 0% |
| Computing a score from partial or stale evidence | Phase 2 + Phase 4 | Score generation requires complete evidence and shows provenance/versioning |
| Building an invalid/unmaintainable custom-rule system | Phase 3 — Rule DSL & authoring UX | Rules validate against schemas, round-trip cleanly, and migrate across schema versions |
| Making every run a cold full-solution recomputation | Phase 5 — Performance hardening & operational safety | Stage timings, cancellation, per-solution locks, and reuse/caching measurably reduce run time |

## Sources

- Microsoft Learn — **Configure code analysis rules** (updated 2026-02-13) — HIGH  
  https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/configuration-options
- Microsoft Learn — **Customize Roslyn analyzer rules** (updated 2025-10-30) — HIGH  
  https://learn.microsoft.com/en-us/visualstudio/code-quality/use-roslyn-analyzers?view=visualstudio
- Microsoft Learn — **global.json overview** — HIGH  
  https://learn.microsoft.com/en-us/dotnet/core/tools/global-json
- Microsoft Learn — **.NET project SDK overview** — HIGH  
  https://learn.microsoft.com/en-us/dotnet/core/project-sdk/overview
- Microsoft Learn — **MSBuild tasks** — HIGH  
  https://learn.microsoft.com/en-us/visualstudio/msbuild/msbuild-tasks?view=visualstudio
- Microsoft Learn — **Exec task** (updated 2025-08-12) — HIGH  
  https://learn.microsoft.com/en-us/visualstudio/msbuild/exec-task?view=visualstudio
- Dustin Campbell — **Using MSBuildWorkspace** (Roslyn team gist) — MEDIUM  
  https://gist.github.com/DustinCampbell/32cd69d04ea1c08a16ae5c4cd21dd3a3
- dotnet/roslyn issue **#2779 MSBuildWorkspace and WPF Projects** — MEDIUM  
  https://github.com/dotnet/roslyn/issues/2779
- dotnet/roslyn issue **#62314 Loading net60 project into net472 host is not fully supported** — MEDIUM  
  https://github.com/dotnet/roslyn/issues/62314
- dotnet/roslyn issue **#71784 OpenProjectAsync failing in docker** — MEDIUM  
  https://github.com/dotnet/roslyn/issues/71784
- dotnet/roslyn issue **#23823 Slow Roslyn solution load time** — MEDIUM  
  https://github.com/dotnet/roslyn/issues/23823
- coverlet — **KnownIssues.md** — HIGH for tool-specific caveats  
  https://github.com/coverlet-coverage/coverlet/blob/master/Documentation/KnownIssues.md
- coverlet — **DeterministicBuild.md** (updated 2026-01-08) — HIGH for deterministic-build caveats  
  https://github.com/coverlet-coverage/coverlet/blob/master/Documentation/DeterministicBuild.md
- Semgrep — **Rule structure syntax** (updated 2026-04-15) — MEDIUM  
  https://semgrep.dev/docs/writing-rules/rule-syntax
- Semgrep — **Use the Semgrep rule schema in VS Code** (updated 2025-06-18) — MEDIUM  
  https://semgrep.dev/docs/kb/rules/using-semgrep-rule-schema-in-vscode
- Semgrep blog — **Structure Mode: Never write an invalid Semgrep rule again** (2024-04-30) — MEDIUM  
  https://semgrep.dev/blog/2024/structure-mode-never-write-an-invalid-semgrep-rule

---
*Pitfalls research for: CodePass / self-hosted .NET-native code quality platform*
*Researched: 2026-04-19*