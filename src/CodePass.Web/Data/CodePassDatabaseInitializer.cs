using System.Data.Common;
using Microsoft.EntityFrameworkCore;

namespace CodePass.Web.Data;

public static class CodePassDatabaseInitializer
{
    public static async Task InitializeAsync(CodePassDbContext dbContext, CancellationToken cancellationToken = default)
    {
        await dbContext.Database.EnsureCreatedAsync(cancellationToken);

        if (!dbContext.Database.IsSqlite())
        {
            return;
        }

        if (!await TableExistsAsync(dbContext, "AuthoredRuleDefinitions", cancellationToken))
        {
            await dbContext.Database.ExecuteSqlRawAsync(
                """
                CREATE TABLE IF NOT EXISTS "AuthoredRuleDefinitions" (
                    "Id" TEXT NOT NULL CONSTRAINT "PK_AuthoredRuleDefinitions" PRIMARY KEY,
                    "Code" TEXT NOT NULL,
                    "Title" TEXT NOT NULL,
                    "Description" TEXT NULL,
                    "RuleKind" TEXT NOT NULL,
                    "SchemaVersion" TEXT NOT NULL,
                    "Severity" TEXT NOT NULL,
                    "ScopeJson" TEXT NOT NULL,
                    "ParametersJson" TEXT NOT NULL,
                    "RawDefinitionJson" TEXT NOT NULL,
                    "IsEnabled" INTEGER NOT NULL,
                    "CreatedAtUtc" TEXT NOT NULL,
                    "UpdatedAtUtc" TEXT NOT NULL
                );
                """,
                cancellationToken);
        }

        await dbContext.Database.ExecuteSqlRawAsync(
            "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_AuthoredRuleDefinitions_Code\" ON \"AuthoredRuleDefinitions\" (\"Code\");",
            cancellationToken);

        await dbContext.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS \"IX_AuthoredRuleDefinitions_RuleKind\" ON \"AuthoredRuleDefinitions\" (\"RuleKind\");",
            cancellationToken);

        if (!await TableExistsAsync(dbContext, "SolutionRuleAssignments", cancellationToken))
        {
            await dbContext.Database.ExecuteSqlRawAsync(
                """
                CREATE TABLE IF NOT EXISTS "SolutionRuleAssignments" (
                    "Id" TEXT NOT NULL CONSTRAINT "PK_SolutionRuleAssignments" PRIMARY KEY,
                    "RegisteredSolutionId" TEXT NOT NULL,
                    "AuthoredRuleDefinitionId" TEXT NOT NULL,
                    "IsEnabled" INTEGER NOT NULL,
                    "CreatedAtUtc" TEXT NOT NULL,
                    "UpdatedAtUtc" TEXT NOT NULL,
                    CONSTRAINT "FK_SolutionRuleAssignments_RegisteredSolutions_RegisteredSolutionId" FOREIGN KEY ("RegisteredSolutionId") REFERENCES "RegisteredSolutions" ("Id") ON DELETE CASCADE,
                    CONSTRAINT "FK_SolutionRuleAssignments_AuthoredRuleDefinitions_AuthoredRuleDefinitionId" FOREIGN KEY ("AuthoredRuleDefinitionId") REFERENCES "AuthoredRuleDefinitions" ("Id") ON DELETE CASCADE
                );
                """,
                cancellationToken);
        }

        await dbContext.Database.ExecuteSqlRawAsync(
            "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_SolutionRuleAssignments_RegisteredSolutionId_AuthoredRuleDefinitionId\" ON \"SolutionRuleAssignments\" (\"RegisteredSolutionId\", \"AuthoredRuleDefinitionId\");",
            cancellationToken);

        if (!await TableExistsAsync(dbContext, "RuleAnalysisRuns", cancellationToken))
        {
            await dbContext.Database.ExecuteSqlRawAsync(
                """
                CREATE TABLE IF NOT EXISTS "RuleAnalysisRuns" (
                    "Id" TEXT NOT NULL CONSTRAINT "PK_RuleAnalysisRuns" PRIMARY KEY,
                    "RegisteredSolutionId" TEXT NOT NULL,
                    "Status" TEXT NOT NULL,
                    "StartedAtUtc" TEXT NOT NULL,
                    "CompletedAtUtc" TEXT NULL,
                    "RuleCount" INTEGER NOT NULL,
                    "TotalViolations" INTEGER NOT NULL,
                    "ErrorMessage" TEXT NULL,
                    CONSTRAINT "FK_RuleAnalysisRuns_RegisteredSolutions_RegisteredSolutionId" FOREIGN KEY ("RegisteredSolutionId") REFERENCES "RegisteredSolutions" ("Id") ON DELETE CASCADE
                );
                """,
                cancellationToken);
        }

        await dbContext.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS \"IX_RuleAnalysisRuns_RegisteredSolutionId\" ON \"RuleAnalysisRuns\" (\"RegisteredSolutionId\");",
            cancellationToken);

        await dbContext.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS \"IX_RuleAnalysisRuns_StartedAtUtc\" ON \"RuleAnalysisRuns\" (\"StartedAtUtc\");",
            cancellationToken);

        if (!await TableExistsAsync(dbContext, "RuleAnalysisViolations", cancellationToken))
        {
            await dbContext.Database.ExecuteSqlRawAsync(
                """
                CREATE TABLE IF NOT EXISTS "RuleAnalysisViolations" (
                    "Id" TEXT NOT NULL CONSTRAINT "PK_RuleAnalysisViolations" PRIMARY KEY,
                    "RuleAnalysisRunId" TEXT NOT NULL,
                    "AuthoredRuleDefinitionId" TEXT NULL,
                    "RuleCode" TEXT NOT NULL,
                    "RuleTitle" TEXT NOT NULL,
                    "RuleKind" TEXT NOT NULL,
                    "RuleSeverity" TEXT NOT NULL,
                    "Message" TEXT NOT NULL,
                    "FilePath" TEXT NOT NULL,
                    "StartLine" INTEGER NOT NULL,
                    "StartColumn" INTEGER NOT NULL,
                    "EndLine" INTEGER NOT NULL,
                    "EndColumn" INTEGER NOT NULL,
                    CONSTRAINT "FK_RuleAnalysisViolations_AuthoredRuleDefinitions_AuthoredRuleDefinitionId" FOREIGN KEY ("AuthoredRuleDefinitionId") REFERENCES "AuthoredRuleDefinitions" ("Id") ON DELETE SET NULL,
                    CONSTRAINT "FK_RuleAnalysisViolations_RuleAnalysisRuns_RuleAnalysisRunId" FOREIGN KEY ("RuleAnalysisRunId") REFERENCES "RuleAnalysisRuns" ("Id") ON DELETE CASCADE
                );
                """,
                cancellationToken);
        }

        await dbContext.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS \"IX_RuleAnalysisViolations_RuleAnalysisRunId\" ON \"RuleAnalysisViolations\" (\"RuleAnalysisRunId\");",
            cancellationToken);

        await dbContext.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS \"IX_RuleAnalysisViolations_RuleCode\" ON \"RuleAnalysisViolations\" (\"RuleCode\");",
            cancellationToken);
    }

    private static async Task<bool> TableExistsAsync(CodePassDbContext dbContext, string tableName, CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        var shouldClose = connection.State != System.Data.ConnectionState.Open;

        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = $name LIMIT 1;";

            var parameter = command.CreateParameter();
            parameter.ParameterName = "$name";
            parameter.Value = tableName;
            command.Parameters.Add(parameter);

            var result = await command.ExecuteScalarAsync(cancellationToken);
            return result is not null;
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }
}
