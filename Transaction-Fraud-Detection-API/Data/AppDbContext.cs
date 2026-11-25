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

    public DbSet<TriggeredRule> TriggeredRules => Set<TriggeredRule>();

    public DbSet<PaymentAttemptFraudEvaluation> PaymentAttemptFraudEvaluations => Set<PaymentAttemptFraudEvaluation>();

    public DbSet<PaymentAttemptTriggeredRule> PaymentAttemptTriggeredRules => Set<PaymentAttemptTriggeredRule>();

    public DbSet<PaymentAttempt> PaymentAttempts => Set<PaymentAttempt>();

    public DbSet<AccountChangeEvent> AccountChangeEvents => Set<AccountChangeEvent>();

    public DbSet<User> Users => Set<User>();

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

        modelBuilder.Entity<PaymentAttempt>()
            .Property(p => p.Status)
            .HasConversion<string>()
            .HasMaxLength(20);

        modelBuilder.Entity<PaymentAttempt>()
        .HasOne(p => p.Transaction)
        .WithMany()
        .HasForeignKey(p => p.TransactionId)
        .OnDelete(DeleteBehavior.Restrict);


        modelBuilder.Entity<PaymentAttemptFraudEvaluation>()
        .Property(e => e.Decision)
        .HasConversion<string>()
        .HasMaxLength(20);

        modelBuilder.Entity<PaymentAttemptFraudEvaluation>()
            .HasOne(e => e.PaymentAttempt)
            .WithOne()
            .HasForeignKey<PaymentAttemptFraudEvaluation>(e => e.PaymentAttemptId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<PaymentAttemptTriggeredRule>()
            .Property(r => r.RuleCode)
            .HasConversion<string>()
            .HasMaxLength(50);

        modelBuilder.Entity<PaymentAttemptTriggeredRule>()
            .HasOne(r => r.PaymentAttemptFraudEvaluation)
            .WithMany()
            .HasForeignKey(r => r.PaymentAttemptFraudEvaluationId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<AccountChangeEvent>()
    .Property(e => e.Type)
    .HasConversion<string>()
    .HasMaxLength(20);

        modelBuilder.Entity<AccountChangeEvent>()
            .HasIndex(e => new { e.UserId, e.Time });

        modelBuilder.Entity<Transaction>()
    .HasIndex(t => new { t.UserId, t.Timestamp });

        modelBuilder.Entity<User>()
    .HasIndex(u => u.Username)
    .IsUnique();

        modelBuilder.Entity<Transaction>()
            .HasOne(t => t.User)
            .WithMany(u => u.Transactions)
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<AccountChangeEvent>()
            .HasOne(e => e.User)
            .WithMany(u => u.AccountChangeEvents)
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}