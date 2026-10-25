
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace Transaction_Fraud_Detection_API.Models;

public class Transaction
{
    public Guid TransactionId { get; set; }

    [Required]
    [MaxLength(100)]
    public string UserId { get; set; } = string.Empty;

    [Precision(18, 2)]
    public decimal Amount { get; set; }

    [Required]
    [StringLength(3, MinimumLength = 3)]
    public string Currency { get; set; } = "SAR";

    [Required]
    [MaxLength(100)]
    public string MerchantId { get; set; } = string.Empty;

    public DateTimeOffset Timestamp { get; set; }
}
