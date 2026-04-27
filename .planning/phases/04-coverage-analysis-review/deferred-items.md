# Deferred Items

- 2026-04-27 — Unrelated in-progress coverage persistence/database-initializer changes temporarily made `dotnet build CodePass.sln` fail in the active checkout with missing helper methods in `CodePassDatabaseInitializerTests`. Plan 04-01 changes were verified in a clean detached worktree at commit `66727e1`, where `dotnet test CodePass.sln --filter "FullyQualifiedName~CoberturaCoverageParserTests" && dotnet build CodePass.sln` passed; the final checkout also passes the same command.
