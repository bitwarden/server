namespace Bit.Core.Billing.Organizations.Models;

public record OrganizationSubscriptionUpdate
{
    public PasswordManagerSelections? PasswordManager { get; init; }
    public SecretsManagerSelections? SecretsManager { get; init; }

    public record PasswordManagerSelections
    {
        public int? Seats { get; init; }
        public int? AdditionalStorage { get; init; }
    }

    public record SecretsManagerSelections
    {
        public int? Seats { get; init; }
        public int? AdditionalServiceAccounts { get; init; }
    }
}
