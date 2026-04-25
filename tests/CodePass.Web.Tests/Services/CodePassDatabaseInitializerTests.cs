using CodePass.Web.Data;
using CodePass.Web.Data.Entities;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CodePass.Web.Tests.Services;

public sealed class CodePassDatabaseInitializerTests
{
    [Fact]
    public async Task InitializeAsync_ShouldAddAuthoredRulesTableToExistingLegacySqliteDatabase()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"codepass-init-{Guid.NewGuid():N}.db");
        var connectionString = $"Data Source={databasePath}";

        try
        {
            await CreateLegacyDatabaseAsync(connectionString);

            var options = new DbContextOptionsBuilder<CodePassDbContext>()
                .UseSqlite(connectionString)
                .Options;

            await using var dbContext = new CodePassDbContext(options);
            await CodePassDatabaseInitializer.InitializeAsync(dbContext);

            var tableNames = await ReadTableNamesAsync(connectionString);
            tableNames.Should().Contain("RegisteredSolutions");
            tableNames.Should().Contain("AuthoredRuleDefinitions");

            var existingSolution = await dbContext.RegisteredSolutions.SingleAsync();
            existingSolution.DisplayName.Should().Be("Legacy solution");

            dbContext.AuthoredRuleDefinitions.Add(new AuthoredRuleDefinition
            {
                Id = Guid.NewGuid(),
                Code = "CP2000",
                Title = "Added after upgrade",
                Description = null,
                RuleKind = "syntax_presence",
                SchemaVersion = "1.0",
                Severity = RuleSeverity.Warning,
                ScopeJson = "{\"projects\":[\"*\"],\"files\":[\"**/*.cs\"],\"excludeFiles\":[]}",
                ParametersJson = "{\"mode\":\"forbid\",\"targets\":[\"local_declaration\"],\"syntaxKinds\":[\"var\"],\"allowInTests\":false}",
                RawDefinitionJson = "{\"id\":\"CP2000\",\"title\":\"Added after upgrade\",\"kind\":\"syntax_presence\",\"schemaVersion\":\"1.0\",\"severity\":\"warning\",\"enabled\":true,\"language\":\"csharp\",\"scope\":{\"projects\":[\"*\"],\"files\":[\"**/*.cs\"],\"excludeFiles\":[]},\"parameters\":{\"mode\":\"forbid\",\"targets\":[\"local_declaration\"],\"syntaxKinds\":[\"var\"],\"allowInTests\":false}}",
                IsEnabled = true,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                UpdatedAtUtc = DateTimeOffset.UtcNow
            });

            await dbContext.SaveChangesAsync();

            (await dbContext.AuthoredRuleDefinitions.CountAsync()).Should().Be(1);
        }
        finally
        {
            File.Delete(databasePath);
        }
    }

    private static async Task CreateLegacyDatabaseAsync(string connectionString)
    {
        var options = new DbContextOptionsBuilder<LegacyCodePassDbContext>()
            .UseSqlite(connectionString)
            .Options;

        await using var legacyContext = new LegacyCodePassDbContext(options);
        await legacyContext.Database.EnsureCreatedAsync();
        legacyContext.RegisteredSolutions.Add(new RegisteredSolution
        {
            Id = Guid.NewGuid(),
            DisplayName = "Legacy solution",
            SolutionPath = "/tmp/legacy.sln",
            Status = RegisteredSolutionStatus.Valid,
            LastValidatedAtUtc = DateTimeOffset.UtcNow,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        });
        await legacyContext.SaveChangesAsync();
    }

    private static async Task<IReadOnlyList<string>> ReadTableNamesAsync(string connectionString)
    {
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table' ORDER BY name;";
        await using var reader = await command.ExecuteReaderAsync();

        var names = new List<string>();
        while (await reader.ReadAsync())
        {
            names.Add(reader.GetString(0));
        }

        return names;
    }

    private sealed class LegacyCodePassDbContext(DbContextOptions<LegacyCodePassDbContext> options) : DbContext(options)
    {
        public DbSet<RegisteredSolution> RegisteredSolutions => Set<RegisteredSolution>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            var registeredSolution = modelBuilder.Entity<RegisteredSolution>();

            registeredSolution.HasKey(solution => solution.Id);
            registeredSolution.Property(solution => solution.DisplayName).IsRequired();
            registeredSolution.Property(solution => solution.SolutionPath).IsRequired();
            registeredSolution.Property(solution => solution.Status).HasConversion<string>().IsRequired();
            registeredSolution.HasIndex(solution => solution.DisplayName);
            registeredSolution.HasIndex(solution => solution.SolutionPath).IsUnique();
        }
    }
}
