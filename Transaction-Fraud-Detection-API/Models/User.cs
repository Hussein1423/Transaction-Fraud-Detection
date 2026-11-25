using System.ComponentModel.DataAnnotations;

namespace Transaction_Fraud_Detection_API.Models;

public class User
{
    [Key]
    [MaxLength(100)]
    public string UserId { get; set; } = Guid.NewGuid().ToString();

    [Required]
    [MaxLength(100)]
    public string Username { get; set; } = string.Empty;

    [Required]
    [MaxLength(255)]
    public string Password { get; set; } = string.Empty;

    public ICollection<Transaction> Transactions { get; set; }
        = new List<Transaction>();

    public ICollection<AccountChangeEvent> AccountChangeEvents { get; set; }
        = new List<AccountChangeEvent>();
}