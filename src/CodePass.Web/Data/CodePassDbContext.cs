using CodePass.Web.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CodePass.Web.Data;

public sealed class CodePassDbContext(DbContextOptions<CodePassDbContext> options) : DbContext(options)
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
