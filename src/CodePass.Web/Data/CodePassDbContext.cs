using CodePass.Web.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CodePass.Web.Data;

public sealed class CodePassDbContext(DbContextOptions<CodePassDbContext> options) : DbContext(options)
{
    public DbSet<RegisteredSolution> RegisteredSolutions => Set<RegisteredSolution>();
    public DbSet<AuthoredRuleDefinition> AuthoredRuleDefinitions => Set<AuthoredRuleDefinition>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var registeredSolution = modelBuilder.Entity<RegisteredSolution>();

        registeredSolution.HasKey(solution => solution.Id);
        registeredSolution.Property(solution => solution.DisplayName).IsRequired();
        registeredSolution.Property(solution => solution.SolutionPath).IsRequired();
        registeredSolution.Property(solution => solution.Status).HasConversion<string>().IsRequired();
        registeredSolution.HasIndex(solution => solution.DisplayName);
        registeredSolution.HasIndex(solution => solution.SolutionPath).IsUnique();

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
