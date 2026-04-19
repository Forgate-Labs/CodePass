# Feature Research

**Domain:** Self-hosted .NET-native code quality platform for C#/.NET solutions
**Researched:** 2026-04-19
**Confidence:** MEDIUM

## Feature Landscape

**Bottom line:** in this domain, users now expect five things as baseline: build-aware solution analysis, actionable rule violations, coverage visibility, configurable pass/fail standards, and some way to control legacy-code noise. The strongest differentiator is **not** “more rules”; it is **making .NET-specific custom rules practical for normal teams**, not just experts writing plugins or query languages.

### Table Stakes (Users Expect These)

Features users assume exist. Missing these = product feels incomplete.

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| Solution-aware analysis target (`.sln` / `.csproj`) | Qodana, SonarScanner for .NET, and other .NET analyzers are build-aware. Users expect the platform to understand solution boundaries, target frameworks, restore/build context, and test projects. | MEDIUM | CodePass should treat `.sln` registration as the primary entry point and surface restore/build failures clearly. |
| Rule analysis with actionable violations | Every serious platform shows rule violations with rule metadata, severity, and source locations. Without this, it is just a log viewer. | MEDIUM | Must support grouping by rule, severity, project, namespace, file, and class. Rule detail needs explanation + affected locations. |
| Rule/profile configuration | Sonar uses quality profiles, Qodana uses inspection profiles, NDepend ships default rules plus customization. Users expect enable/disable, severity tuning, and thresholds. | MEDIUM | Even if CodePass has a small default catalog, users need project-level control over which rules matter. |
| Coverage ingestion and class-level visibility | Sonar imports .NET coverage reports, Qodana exposes method/class/file coverage, and NDepend couples coverage with rules/gates. Coverage is part of the code-quality conversation, not a separate afterthought. | MEDIUM | v1 class-level coverage is enough. Internally, design for report import from common .NET tools like Coverlet/OpenCover/dotCover-style outputs. |
| Project score / quality gate / pass-fail summary | Sonar quality gates, Qodana failure conditions, and NDepend PASS/WARN/FAIL all show users whether the project is acceptable now. | MEDIUM | CodePass’s dashboard score is table stakes, not a novelty. The novelty is making it simpler and more .NET-focused. |
| Legacy-noise control (baseline, suppressions, or “new problems only”) | Existing tools all provide some mechanism to avoid overwhelming teams with old debt: Sonar focuses on new code, Qodana supports baselines, NDepend compares against baselines. | HIGH | CodePass does not need full historical analytics in v1, but it will need at least a lightweight baseline or suppression model soon after launch. Without it, adoption on existing codebases will be painful. |
| Roslyn ecosystem interoperability | In .NET, users already have Microsoft analyzers, StyleCop, Meziantou, Roslynator, ReSharper, `.editorconfig`, etc. A .NET-first platform is expected to coexist with that world. | MEDIUM | This does **not** have to mean “import everything in v1”, but CodePass should eventually ingest or at least respect existing analyzer/configuration conventions. |

### Differentiators (Competitive Advantage)

Features that set the product apart. Not required, but valuable.

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| Structured custom-rule DSL with schema-driven forms | This is the clearest product wedge. Sonar custom rules for C# are not lightweight, NDepend CQLinq is powerful but expert-oriented, and Qodana customization is still tool-centric. A schema-backed DSL makes custom .NET analysis accessible to regular teams. | HIGH | This should be CodePass’s main bet. Keep the rule catalog closed and versioned; expose forms for common rule kinds and optional raw JSON for power users. |
| Roslyn-semantic rule authoring for architecture and codebase conventions | Most teams want rules about namespaces, attributes, DI registrations, layering, naming, inheritance, exceptions, async usage, test conventions, etc. A .NET-native rule model can express these better than generic platforms. | HIGH | Start with a small number of high-value rule kinds: naming, attribute presence, type/member shape, dependency/layering constraints, inheritance/interface usage, and test/project conventions. |
| Frictionless self-hosted local-path workflow | Sonar and Qodana lean heavily toward CI/repository-centric workflows. A platform that simply watches a machine-local `.sln` path and lets the user run focused analyses is materially simpler for solo developers and small .NET teams. | LOW | This is not flashy, but it strongly matches the project’s stated frustration with infrastructure-heavy tools. |
| Opinionated “current pass” dashboard for .NET teams | Existing platforms often optimize for enterprise governance, PR checks, and portfolios. CodePass can win by making the current state obvious: What failed? Why? Which rules hurt the score? Which classes lack coverage? | MEDIUM | Keep the dashboard current-state-first, not analytics-first. This aligns with the project goal better than trend-heavy reporting. |
| First-class rule authoring UX, not just rule execution | Competitors mostly assume expert authorship via plugins, query languages, YAML/XML, or external analyzers. A good authoring experience itself can be the feature. | HIGH | Treat schema docs, form validation, previews, examples, and JSON diff/versioning as product features, not admin plumbing. |

