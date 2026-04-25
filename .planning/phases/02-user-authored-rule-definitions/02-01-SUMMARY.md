---
phase: 02-user-authored-rule-definitions
plan: 01
subsystem: api
tags: [dotnet, ef-core, sqlite, rules, blazor, xunit]
requires:
  - phase: 01-registered-solutions
    provides: SQLite-backed app startup, EF Core patterns, and in-memory service test fixtures
provides:
  - Persisted authored rule definitions with stable DSL metadata and raw JSON payload storage
  - Closed rule-kind catalog metadata for schema-driven rule authoring
  - Authored-only rule CRUD and active-rule queries with validation and regression tests
affects: [02-02, 02-03, rule-authoring-ui, rule-analysis]
tech-stack:
  added: []
  patterns: [Closed in-code rule catalog, SQLite-friendly string JSON persistence, authored-only active-rule service boundary]
key-files:
  created:
    - src/CodePass.Web/Data/Entities/AuthoredRuleDefinition.cs
    - src/CodePass.Web/Data/Entities/RuleSeverity.cs
    - src/CodePass.Web/Services/Rules/IRuleCatalogService.cs
    - src/CodePass.Web/Services/Rules/IRuleDefinitionService.cs
    - src/CodePass.Web/Services/Rules/RuleAuthoringModels.cs
    - src/CodePass.Web/Services/Rules/RuleCatalogService.cs
    - src/CodePass.Web/Services/Rules/RuleDefinitionService.cs
    - tests/CodePass.Web.Tests/Services/RuleCatalogServiceTests.cs
    - tests/CodePass.Web.Tests/Services/RuleDefinitionServiceTests.cs
  modified:
    - src/CodePass.Web/Data/CodePassDbContext.cs
    - src/CodePass.Web/Program.cs
key-decisions:
  - "Keep rule-kind metadata in a closed in-code catalog so the UI can render supported schemas without turning catalog entries into active rules."
  - "Persist scope, parameters, and raw DSL documents as normalized JSON strings to stay SQLite-friendly and deterministic in tests."
patterns-established:
  - "Rule authoring services are split between catalog metadata (IRuleCatalogService) and persisted authored rules (IRuleDefinitionService)."
  - "Active-rule reads come only from enabled AuthoredRuleDefinitions records and never from shipped fallback packs."
requirements-completed: [RULE-07]
duration: 8 min
completed: 2026-04-25
---

# Phase 2 Plan 1: User-authored rule backend foundation Summary

**SQLite-backed authored rule persistence, closed rule-kind schema metadata, and authored-only active-rule services for Phase 2.**

## Performance

- **Duration:** 8 min
- **Started:** 2026-04-25T01:27:31Z
- **Completed:** 2026-04-25T01:31:24Z
- **Tasks:** 3
- **Files modified:** 11

## Accomplishments
- Added persisted authored rule storage with stable metadata, severity, enabled state, and raw DSL JSON.
- Introduced a closed rule catalog plus CRUD/query services that validate kind, schema version, and JSON payload shape before save.
- Added regression tests proving catalog behavior, DI registration, JSON normalization, and authored-only active-rule queries.

## Task Commits

Each task was committed atomically:

1. **Task 1: Add persisted user-authored rule definition storage** - `5ac8adc` (feat)
2. **Task 2: Implement the closed rule catalog and authored-rule service layer** - `ca4c8c5` (feat)
3. **Task 3: Add backend tests for authored-only rule exposure and catalog validation** - `ea97791` (test)

## Files Created/Modified
- `src/CodePass.Web/Data/Entities/AuthoredRuleDefinition.cs` - EF entity for persisted user-authored rule DSL documents.
- `src/CodePass.Web/Data/Entities/RuleSeverity.cs` - Severity enum stored as strings in SQLite.
- `src/CodePass.Web/Data/CodePassDbContext.cs` - DbSet and EF mapping for authored rule definitions.
- `src/CodePass.Web/Services/Rules/RuleAuthoringModels.cs` - Shared catalog, request, and DTO models for rule authoring.
- `src/CodePass.Web/Services/Rules/RuleCatalogService.cs` - Closed supported rule-kind catalog for schema-driven UI authoring.
- `src/CodePass.Web/Services/Rules/RuleDefinitionService.cs` - Validation, normalization, CRUD, and authored-only active-rule queries.
- `src/CodePass.Web/Program.cs` - DI registration for rule catalog and authored rule services.
- `tests/CodePass.Web.Tests/Services/RuleCatalogServiceTests.cs` - Catalog coverage for supported kinds and unknown kinds.
- `tests/CodePass.Web.Tests/Services/RuleDefinitionServiceTests.cs` - Service coverage for normalization, validation, DI resolution, and active-rule reads.

## Decisions Made
- Kept engine-supported rule kinds in a closed catalog service so Phase 2 UI work can query schema metadata without treating those entries as active rules.
- Normalized scope, parameters, and raw DSL JSON before persistence to keep SQLite storage provider-agnostic and test output deterministic.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Replaced collection expressions that prevented catalog defaults from compiling**
- **Found during:** Task 2 (Implement the closed rule catalog and authored-rule service layer)
- **Issue:** The initial catalog defaults used collection expressions in a generic serialization call that the compiler could not infer.
- **Fix:** Replaced those defaults with explicit `new[] { ... }` arrays so the catalog compiles cleanly.
- **Files modified:** `src/CodePass.Web/Services/Rules/RuleCatalogService.cs`
- **Verification:** `dotnet build CodePass.sln`
- **Committed in:** `ca4c8c5` (part of task commit)

---

**Total deviations:** 1 auto-fixed (1 blocking)
**Impact on plan:** No scope creep. The fix only removed a compilation blocker in the planned catalog implementation.

## Issues Encountered
None.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- `/rules` UI work can now load closed schema metadata and persist authored rule definitions against stable backend contracts.
- Active-rule reads are constrained to enabled authored records, so later rule-analysis work has a safe authored-only source.

---
*Phase: 02-user-authored-rule-definitions*
*Completed: 2026-04-25*

## Self-Check: PASSED
