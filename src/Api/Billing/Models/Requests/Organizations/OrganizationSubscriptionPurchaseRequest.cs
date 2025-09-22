using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Organizations.Models;

namespace Bit.Api.Billing.Models.Requests.Organizations;

public record OrganizationSubscriptionPurchaseRequest : IValidatableObject
{
    [Required]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ProductTierType Tier { get; set; }

    [Required]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public PlanCadenceType Cadence { get; set; }

    [Required]
    public required PasswordManagerPurchaseSelections PasswordManager { get; set; }

    public SecretsManagerPurchaseSelections? SecretsManager { get; set; }

    public OrganizationSubscriptionPurchase ToDomain() => new()
    {
        Tier = Tier,
        Cadence = Cadence,
        PasswordManager = new OrganizationSubscriptionPurchase.PasswordManagerSelections
        {
            Seats = PasswordManager.Seats,
            AdditionalStorage = PasswordManager.AdditionalStorage,
            Sponsored = PasswordManager.Sponsored
        },
        SecretsManager = SecretsManager != null ? new OrganizationSubscriptionPurchase.SecretsManagerSelections
        {
            Seats = SecretsManager.Seats,
            AdditionalServiceAccounts = SecretsManager.AdditionalServiceAccounts,
            Standalone = SecretsManager.Standalone
        } : null
    };

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Tier != ProductTierType.Families)
        {
            yield break;
        }

        if (Cadence == PlanCadenceType.Monthly)
        {
            yield return new ValidationResult("Monthly cadence is not available on the Families plan.");
        }

        if (SecretsManager != null)
        {
            yield return new ValidationResult("Secrets Manager is not available on the Families plan.");
        }
    }

    public record PasswordManagerPurchaseSelections
    {
        [Required]
        [Range(1, 100000, ErrorMessage = "Password Manager seats must be between 1 and 100,000")]
        public int Seats { get; set; }

        [Required]
        [Range(0, 99, ErrorMessage = "Additional storage must be between 0 and 99 GB")]
        public int AdditionalStorage { get; set; }

        public bool Sponsored { get; set; } = false;
    }

    public record SecretsManagerPurchaseSelections
    {
        [Required]
        [Range(1, 100000, ErrorMessage = "Secrets Manager seats must be between 1 and 100,000")]
        public int Seats { get; set; }

        [Required]
        [Range(0, 100000, ErrorMessage = "Additional service accounts must be between 0 and 100,000")]
        public int AdditionalServiceAccounts { get; set; }

        public bool Standalone { get; set; } = false;
    }
}