### Anti-Features (Commonly Requested, Often Problematic)

Features that seem good but create problems.

| Feature | Why Requested | Why Problematic | Alternative |
|---------|---------------|-----------------|-------------|
| Multi-language support in v1 | Teams often want one platform for all repos. | It destroys the core positioning. CodePass wins by being better for C#/.NET, not by being adequate for everything. | Stay C#/.NET-only until the DSL, analysis engine, and UX are clearly superior in that niche. |
| CI/PR/branch automation before the local workflow is excellent | Competitors market PR decoration and pipeline gates, so users will ask for it quickly. | It forces repository binding, branch identity, SCM models, and more operational complexity before the core analysis loop is proven. | Keep v1 manual and local-path driven; add automation only after the rule/coverage review loop feels excellent. |
| Execution history and trend dashboards in v1 | Managers and architects love historical charts. | Trending requires stable fingerprints, storage growth, comparison semantics, and UI complexity. It can easily delay the core product. | Show current snapshot + pass/fail now. Add baseline/noise control before full trend analytics. |
| AI-generated rule authoring or one-click auto-fix in v1 | It sounds modern and competitors market AI heavily. | For custom rules, determinism, reviewability, schema validity, and versioning matter more than novelty. For fixes, trust and safety are hard problems. | Use a structured DSL first. If AI comes later, make it assist schema filling instead of replacing it. |
| Expanding into SAST/SCA/secrets/license scanning in v1 | Sonar and Qodana bundle security features, so buyers may expect them. | This turns CodePass into a broad AppSec platform and weakens the focused code-quality story. | Stay on maintainability/reliability/coverage first; add extension points later if the niche is validated. |
| Enterprise workflow features (portfolios, assignment workflows, multi-tenant roles) too early | Larger teams often expect governance features. | Heavy auth, org models, audit trails, and collaboration workflows add a large non-core surface area. | Keep auth/roles minimal for v1 and optimize for single-instance internal use first. |

## Feature Dependencies

```text
[Solution registration + build configuration]
    ├──requires──> [Rule analysis execution]
    │                  ├──requires──> [Violation persistence + rule metadata]
    │                  │                  └──requires──> [Rule/group/severity UI]
    │                  └──enhances──> [Project score / pass-fail dashboard]
    └──requires──> [Coverage execution]
                       ├──requires──> [Coverage report ingestion/parsing]
                       ├──requires──> [Class-level coverage UI]
                       └──enhances──> [Project score / pass-fail dashboard]

[Rule catalog + schema definitions]
    └──requires──> [Schema-driven rule editor]
                       └──requires──> [Custom rule execution]
                              └──enhances──> [Violation persistence + rule metadata]

[Stable issue fingerprinting]
    ├──requires──> [Baseline / suppressions]
    └──requires──> [Trend/history later]

[Roslyn ecosystem interoperability]
    └──enhances──> [Rule analysis execution]
```

### Dependency Notes

- **Solution registration requires rule and coverage execution:** in .NET, credible analysis is build-aware. The product must know which solution/project/configuration it is running against before anything else matters.
- **Violation persistence + rule metadata require rule execution:** grouping violations by rule with severity is impossible if results are stored only as flat text output.
- **Project score requires both rule and coverage data:** a score that ignores either code issues or test coverage will feel arbitrary in this category.
- **Schema-driven rule editing requires a closed rule catalog:** the form experience only works if each rule kind has a known schema, validation model, and execution contract.
- **Baseline/suppressions require stable issue fingerprinting:** this is the hidden dependency most teams underestimate. If issue identities are unstable, every later feature in noise control and trends becomes unreliable.
- **Roslyn interoperability enhances analysis rather than replacing it:** CodePass should not become a generic report bucket; it should combine first-party rules with ecosystem compatibility.

## MVP Definition

### Launch With (v1)

Minimum viable product — what’s needed to validate the concept.

- [x] Register a local `.sln` path and validate the solution/build context — foundational for a real .NET-native workflow.
- [x] Manually run rule analysis and inspect violations grouped by rule with severity — core user value.
- [x] Manually run coverage analysis and inspect coverage by class — required to support a believable code-quality story.
- [x] Show a project score/dashboard with a simple pass/fail outcome — necessary for “does this project pass?” clarity.
- [x] Create and edit custom rules through a structured DSL with schema-driven forms and optional raw JSON — the primary differentiator worth validating.

### Add After Validation (v1.x)

Features to add once core is working.

- [ ] Lightweight baseline / suppression support — essential for legacy-code adoption once real users run against non-trivial codebases.
- [ ] Roslyn analyzer interoperability/import — add when users ask to unify existing analyzer investments.
- [ ] More rule kinds for architectural and organizational conventions — add once the DSL and executor model prove stable.
- [ ] Rule/profile packs per project or team — add when more than one opinionated standard needs to coexist.

