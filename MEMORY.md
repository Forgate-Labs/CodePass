# MEMORY

- Date: 2026-04-19
  - Error: I proposed a v1 scope item that implied CodePass would ship built-in rules/rule kinds.
  - Pattern: When discussing customizable rule systems, I can conflate engine-supported rule shapes with built-in product rules.
  - Preventive rule: For CodePass, do not assume built-in production rules in v1. Treat rules as user-authored unless the user explicitly asks for shipped defaults; repository examples are fine only as learning material.

- Date: 2026-04-19
  - Error: I captured “remover” as a direct action on the solution card before the user finished defining the edit/remove flow.
  - Pattern: When summarizing UI actions early, I can lock in an interaction location (card vs modal) before the user has decided it.
  - Preventive rule: For CodePass UI discussions, do not assume where an action lives until the user decides the interaction flow; keep action availability separate from action placement.

- Date: 2026-04-25
  - Error: I gave a long single-line raw JSON rule and then implied the user's paste was likely wrong when the UI still reported invalid JSON.
  - Pattern: Long JSON strings in chat can wrap or copy poorly into browser textareas, causing invalid JSON even when the semantic content is correct.
  - Preventive rule: For CodePass raw-rule examples, provide pretty-printed JSON with short string values and avoid long inline strings; if a user reports paste failure, assume the example/UX is at fault and simplify the payload first.

- Date: 2026-04-29
  - Error: I committed and pushed the local SQLite database files (`codepass.db`, `codepass.db-shm`, `codepass.db-wal`) to the repository.
  - Pattern: When staging all changes with `git add -A`, generated local runtime artifacts can be included if ignore rules are incomplete.
  - Preventive rule: Before committing in CodePass, inspect staged files and never commit local database artifacts; ensure SQLite files are ignored and remove them from tracking if present.
