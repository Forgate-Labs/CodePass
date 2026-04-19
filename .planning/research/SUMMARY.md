# Project Research Summary

**Project:** CodePass
**Domain:** Self-hosted .NET-native code quality platform for C#/.NET solutions
**Researched:** 2026-04-19
**Confidence:** MEDIUM

## Executive Summary

CodePass should be built as a **self-hosted, build-aware .NET code quality product**, not as a generic scanner or a CI-first governance platform. The research is consistent: credible tools in this space understand real `.sln` / `.csproj` boundaries, load projects with actual build context, normalize rule and coverage evidence into internal models, and run heavy analysis work outside the request/UI path. For CodePass, that points to a **.NET 10 modular monolith** using **Blazor Web App (Interactive Server)**, **EF Core + PostgreSQL**, an explicit **background worker boundary**, **Roslyn/MSBuild** for rule analysis, and **Cobertura-normalized coverage ingestion**.

The recommended v1 is intentionally narrow and opinionated: **register a local `.sln`**, **manually run rule analysis**, **inspect grouped violations with severity**, **manually run coverage**, **inspect class-level coverage**, and **show a current-state pass/fail dashboard**. The strategic differentiator is the **closed, schema-backed custom-rule DSL** with form-driven authoring and optional raw JSON editing. That is the part worth proving. CI automation, trend dashboards, multi-language support, AI rule authoring, and enterprise workflow features should stay out of the initial roadmap.

The biggest risk is **false confidence**. If Roslyn solution loading is partial, SDK/MSBuild resolution drifts by machine, or coverage is invalid but still summarized into a score, users will stop trusting the product. Mitigation is clear: isolate build/test execution in a least-privilege worker, capture an environment fingerprint for every run, persist raw diagnostics/artifacts, model `Incomplete`/`Unsupported` states explicitly, and only publish scores when the evidence bundle is complete and comparable.

## Key Findings

### Recommended Stack

The stack research strongly favors a **single .NET 10 codebase** over a split frontend/API/worker architecture. The product shape is an operator dashboard plus local analysis orchestration, so Blazor Interactive Server is a better fit than a SPA. Persistence should be relational-first with JSON support, and job execution should stay simple until scheduling or distributed work is actually needed.

**Core technologies:**
- **.NET 10 LTS + ASP.NET Core + Blazor Web App (Interactive Server):** single .NET-native host for UI, backend, auth, and orchestration.
- **MudBlazor 9.3.0:** fast path for dense admin/reporting UI.
- **EF Core 10.0.6 + PostgreSQL 17.x + Npgsql 10.0.1:** durable relational core with JSONB support for rule payloads and run metadata.
- **`BackgroundService` + bounded `Channel<T>`:** enough for manual analysis/coverage jobs without Hangfire or Quartz overhead.
- **Roslyn Workspaces 5.3.0 + Microsoft.Build.Locator 1.11.2:** correct foundation for solution-aware, semantic .NET analysis.
- **`System.Text.Json.Schema` + JsonSchema.Net 9.2.0:** generate and validate versioned rule schemas for the DSL.
- **Cobertura as canonical coverage format + `coverlet.collector`/`dotnet-coverage`:** practical coverage interchange for normalization and class-level reporting.
- **Native hosting first, containers optional:** Windows service/systemd should be default; Docker is secondary because local filesystem and SDK visibility matter.

**Critical version requirements:**
- Stay aligned on the **.NET 10 / EF Core 10** line.
- Register **MSBuildLocator before any MSBuild API** usage.
- Treat **Cobertura** as the internal canonical format even if acquisition strategies vary.
- Do not assume one coverage driver fits both **VSTest** and **Microsoft Testing Platform** workflows.

### Expected Features

The feature research is clear: users will not see CodePass as credible unless it handles real solution analysis, actionable findings, coverage, and pass/fail clarity. What makes CodePass interesting is not “more rules,” but **making custom .NET rules practical for normal teams**.

