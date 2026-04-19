# CodePass

## What This Is

CodePass is a self-hosted .NET 10 code quality platform built specifically for C# and the broader .NET ecosystem. It helps users register a solution by its `.sln` path, run focused code analysis and coverage analysis, and inspect clear results through a Blazor Server management UI. Its main differentiator is making highly customizable analysis rules practical through a structured DSL backed by Roslyn.

## Core Value

Make it easy to run lightweight, deeply .NET-native code analysis locally and define custom rules without fighting a generic, infrastructure-heavy platform.

## Requirements

### Validated

(None yet — ship to validate)

### Active

- [ ] Users can register a .NET solution by providing the local path to a `.sln` file.
- [ ] Users can manually trigger rule analysis for a registered solution and inspect violations grouped by rule with severity.
- [ ] Users can manually trigger coverage analysis for a registered solution and inspect unit test coverage by class.
- [ ] Admin users can create and edit custom analysis rules through a structured DSL model backed by schemas.
- [ ] The web UI shows a project score/dashboard that makes it obvious whether the code currently “passes”.

### Out of Scope

- GitHub and Azure DevOps integration — defer until the core local/self-hosted workflow is proven.
- Automatic scheduling or pipeline-triggered analysis — manual execution is enough for v1 validation.
- Execution history and trend analysis — current pass/fail clarity matters more than historical reporting in v1.
- Branch and pull request comparisons — not needed before the base analysis experience is solid.
- Support beyond C#/.NET — the product value comes from being tightly focused on .NET first.
- Multi-tenant teams, advanced permissions, and robust auth — unnecessary complexity for the initial self-use and early-user phase.

## Context

The project is motivated by frustration with existing general-purpose tools such as SonarQube: they are powerful, but feel generic, infrastructure-heavy, and not deeply tailored to the .NET experience. CodePass should feel native to the .NET ecosystem, be easy to run locally for free, and stay closely aligned with Roslyn rather than abstracting away from it.

The envisioned product has a Blazor Server frontend for management and reporting. Users add projects by supplying a `.sln` path on a machine where the self-hosted CodePass instance already has filesystem access. In v1, rule analysis and coverage analysis are separate manual actions.

Custom rules are the product’s strategic differentiator. The current preferred approach is a structured DSL rather than free-form natural language: a closed catalog of rule kinds, schemas per rule kind, JSON-backed rule instances, and Roslyn-based executors. The frontend should help authors assemble rules from schemas while also allowing direct JSON editing when needed.

The first user is primarily the project creator, but the product should also be credible for internal teams, small and medium .NET teams, and tech leads or architects later. The project also serves as proof that a focused .NET-first platform can solve a real problem with a more opinionated and efficient design.

The dashboard must make the current quality state understandable at a glance. The minimum valuable outputs are: a score for the project, a list of violations by rule with severity defined by each rule, and coverage by class.

All implementation code must be written in English.

## Constraints

- **Tech stack**: .NET 10 with Blazor Server — chosen to keep the full product deeply aligned with the .NET ecosystem.
- **Execution model**: Self-hosted with local filesystem access to the target `.sln` path — because v1 is optimized for easy local use.
- **Analysis engine**: Roslyn-centered rule execution — because the product should be intimately tied to .NET semantics, not text matching.
- **Rule authoring model**: Structured DSL with schemas and a closed rule catalog — because custom rules must be easy to validate, evolve, and render in the UI.
- **Scope**: C#/.NET only in v1 — because focus is a competitive advantage, not a limitation.
- **Workflow**: Separate manual runs for rule analysis and coverage analysis — because automation can wait until the core experience is validated.

## Key Decisions

| Decision | Rationale | Outcome |
|----------|-----------|---------|
| Build CodePass as a .NET-native alternative rather than a multi-language scanner | Focus on a stronger opinionated experience for C#/.NET users | — Pending |
| Use Roslyn as the analysis foundation | Roslyn provides syntax and semantic analysis needed for precise rules | — Pending |
| Use a structured DSL for custom rules instead of free-form text | Structured rules are easier to validate, version, execute, and drive from the UI | — Pending |
| Use schema-driven rule forms with optional raw JSON editing | The UI should help authors, but advanced users still need direct control | — Pending |
| Start with self-hosted local-path analysis of `.sln` files | This is the simplest way to deliver value quickly with low infrastructure overhead | — Pending |
| Keep rule analysis and coverage analysis as separate manual actions in v1 | It reduces complexity while preserving the two main user outcomes | — Pending |
| Keep all implementation code in English | Consistency and maintainability across the codebase | — Pending |

---
*Last updated: 2026-04-19 after initialization*
