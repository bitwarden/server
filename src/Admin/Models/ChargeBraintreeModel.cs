using System.ComponentModel.DataAnnotations;

namespace Bit.Admin.Models;

public class ChargeBraintreeModel : IValidatableObject
{
    [Required]
    [Display(Name = "Braintree Customer Id")]
    public string Id { get; set; }
    [Required]
    [Display(Name = "Amount")]
    public decimal? Amount { get; set; }
    public string TransactionId { get; set; }
    public string PayPalTransactionId { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Id != null)
        {
            if (Id.Length != 36 || (Id[0] != 'o' && Id[0] != 'u') ||
                !Guid.TryParse(Id.Substring(1, 32), out var guid))
            {
                yield return new ValidationResult("Customer Id is not a valid format.");
            }
        }
    }
}
