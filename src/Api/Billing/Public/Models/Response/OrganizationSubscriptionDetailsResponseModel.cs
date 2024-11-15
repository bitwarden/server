using System.ComponentModel.DataAnnotations;

namespace Bit.Api.Billing.Public.Models;

public class OrganizationSubscriptionDetailsResponseModel : IValidatableObject
{
    public PasswordManagerSubscriptionDetails PasswordManager { get; set; }
    public SecretsManagerSubscriptionDetails SecretsManager { get; set; }
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (PasswordManager == null && SecretsManager == null)
        {
            yield return new ValidationResult("At least one of PasswordManager or SecretsManager must be provided.");
        }

        yield return ValidationResult.Success;
    }
}
public class PasswordManagerSubscriptionDetails
{
    public int? Seats { get; set; }
    public int? MaxAutoScaleSeats { get; set; }
    public short? Storage { get; set; }
}

public class SecretsManagerSubscriptionDetails
{
    public int? Seats { get; set; }
    public int? MaxAutoScaleSeats { get; set; }
    public int? ServiceAccounts { get; set; }
    public int? MaxAutoScaleServiceAccounts { get; set; }
}