**Must have (table stakes):**
- **Solution-aware registration of `.sln` / `.csproj`:** users expect build-aware analysis, not file scanning.
- **Manual rule analysis with actionable violations:** grouped by rule, severity, project, namespace, file, and class.
- **Rule/profile configuration:** enable/disable, severity tuning, thresholds.
- **Coverage ingestion with class-level visibility:** enough for a credible code-quality story.
- **Project score / quality gate / pass-fail summary:** current-state clarity is expected.

**Should have (competitive):**
- **Structured custom-rule DSL with schema-driven forms:** the main product wedge.
- **Roslyn-semantic rule kinds for .NET conventions and architecture:** naming, attributes, layering, inheritance, async/test conventions.
- **Frictionless self-hosted local-path workflow:** simpler than CI-heavy competitor flows.
- **Opinionated current-pass dashboard:** optimized for “what is broken now?” not management reporting.
- **First-class rule authoring UX:** validation, previews, examples, and versionable JSON are part of the product.

**Defer (v2+):**
- **CI/PR/branch automation**
- **Execution history and trend dashboards**
- **Enterprise governance / multi-tenant workflow features**
- **Multi-language support**
- **SAST/SCA/secrets/license scanning, AI authoring, or auto-fix**

### Architecture Approach

Architecture research points to a **modular monolith with an explicit execution boundary**. Keep one product, one solution, and one database in v1, but make heavy analysis work live behind a queue/worker interface from day one. The UI should stay thin, the application layer should own commands/queries and transactions, and the worker should create **per-run analysis sessions** for Roslyn and coverage work. External tool outputs must be **normalized into stable internal models** before they affect dashboards or scores.

**Major components:**
1. **`CodePass.Web`** — Blazor UI for project registration, rule authoring, manual runs, and reporting.
2. **Application layer** — commands/queries, run orchestration, validation, and transaction boundaries.
3. **Solution Registry + Rule Catalog** — registered solution metadata, rule kinds, schemas, and versioned rule definitions.
4. **Run Orchestrator + Job Queue/Worker** — durable run records, queueing, concurrency control, and isolated execution.
5. **Roslyn Analysis Engine** — solution loading, semantic analysis, typed rule executors, normalized violations.
6. **Coverage Pipeline** — acquisition/import, parsing, mapping, aggregation, and artifact retention.
7. **Reporting / Score Service** — read models, grouped findings, coverage summaries, and validated pass/fail output.
8. **Infrastructure adapters** — EF Core, filesystem, process execution, logging, clock, and environment detection.

### Critical Pitfalls

1. **Assuming `MSBuildWorkspace` equals real build parity** — treat solution loading as a fidelity problem, capture diagnostics, support a clear project-type matrix, and never score partially loaded projects as if they were complete.
2. **Letting SDK/MSBuild environment drift change results** — persist a run fingerprint with SDK, `global.json`, MSBuild instance, TFMs, OS, and host details; fail fast when prerequisites are missing.
3. **Running build/test/coverage work like harmless local logic** — never execute analysis inside the Blazor process; use a least-privilege worker, path allowlists, per-run temp directories, and explicit cleanup.
4. **Treating coverage and scoring as simple percentages** — keep raw artifacts, model `Invalid`/`Incomplete` states, and only publish a score from complete, versioned evidence.
5. **Letting the custom-rule DSL sprawl into unstructured JSON or scripting** — keep the catalog closed, schema-version every rule kind, validate on the backend, and add migration infrastructure early.

## Implications for Roadmap

Based on research, suggested phase structure:

