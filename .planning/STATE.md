---
gsd_state_version: 1.0
milestone: v1.0
milestone_name: milestone
status: planning
stopped_at: Completed 04-coverage-analysis-review-02-PLAN.md
last_updated: "2026-04-27T18:52:00.266Z"
last_activity: 2026-04-27 — Completed Phase 4 coverage persistence schema with EF entities, additive SQLite initialization, and initializer regression tests.
progress:
  total_phases: 5
  completed_phases: 3
  total_plans: 18
  completed_plans: 14
  percent: 78
---

# Project State

## Project Reference

See: .planning/PROJECT.md (updated 2026-04-19)

**Core value:** Make it easy to run lightweight, deeply .NET-native code analysis locally and define custom rules without fighting a generic, infrastructure-heavy platform.
**Current focus:** Phase 4 - Coverage Analysis Review

## Current Position

Phase: 4 of 5 (Coverage Analysis Review)
Plan: 03 of 6 (next: persist normalized coverage results and latest-run retrieval)
Status: Phase 4 in progress; ready for Plan 04-03
Last activity: 2026-04-27 — Completed Phase 4 coverage persistence schema with EF entities, additive SQLite initialization, and initializer regression tests.

Progress: [████████░░] 78%

## Performance Metrics

**Velocity:**
- Total plans completed: 14
- Average duration: 17.4 min
- Total execution time: 4.1 hours

**By Phase:**

| Phase | Plans | Total | Avg/Plan |
|-------|-------|-------|----------|
| 01-registered-solutions | 3 | 61 min | 20.3 min |
| 02-user-authored-rule-definitions | 3 | 125 min | 41.7 min |
| 03-rule-analysis-review | 6 | 49 min | 8.2 min |
| 04-coverage-analysis-review | 2 | 6 min | 3.0 min |

**Recent Trend:**
- Last 5 plans: 03-rule-analysis-review-04 (2 min), 03-rule-analysis-review-05 (3 min), 03-rule-analysis-review-06 (33 min), 04-coverage-analysis-review-01 (3 min), 04-coverage-analysis-review-02 (3 min)
- Trend: Phase 4 started with focused backend coverage-analysis plans and fast automated verification.
| Phase 01-registered-solutions P01 | 31 min | 3 tasks | 28 files |
| Phase 01 P02 | 10 min | 3 tasks | 9 files |
| Phase 01-registered-solutions P03 | 20 min | 3 tasks | 7 files |
| Phase 02 P01 | 8 min | 3 tasks | 11 files |
| Phase 02 P02 | 22 min | 3 tasks | 7 files |
| Phase 02-user-authored-rule-definitions P03 | 1h 35m | 3 tasks | 9 files |
| Phase 03-rule-analysis-review P01 | 3 min | 3 tasks | 10 files |
| Phase 03-rule-analysis-review P02 | 5 min | 3 tasks | 5 files |
| Phase 03-rule-analysis-review P03 | 3 min | 3 tasks | 11 files |
| Phase 03-rule-analysis-review P04 | 2 min | 3 tasks | 4 files |
| Phase 03-rule-analysis-review P05 | 3 min | 3 tasks | 7 files |
| Phase 03-rule-analysis-review P06 | 33 min | 2 tasks | 8 files |
| Phase 04-coverage-analysis-review P01 | 3 min | 3 tasks | 5 files |
| Phase 04-coverage-analysis-review P02 | 3 min | 3 tasks | 7 files |

## Accumulated Context

### Decisions

Decisions are logged in PROJECT.md Key Decisions table.
Recent decisions affecting current work:

