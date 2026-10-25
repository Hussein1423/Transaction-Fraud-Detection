using Microsoft.EntityFrameworkCore;
using Transaction_Fraud_Detection_API.Models;
namespace Transaction_Fraud_Detection_API.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<FraudEvaluation> FraudEvaluations => Set<FraudEvaluation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<FraudEvaluation>()
            .Property(e => e.Decision)
            .HasConversion<string>()
            .HasMaxLength(20);

        modelBuilder.Entity<TriggeredRule>()
       .Property(r => r.RuleCode)
       .HasConversion<string>()
       .HasMaxLength(30);
        modelBuilder.Entity<TriggeredRule>()
    .ToTable("TriggeredRules");
    }
}