### Phase 1: Secure Foundation & Solution Registration
**Rationale:** Everything depends on trustworthy solution registration, durable run tracking, and safe execution boundaries. If this is wrong, the rest of the roadmap sits on sand.
**Delivers:** PostgreSQL/EF persistence, registered solutions, path normalization and allowed-root checks, run records, queue abstraction, in-process worker boundary, logging/telemetry, artifact directories, and basic dashboard shell.
**Addresses:** local `.sln` registration, manual-run workflow foundation, self-hosted local-path experience.
**Avoids:** unsafe execution in the web process, unrestricted path registration, and hidden environment drift.

### Phase 2: Roslyn Loading Fidelity & Evidence Model
**Rationale:** Do not build polished rule UX until CodePass can load representative real-world solutions reliably and explain what loaded versus what did not.
**Delivers:** `MSBuildLocator` + `MSBuildWorkspace` integration, workspace diagnostics capture, project load completeness states (`Loaded`, `PartiallyLoaded`, `Unsupported`, `Failed`), environment fingerprints, target framework visibility, and representative fixture solutions.
**Addresses:** solution-aware analysis credibility and build-aware behavior users expect.
**Uses:** Roslyn workspace stack, worker boundary, environment metadata, normalized run model.
**Avoids:** false build parity assumptions, generated-code/TFM blindness, and untrustworthy downstream scoring.

### Phase 3: Rule Engine & Custom-Rule MVP
**Rationale:** This is the product’s main differentiator and should be the first full end-to-end value slice once solution loading is proven.
**Delivers:** closed rule catalog, schema registry, JSON-backed rule persistence, schema-driven editor with optional raw JSON, a small set of high-value rule kinds, manual rule runs, normalized violations, and grouped violation review UI.
**Addresses:** actionable violations, rule/profile configuration, and the core custom-rule authoring promise.
**Avoids:** arbitrary code execution, DSL sprawl, and analysis logic leaking into UI components.

### Phase 4: Coverage Pipeline & Trustworthy Score Dashboard
**Rationale:** The dashboard only becomes meaningful when rule evidence and coverage evidence are both valid, explainable, and normalized.
**Delivers:** Cobertura import/normalization, one supported acquisition strategy, class-level coverage views, score calculation from a validated evidence bundle, explicit incomplete/invalid states, and current-state pass/fail reporting.
**Addresses:** class-level coverage visibility and project score/quality gate expectations.
**Uses:** coverage acquisition boundary, parser/mapper pipeline, reporting projections, severity-weighted scoring.
**Avoids:** turning broken coverage into `0%`, showing a clean score from stale/partial evidence, and coupling coverage to one fragile test command.

### Phase 5: Adoption Hardening — Baselines, Performance, Interop
**Rationale:** Once the core loop works, the next barrier is real-world adoption on legacy solutions and larger repos.
**Delivers:** stable issue fingerprinting, baseline/suppression or “new problems only” mode, generated-code/noise controls, per-solution run locks, stage timing, performance instrumentation, artifact retention policy, more rule kinds, and selective Roslyn ecosystem interoperability.
**Addresses:** legacy-noise control, broader rule coverage, and interoperability expectations.
**Avoids:** full-solution cold recomputation on every run, noisy findings that block adoption, and scaling pain as rule count and repo size grow.

### Phase 6: Post-Validation Expansion (v2+)
**Rationale:** Only pursue these once the manual local workflow is clearly valuable and trustworthy.
**Delivers:** CI/PR integration, scheduled analysis, history/trends, richer team governance, and optional broader ecosystem integrations.
**Addresses:** deferred enterprise and workflow asks.
**Avoids:** bloating v1 with infrastructure-heavy features before the core product is validated.

### Phase Ordering Rationale

- **Security and execution isolation come before analysis depth** because build/test execution is the highest-risk boundary in the product.
- **Roslyn fidelity comes before rule-authoring polish** because a great UI on top of unreliable project loading creates fake confidence.
- **Rule analysis precedes coverage scoring** because coverage and pass/fail logic only matter after normalized violation models exist.
- **Coverage normalization must precede score publication** so the dashboard reflects validated evidence instead of loosely correlated numbers.
- **Baseline/noise control follows MVP** because it depends on stable issue fingerprints and evidence models, but should come quickly after launch for real adoption.
- **CI/history/enterprise features are explicitly deferred** because they add workflow complexity without proving the product wedge.

