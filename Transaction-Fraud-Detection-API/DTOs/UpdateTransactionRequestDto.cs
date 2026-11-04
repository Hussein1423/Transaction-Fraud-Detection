using System.ComponentModel.DataAnnotations;

namespace Transaction_Fraud_Detection_API.DTOs;

public class UpdateTransactionRequestDto : IValidatableObject
{
    [Range(typeof(decimal), "1.0", "9999999999999999.99")]
    public decimal? Amount { get; set; }

    [RegularExpression(@"^[A-Za-z]{3}$")]
    public string? Currency { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Amount is null && Currency is null)
        {
            yield return new ValidationResult(
                "At least one field (Amount or Currency) must be provided.",
                new[] { nameof(Amount), nameof(Currency) });
        }
    }
}