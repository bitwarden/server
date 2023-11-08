using System.ComponentModel.DataAnnotations;

namespace Bit.Api.Billing.Public.Models;

public class OrganizationSubscriptionUpdateRequestModel
{
    [ValidateAtLeastOneNotNull]
    public PasswordManager PasswordManager { get; set; }
    [ValidateAtLeastOneNotNull]
    public SecretsManager SecretsManager { get; set; }
}

public class PasswordManager
{
    [Range(0, int.MaxValue, ErrorMessage = "Seats cannot be negative.")]
    public int Seats { get; set; }
    [Range(0, int.MaxValue, ErrorMessage = "Storage cannot be negative.")]
    public short Storage { get; set; }
    [Range(0, int.MaxValue, ErrorMessage = "MaxAutoScaleSeats cannot be negative.")]
    public int? MaxAutoScaleSeats { get; set; }
    public PasswordManager(int seats, short storage, int? maxAutoScaleSeats)
    {
        Seats = seats;
        Storage = storage;
        MaxAutoScaleSeats = maxAutoScaleSeats;
    }
}

public class SecretsManager
{
    [Range(0, int.MaxValue, ErrorMessage = "Seats cannot be negative.")]
    public int Seats { get; set; }
    [Range(0, int.MaxValue, ErrorMessage = "MaxAutoScaleSeats cannot be negative.")]
    public int? MaxAutoScaleSeats { get; set; }
    [Range(0, int.MaxValue, ErrorMessage = "ServiceAccounts cannot be negative.")]
    public int ServiceAccounts { get; set; }
    [Range(0, int.MaxValue, ErrorMessage = "MaxAutoScaleServiceAccounts cannot be negative.")]
    public int? MaxAutoScaleServiceAccounts { get; set; }
    public SecretsManager(int seats, int? maxAutoScaleSeats, int serviceAccounts, int? maxAutoScaleServiceAccounts)
    {
        Seats = seats;
        MaxAutoScaleSeats = maxAutoScaleSeats;
        ServiceAccounts = serviceAccounts;
        MaxAutoScaleServiceAccounts = maxAutoScaleServiceAccounts;
    }
}

[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public class ValidateAtLeastOneNotNullAttribute : ValidationAttribute
{
    protected override ValidationResult IsValid(object value, ValidationContext validationContext)
    {
        var passwordManager = validationContext.ObjectType.GetProperty("PasswordManager")?.GetValue(validationContext.ObjectInstance);
        var secretsManager = validationContext.ObjectType.GetProperty("SecretsManager")?.GetValue(validationContext.ObjectInstance);

        if (passwordManager == null && secretsManager == null)
        {
            return new ValidationResult("At least one of PasswordManager or SecretsManager must be provided.");
        }

        return ValidationResult.Success;
    }
}