### Future Consideration (v2+)

Features to defer until product-market fit is established.

- [ ] CI/PR/branch integration — valuable, but only after the local/manual workflow is excellent.
- [ ] Execution history, trend analysis, and regressions over time — useful once stable fingerprints and baseline semantics exist.
- [ ] Enterprise collaboration/governance features — only after single-instance and small-team usage is proven.
- [ ] Security/SCA/autofix/AI-authoring expansion — only if CodePass first wins as a code-quality product.

## Feature Prioritization Matrix

| Feature | User Value | Implementation Cost | Priority |
|---------|------------|---------------------|----------|
| Local `.sln` registration and build-aware analysis target | HIGH | MEDIUM | P1 |
| Violations grouped by rule with severity and locations | HIGH | MEDIUM | P1 |
| Coverage analysis with class-level reporting | HIGH | MEDIUM | P1 |
| Project score / pass-fail dashboard | HIGH | MEDIUM | P1 |
| Structured custom-rule DSL + form editor | HIGH | HIGH | P1 |
| Baseline / suppressions / new-problems focus | HIGH | HIGH | P2 |
| Roslyn ecosystem interoperability | MEDIUM | MEDIUM | P2 |
| Rule/profile packs and advanced rule catalogs | MEDIUM | MEDIUM | P2 |
| CI/PR automation | MEDIUM | HIGH | P3 |
| Trend/history dashboards | MEDIUM | HIGH | P3 |
| Enterprise collaboration/governance | LOW | HIGH | P3 |
| Security/SCA/AI expansion | LOW | HIGH | P3 |

**Priority key:**
- P1: Must have for launch
- P2: Should have, add when possible
- P3: Nice to have, future consideration

## Competitor Feature Analysis

| Feature | SonarQube | NDepend | Our Approach |
|---------|-----------|---------|--------------|
| Quality gate / pass-fail | Strong quality gates on new/overall code, often CI-oriented | PASS/WARN/FAIL quality gates tied to code metrics and debt | Simpler current-state score focused on one .NET project/solution at a time |
| Coverage integration | Imports .NET coverage reports and uses them in gates | Imports coverage, shows it in dashboard, rules, trends, and reports | Manual coverage run with class-centric UI first; keep scoring understandable |
| Rule customization | Strong rule/profile management, but C# custom rule authoring is not lightweight | Very powerful CQLinq custom rules, but expert-oriented | Schema-backed DSL aimed at normal .NET teams, not just experts |
| Legacy-code noise control | Focus on new code and quality gates | Baseline diffs and issue regression tracking | Lightweight baseline/suppression after core validation |
| Ecosystem interoperability | Imports Roslyn issues for C# by default | Imports Roslyn and ReSharper issues as first-class citizens | Later interop with Roslyn ecosystem without diluting first-party rule model |

## Sources

- CodePass project context: `.planning/PROJECT.md`
- SonarQube product overview: https://www.sonarsource.com/products/sonarqube/ [HIGH]
- SonarQube basic analysis principles: https://docs.sonarsource.com/sonarqube-server/2026.1/discovering/analysis-overview/basic-principles [HIGH]
- SonarQube quality gates: https://docs.sonarsource.com/sonarqube-server/quality-standards-administration/managing-quality-gates/introduction-to-quality-gates [HIGH]
- SonarQube .NET coverage import parameters: https://docs.sonarsource.com/sonarqube-server/analyzing-source-code/test-coverage/test-coverage-parameters [HIGH]
- SonarQube external analyzer reports / Roslyn import: https://docs.sonarsource.com/sonarqube-server/analyzing-source-code/importing-external-issues/external-analyzer-reports [HIGH]
- SonarQube custom coding rules support: https://docs.sonarsource.com/sonarqube-server/extension-guide/adding-coding-rules [HIGH]
- Qodana overview: https://www.jetbrains.com/help/qodana/about-qodana.html [HIGH]
- Qodana for .NET: https://www.jetbrains.com/help/qodana/dotnet.html [HIGH]
- Qodana code coverage: https://www.jetbrains.com/help/qodana/code-coverage.html [HIGH]
- NDepend features: https://www.ndepend.com/features [HIGH]
- NDepend coverage import: https://www.ndepend.com/docs/code-coverage [HIGH]
- NDepend CQLinq features: https://www.ndepend.com/docs/cqlinq-features [HIGH]
- NDepend Roslyn/ReSharper issue reporting: https://www.ndepend.com/docs/reporting-roslyn-analyzers-issues [HIGH]
- NDepend trend monitoring: https://www.ndepend.com/docs/monitor-code-trend [HIGH]

---
*Feature research for: self-hosted .NET-native code quality platform for C#/.NET solutions*
*Researched: 2026-04-19*
