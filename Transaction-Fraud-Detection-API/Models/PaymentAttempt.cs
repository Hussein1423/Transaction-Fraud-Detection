using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Transaction_Fraud_Detection_API.Enums;

namespace Transaction_Fraud_Detection_API.Models;

public class PaymentAttempt
{
    [Key]
    public Guid PaymentAttemptId { get; set; }

    [Required]
    public Guid TransactionId { get; set; }

    [Required]
    public PaymentAttemptStatus Status { get; set; }

    [MaxLength(250)]
    public string? FailureReason { get; set; }

    [Required]
    public DateTimeOffset AttemptedAt { get; set; }

    [ForeignKey(nameof(TransactionId))]
    public Transaction Transaction { get; set; } = null!;

    [Precision(18, 2)]
    public decimal Amount { get; set; }

    [Required, StringLength(3, MinimumLength = 3)]
    public string Currency { get; set; } = "SAR";
    [StringLength(64, MinimumLength = 64)]
    public string? PaymentInstrumentFingerprint { get; set; }
}