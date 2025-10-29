using System.ComponentModel.DataAnnotations;
using Bit.Core.Billing.Organizations.Models;

namespace Bit.Api.Billing.Models.Requests.Organizations;

public record OrganizationSubscriptionUpdateRequest
{
    public PasswordManagerUpdateSelections? PasswordManager { get; set; }
    public SecretsManagerUpdateSelections? SecretsManager { get; set; }

    public OrganizationSubscriptionUpdate ToDomain() => new()
    {
        PasswordManager =
            PasswordManager != null
                ? new OrganizationSubscriptionUpdate.PasswordManagerSelections
                {
                    Seats = PasswordManager.Seats,
                    AdditionalStorage = PasswordManager.AdditionalStorage
                }
                : null,
        SecretsManager =
            SecretsManager != null
                ? new OrganizationSubscriptionUpdate.SecretsManagerSelections
                {
                    Seats = SecretsManager.Seats,
                    AdditionalServiceAccounts = SecretsManager.AdditionalServiceAccounts
                }
                : null
    };

    public record PasswordManagerUpdateSelections
    {
        [Range(1, 100000, ErrorMessage = "Password Manager seats must be between 1 and 100,000")]
        public int? Seats { get; set; }

        [Range(0, 99, ErrorMessage = "Additional storage must be between 0 and 99 GB")]
        public int? AdditionalStorage { get; set; }
    }

    public record SecretsManagerUpdateSelections
    {
        [Range(0, 100000, ErrorMessage = "Secrets Manager seats must be between 0 and 100,000")]
        public int? Seats { get; set; }

        [Range(0, 100000, ErrorMessage = "Additional service accounts must be between 0 and 100,000")]
        public int? AdditionalServiceAccounts { get; set; }
    }
}
