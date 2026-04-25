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
            tableNames.Should().Contain("SolutionRuleAssignments");

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

    [Fact]
    public async Task InitializeAsync_ShouldAddSolutionRuleAssignmentsTableWithoutLosingExistingSolutionOrAuthoredRuleData()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"codepass-init-{Guid.NewGuid():N}.db");
        var connectionString = $"Data Source={databasePath}";

        try
        {
            var existingSolutionId = Guid.NewGuid();
            var existingRuleId = Guid.NewGuid();
            await CreateLegacyDatabaseWithAuthoredRulesAsync(connectionString, existingSolutionId, existingRuleId);

            var options = new DbContextOptionsBuilder<CodePassDbContext>()
                .UseSqlite(connectionString)
                .Options;

            await using var dbContext = new CodePassDbContext(options);
            await CodePassDatabaseInitializer.InitializeAsync(dbContext);

            var tableNames = await ReadTableNamesAsync(connectionString);
            tableNames.Should().Contain("RegisteredSolutions");
            tableNames.Should().Contain("AuthoredRuleDefinitions");
            tableNames.Should().Contain("SolutionRuleAssignments");
            tableNames.Should().Contain("RuleAnalysisRuns");
            tableNames.Should().Contain("RuleAnalysisViolations");

            (await dbContext.RegisteredSolutions.CountAsync()).Should().Be(1);
            (await dbContext.AuthoredRuleDefinitions.CountAsync()).Should().Be(1);

            dbContext.SolutionRuleAssignments.Add(new SolutionRuleAssignment
            {
                Id = Guid.NewGuid(),
                RegisteredSolutionId = existingSolutionId,
                AuthoredRuleDefinitionId = existingRuleId,
                IsEnabled = true,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                UpdatedAtUtc = DateTimeOffset.UtcNow
            });

            var analysisRun = new RuleAnalysisRun
            {
                Id = Guid.NewGuid(),
                RegisteredSolutionId = existingSolutionId,
                Status = RuleAnalysisRunStatus.Succeeded,
                StartedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1),
                CompletedAtUtc = DateTimeOffset.UtcNow,
                RuleCount = 1,
                TotalViolations = 1
            };
            dbContext.RuleAnalysisRuns.Add(analysisRun);
            dbContext.RuleAnalysisViolations.Add(new RuleAnalysisViolation
            {
                Id = Guid.NewGuid(),
                RuleAnalysisRunId = analysisRun.Id,
                AuthoredRuleDefinitionId = existingRuleId,
                RuleCode = "CP3000",
                RuleTitle = "Existing authored rule",
                RuleKind = "syntax_presence",
                RuleSeverity = RuleSeverity.Error,
                Message = "Use explicit type instead of var.",
                FilePath = "src/Legacy.cs",
                StartLine = 10,
                StartColumn = 5,
                EndLine = 10,
                EndColumn = 8
            });

            await dbContext.SaveChangesAsync();

            (await dbContext.SolutionRuleAssignments.CountAsync()).Should().Be(1);
            (await dbContext.RuleAnalysisRuns.CountAsync()).Should().Be(1);
            (await dbContext.RuleAnalysisViolations.CountAsync()).Should().Be(1);
            var existingSolution = await dbContext.RegisteredSolutions.SingleAsync();
            existingSolution.DisplayName.Should().Be("Legacy solution with authored rules");
            var existingRule = await dbContext.AuthoredRuleDefinitions.SingleAsync();
            existingRule.Code.Should().Be("CP3000");
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

    private static async Task CreateLegacyDatabaseWithAuthoredRulesAsync(string connectionString, Guid solutionId, Guid ruleId)
    {
        var options = new DbContextOptionsBuilder<LegacyCodePassDbContextWithAuthoredRules>()
            .UseSqlite(connectionString)
            .Options;

        await using var legacyContext = new LegacyCodePassDbContextWithAuthoredRules(options);
        await legacyContext.Database.EnsureCreatedAsync();
        var now = DateTimeOffset.UtcNow;
        legacyContext.RegisteredSolutions.Add(new RegisteredSolution
        {
            Id = solutionId,
            DisplayName = "Legacy solution with authored rules",
            SolutionPath = "/tmp/legacy-with-authored-rules.sln",
            Status = RegisteredSolutionStatus.Valid,
            LastValidatedAtUtc = now,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });
        legacyContext.AuthoredRuleDefinitions.Add(new AuthoredRuleDefinition
        {
            Id = ruleId,
            Code = "CP3000",
            Title = "Existing authored rule",
            Description = null,
            RuleKind = "syntax_presence",
            SchemaVersion = "1.0",
            Severity = RuleSeverity.Error,
            ScopeJson = "{\"projects\":[\"*\"],\"files\":[\"**/*.cs\"],\"excludeFiles\":[]}",
            ParametersJson = "{\"mode\":\"forbid\",\"targets\":[\"local_declaration\"],\"syntaxKinds\":[\"var\"],\"allowInTests\":false}",
            RawDefinitionJson = "{\"id\":\"CP3000\",\"title\":\"Existing authored rule\",\"kind\":\"syntax_presence\",\"schemaVersion\":\"1.0\",\"severity\":\"error\",\"enabled\":true,\"language\":\"csharp\",\"scope\":{\"projects\":[\"*\"],\"files\":[\"**/*.cs\"],\"excludeFiles\":[]},\"parameters\":{\"mode\":\"forbid\",\"targets\":[\"local_declaration\"],\"syntaxKinds\":[\"var\"],\"allowInTests\":false}}",
            IsEnabled = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
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
            ConfigureRegisteredSolution(modelBuilder);
        }
    }

    private sealed class LegacyCodePassDbContextWithAuthoredRules(DbContextOptions<LegacyCodePassDbContextWithAuthoredRules> options) : DbContext(options)
    {
        public DbSet<RegisteredSolution> RegisteredSolutions => Set<RegisteredSolution>();

        public DbSet<AuthoredRuleDefinition> AuthoredRuleDefinitions => Set<AuthoredRuleDefinition>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            ConfigureRegisteredSolution(modelBuilder);
            ConfigureAuthoredRuleDefinition(modelBuilder);
        }
    }

    private static void ConfigureRegisteredSolution(ModelBuilder modelBuilder)
    {
        var registeredSolution = modelBuilder.Entity<RegisteredSolution>();

        registeredSolution.HasKey(solution => solution.Id);
        registeredSolution.Property(solution => solution.DisplayName).IsRequired();
        registeredSolution.Property(solution => solution.SolutionPath).IsRequired();
        registeredSolution.Property(solution => solution.Status).HasConversion<string>().IsRequired();
        registeredSolution.HasIndex(solution => solution.DisplayName);
        registeredSolution.HasIndex(solution => solution.SolutionPath).IsUnique();
    }

    private static void ConfigureAuthoredRuleDefinition(ModelBuilder modelBuilder)
    {
        var authoredRuleDefinition = modelBuilder.Entity<AuthoredRuleDefinition>();

        authoredRuleDefinition.HasKey(rule => rule.Id);
        authoredRuleDefinition.Property(rule => rule.Code).IsRequired();
        authoredRuleDefinition.Property(rule => rule.Title).IsRequired();
        authoredRuleDefinition.Property(rule => rule.Description);
        authoredRuleDefinition.Property(rule => rule.RuleKind).IsRequired();
        authoredRuleDefinition.Property(rule => rule.SchemaVersion).IsRequired();
        authoredRuleDefinition.Property(rule => rule.Severity).HasConversion<string>().IsRequired();
        authoredRuleDefinition.Property(rule => rule.ScopeJson).IsRequired();
        authoredRuleDefinition.Property(rule => rule.ParametersJson).IsRequired();
        authoredRuleDefinition.Property(rule => rule.RawDefinitionJson).IsRequired();
        authoredRuleDefinition.Property(rule => rule.IsEnabled).IsRequired();
        authoredRuleDefinition.HasIndex(rule => rule.Code).IsUnique();
        authoredRuleDefinition.HasIndex(rule => rule.RuleKind);
    }
}
