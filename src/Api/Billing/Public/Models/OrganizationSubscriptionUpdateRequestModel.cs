using System.ComponentModel.DataAnnotations;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Models.Business;

namespace Bit.Api.Billing.Public.Models;

public class OrganizationSubscriptionUpdateRequestModel : IValidatableObject
{
    public PasswordManagerSubscriptionUpdateRequestModel PasswordManagerSubscriptionUpdateRequestModel { get; set; }
    public SecretsManagerSubscriptionUpdateRequestModel SecretsManagerSubscriptionUpdateRequestModel { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        /*Note: You need to specify at least one of the properties ('PasswordManager' or 'SecretsManager').
        If both properties are specified, both will be updated but If both properties not specified it will return validation error message*/

        // Retrieve the 'PasswordManager' property value from the validation context object.
        var passwordManager = validationContext.ObjectType.GetProperty("PasswordManager")?.GetValue(validationContext.ObjectInstance);
        // Retrieve the 'SecretsManager' property value from the validation context object.
        var secretsManager = validationContext.ObjectType.GetProperty("SecretsManager")?.GetValue(validationContext.ObjectInstance);

        if (passwordManager == null && secretsManager == null)
        {
            yield return new ValidationResult("At least one of PasswordManager or SecretsManager must be provided.");
        }

        yield return ValidationResult.Success;
    }
}

public class PasswordManagerSubscriptionUpdateRequestModel
{
    [Range(0, int.MaxValue, ErrorMessage = "Seats cannot be negative.")]
    public int Seats { get; set; }
    [Range(0, int.MaxValue, ErrorMessage = "Storage cannot be negative.")]
    public short? Storage { get; set; }
    [Range(0, int.MaxValue, ErrorMessage = "MaxAutoScaleSeats cannot be negative.")]
    public int? MaxAutoScaleSeats { get; set; }
    public PasswordManagerSubscriptionUpdateRequestModel(int seats, short storage, int? maxAutoScaleSeats)
    {
        Seats = seats;
        Storage = storage;
        MaxAutoScaleSeats = maxAutoScaleSeats;
    }
}

public class SecretsManagerSubscriptionUpdateRequestModel
{
    [Range(0, int.MaxValue, ErrorMessage = "Seats cannot be negative.")]
    public int Seats { get; set; }
    [Range(0, int.MaxValue, ErrorMessage = "MaxAutoScaleSeats cannot be negative.")]
    public int? MaxAutoScaleSeats { get; set; }
    [Range(0, int.MaxValue, ErrorMessage = "ServiceAccounts cannot be negative.")]
    public int ServiceAccounts { get; set; }
    [Range(0, int.MaxValue, ErrorMessage = "MaxAutoScaleServiceAccounts cannot be negative.")]
    public int? MaxAutoScaleServiceAccounts { get; set; }

    public virtual SecretsManagerSubscriptionUpdate ToSecretsManagerSubscriptionUpdate(Organization organization)
    {
        return new SecretsManagerSubscriptionUpdate(organization, false)
        {
            MaxAutoscaleSmSeats = MaxAutoScaleSeats,
            MaxAutoscaleSmServiceAccounts = MaxAutoScaleServiceAccounts
        }
            .AdjustSeats(Seats)
            .AdjustServiceAccounts(ServiceAccounts);
    }
}



