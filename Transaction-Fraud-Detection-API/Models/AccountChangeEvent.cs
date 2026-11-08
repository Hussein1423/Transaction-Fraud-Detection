using System.ComponentModel.DataAnnotations;
using Transaction_Fraud_Detection_API.Enums;
namespace Transaction_Fraud_Detection_API.Models
{
    public class AccountChangeEvent
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string UserId { get; set; } = string.Empty;

        public AccountChangeType Type { get; set; }

        public DateTimeOffset Time { get; set; } = DateTimeOffset.UtcNow;
    }
}
