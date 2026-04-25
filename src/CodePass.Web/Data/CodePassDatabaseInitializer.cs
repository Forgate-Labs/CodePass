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

        if (await TableExistsAsync(dbContext, "AuthoredRuleDefinitions", cancellationToken))
        {
            return;
        }

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

        await dbContext.Database.ExecuteSqlRawAsync(
            "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_AuthoredRuleDefinitions_Code\" ON \"AuthoredRuleDefinitions\" (\"Code\");",
            cancellationToken);

        await dbContext.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS \"IX_AuthoredRuleDefinitions_RuleKind\" ON \"AuthoredRuleDefinitions\" (\"RuleKind\");",
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