### Research Flags

Phases likely needing deeper research during planning:
- **Phase 2:** highest technical risk; needs validation on representative repos covering source generators, multi-targeting, WPF/WinForms, `Directory.Build.*`, and `global.json` cases.
- **Phase 4:** coverage tooling is fragmented across VSTest and Microsoft Testing Platform; mapping reliability and invalid/incomplete states need focused design.
- **Phase 5:** stable issue fingerprinting, baseline semantics, and analyzer interoperability are easy to get subtly wrong and deserve targeted follow-up research.

Phases with standard patterns (skip research-phase):
- **Phase 1:** Blazor + EF Core + PostgreSQL + hosted worker + native service patterns are well documented and low ambiguity.
- **Most of Phase 3 infrastructure:** schema validation, JSON persistence, queue-backed run flow, and grouped result UIs are standard; only new rule kinds need targeted research.

## Confidence Assessment

| Area | Confidence | Notes |
|------|------------|-------|
| Stack | HIGH | Strongly grounded in current official .NET 10, ASP.NET Core, EF Core, hosted-service, coverage, and package documentation. |
| Features | MEDIUM | Clear competitor pattern match, but market expectations are inferred from product docs rather than direct user interviews. |
| Architecture | MEDIUM | Sound and internally consistent, but still partly prescriptive because CodePass is greenfield and unvalidated on real repositories. |
| Pitfalls | MEDIUM | High-value warnings backed by official docs and ecosystem issues, but several risks still need confirmation against real target solutions. |

**Overall confidence:** MEDIUM

### Gaps to Address

- **Representative repository test matrix:** validate the supported v1 project matrix early using real repos, not toy samples.
- **Coverage acquisition choice:** decide the exact v1 path (import-first, VSTest collector, or MTP-specific strategy) before exposing coverage as a trusted feature.
- **Stable issue fingerprinting model:** define how violations are identified across runs before building baselines or trend/history features.
- **Scoring formula semantics:** version the formula and specify what blocks score publication when evidence is incomplete.
- **Native-hosting operational envelope:** document the required SDK/workload/toolchain assumptions for Windows service, systemd, and optional container installs.

## Sources

### Primary (HIGH confidence)
- Microsoft Learn — .NET 10 overview — runtime/LTS baseline
- Microsoft Learn — Blazor / ASP.NET Core 10 docs — Blazor Web App + Interactive Server model
- Microsoft Learn — EF Core 10 what's new — EF 10 LTS alignment
- Microsoft Learn — hosted services / queue-service / channels — background worker patterns
- Microsoft Learn — MSBuild locator + MSBuild API guidance — solution loading setup
- Roslyn official overview/docs — workspace model and analyzer foundations
- Microsoft Learn — unit testing code coverage / Microsoft Testing Platform coverage — coverage collection and format guidance
- Microsoft Learn — `global.json`, project SDK, analyzer configuration docs — environment and rule-config behavior
- Npgsql docs/release notes — PostgreSQL provider compatibility and JSON support

### Secondary (MEDIUM confidence)
- SonarQube docs — quality gates, coverage import, external analyzer expectations
- Qodana docs — .NET and coverage workflow expectations
- NDepend docs — custom rule, baseline, coverage, and trend feature patterns
- Coverlet documentation / known issues — real-world coverage caveats
- Roslyn issue tracker items — project loading edge cases, Docker/host/runtime drift, performance pain points
- Semgrep rule schema docs/blog — useful inspiration for structured rule validation and authoring UX

### Tertiary (LOW confidence)
- None material; the main uncertainty is not source quality but unvalidated behavior on real target solutions.

---
*Research completed: 2026-04-19*
*Ready for roadmap: yes*