- [Phase 1]: v1 stays self-hosted around local `.sln` registration and analysis.
- [Phase 2]: Custom rules use a structured DSL with schema-driven authoring and optional raw JSON editing.
- [Phase 2]: v1 ships no built-in production rule packs; active rules in the product are user-authored only.
- [Phase 01-registered-solutions]: Persist registered solutions in SQLite and initialize the local database at app startup with EnsureCreated for the first vertical slice.
- [Phase 01-registered-solutions]: Restrict save-time registration to canonical direct .sln file paths and allow later refresh operations to downgrade saved status when the filesystem changes.
- [Phase 01-registered-solutions]: Refresh saved solution statuses in a background service so later UI work can display current filesystem health without rescanning folders.
- [Phase 01]: The registered-solutions screen refreshes persisted statuses before loading cards so the UI reflects current filesystem health. — This keeps the card UI aligned with the background refresh model from plan 01-01 and avoids showing stale save-time statuses.
- [Phase 01]: The picker flow uses the File System Access API when available and falls back to a .sln-restricted file input while keeping the path editable for the real absolute path. — Browsers often hide absolute filesystem paths, so the editable field preserves the picker-first UX without violating the requirement to store a real local .sln path.
- [Phase 01]: The card surface shows status details and a non-destructive disabled Manage placeholder, preserving the later edit/remove modal decision. — Phase 01-03 owns the edit/remove workflow, so the page exposes the future affordance without moving destructive actions onto the card surface.
- [Phase 01-registered-solutions]: Registered solution maintenance stays in a dedicated modal so cards remain non-destructive surfaces. — Keeps the registered-solutions cards focused on status visibility while the modal owns edit and delete affordances.
- [Phase 01-registered-solutions]: Solution path edits revalidate only when the path value changes, preserving saved metadata updates without unnecessary filesystem checks. — Display-name-only edits should not force redundant validation, but changed paths must still be rechecked before persistence.
- [Phase 01-registered-solutions]: The full /solutions flow was accepted through a human verification checkpoint after a follow-up bug-fix clarified the picker-assisted add experience. — This records the approved checkpoint outcome and the small UX fix needed to satisfy live verification.
- [Phase 02]: Keep rule-kind metadata in a closed in-code catalog so the UI can render supported schemas without turning catalog entries into active rules. — Separates engine-supported authoring metadata from persisted authored rules and preserves the authored-only v1 constraint.
- [Phase 02]: Persist scope, parameters, and raw DSL documents as normalized JSON strings to stay SQLite-friendly and deterministic in tests. — String-backed JSON keeps storage provider-agnostic for SQLite while giving the UI and later execution phases stable serialized payloads.
- [Phase 02]: Keep the /rules screen authored-rule-only and use the catalog exclusively as editor metadata so supported kinds never appear as pseudo-rules. — This preserves the v1 constraint that active rules are always user-authored while still letting the editor use closed catalog metadata.
- [Phase 02]: Represent schema array fields as newline-based string-list inputs so catalog-backed defaults stay editable with standard Bootstrap controls. — This keeps the UI simple, avoids a new component library, and still supports editable list defaults for rule kinds.
- [Phase 02-user-authored-rule-definitions]: Keep raw JSON editing inside the existing authored-rule editor so schema mode and direct DSL mode operate on one persisted user-authored rule document. — Avoids a separate editor surface or external editor dependency while preserving reopen/edit behavior.
- [Phase 02-user-authored-rule-definitions]: Use RuleDefinitionService validation as the source of truth for raw JSON saves. — Ensures UI and persistence paths enforce the same supported kind/schema and required DSL fields.
- [Phase 02-user-authored-rule-definitions]: Initialize missing authored-rule tables at startup for existing local SQLite databases. — Keeps local self-hosted developer databases working without manual deletion or setup.
- [Phase 03-rule-analysis-review]: Persist per-solution rule applicability as explicit SolutionRuleAssignment rows instead of inferring active rules from global authored-rule state.
- [Phase 03-rule-analysis-review]: Require both the authored rule's global IsEnabled flag and the per-solution assignment IsEnabled flag before a rule is returned for analysis.
- [Phase 03-rule-analysis-review]: Keep SQLite startup initialization additive and idempotent so existing local databases gain assignment support without manual deletion.
- [Phase 03-rule-analysis-review]: Execute only the AuthoredRuleDefinitionDto instances passed to the analyzer; unknown rule kinds are skipped so catalog evolution does not activate built-in rules or crash existing runs. — Preserves the v1 authored-rule-only constraint while allowing the closed engine catalog to evolve safely.
- [Phase 03-rule-analysis-review]: Use MSBuildWorkspace with guarded MSBuildLocator.RegisterDefaults() for local solution loading, while excluding MSBuild framework runtime assets to avoid assembly-loading conflicts. — The analyzer must load real local .sln files reliably from the self-hosted process without copying conflicting MSBuild assemblies.
- [Phase 03-rule-analysis-review]: Return analyzer locations as solution-relative paths plus 1-based line and column spans from Roslyn Location data. — Persisted results and UI views need actionable, user-readable code positions independent of absolute machine paths.
- [Phase 03-rule-analysis-review]: Persist rule-analysis violations as snapshots of authored rule identity and source location so results remain reviewable even if rules change later.
- [Phase 03-rule-analysis-review]: Keep AuthoredRuleDefinitionId nullable on violations and use ON DELETE SET NULL so authored-rule deletion does not destroy historical finding details.
- [Phase 03-rule-analysis-review]: Keep RuleAnalysisResultService persistence-focused; orchestration and Roslyn execution remain separate concerns for Plan 03-04.
- [Phase 03-rule-analysis-review]: Manual rule-analysis execution is exposed through a scoped IRuleAnalysisRunService so UI callers do not compose selection, analyzer, and result persistence services directly. — Plan 03-05 needs a single backend operation for the /analysis/rules UI, and this keeps orchestration separate from persistence and Roslyn execution concerns.
- [Phase 03-rule-analysis-review]: Valid registered solutions with no enabled authored rules complete as succeeded zero-rule runs without invoking the analyzer. — Empty per-solution rule selections are a legitimate user state and should leave durable successful run records instead of surfacing analyzer errors.
- [Phase 03-rule-analysis-review]: Keep rule-analysis selection, manual runs, and latest results together on `/analysis/rules` so users can review one selected solution without navigating between pages.
- [Phase 03-rule-analysis-review]: Render rule applicability exclusively from `ISolutionRuleSelectionService` results so catalog metadata never appears as selectable production rules.
- [Phase 03-rule-analysis-review]: Use Bootstrap-native cards, badges, switches, and alerts with stable `data-testid` selectors for maintainable Blazor component tests.
- [Phase 03-rule-analysis-review]: Use the approved running-app browser workflow with a real user-authored raw JSON rule as the final Phase 3 acceptance signal.
- [Phase 03-rule-analysis-review]: Keep final verification fixes narrow to rule-analysis SQLite translation, selected-solution display, and local Blazor static asset loading.
- [Phase 04-coverage-analysis-review]: Keep coverage normalization isolated in CoberturaCoverageParser so persistence and UI work can consume normalized DTOs without shelling out to dotnet.
- [Phase 04-coverage-analysis-review]: Keep DotNetCoverageAnalyzer responsible only for process execution, generated coverage-file discovery, readable failures, and temp-directory cleanup.
- [Phase 04-coverage-analysis-review]: Represent coverage engine outputs as immutable records that are not tied to EF persistence entities.
- [Phase 04-coverage-analysis-review]: Use double-backed coverage percentages so SQLite stores coverage rates as REAL values and avoids decimal translation limitations. — SQLite-backed coverage data will be queried by later services, and REAL-backed percentages avoid provider limitations while preserving enough precision for UI reporting.
- [Phase 04-coverage-analysis-review]: Add both separate and composite coverage-run lookup indexes so latest-run queries can filter by registered solution and order by start time efficiently. — Coverage result retrieval in later plans will need selected-solution latest-run lookups; the composite index supports that query shape while separate indexes satisfy direct lookup requirements.

### Pending Todos

None yet.

### Blockers/Concerns

None yet.

## Session Continuity

Last session: 2026-04-27T18:52:00.264Z
Stopped at: Completed 04-coverage-analysis-review-02-PLAN.md
Resume file: None
