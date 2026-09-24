using System.Text.Json.Serialization;
using Bit.Core.Billing.Enums;

namespace Bit.Subscriptions.User.Models.Requests;

// Mirrors the legacy { purchase, billingAddress } body so the client can reuse its request builder.
internal record GetOrganizationPurchasePreviewRequest
{
    public PurchaseSelections? Purchase { get; init; }
    public BillingAddressSelections? BillingAddress { get; init; }

    internal record PurchaseSelections
    {
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public ProductTierType Tier { get; init; }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public PlanCadenceType Cadence { get; init; }

        public PasswordManagerSelections? PasswordManager { get; init; }
        public SecretsManagerSelections? SecretsManager { get; init; }
        public string[]? Coupons { get; init; }
    }

    internal record PasswordManagerSelections(int Seats, int AdditionalStorage, bool Sponsored);

    internal record SecretsManagerSelections(int Seats, int AdditionalServiceAccounts, bool Standalone);

    internal record BillingAddressSelections(string? Country, string? PostalCode, TaxIdSelection? TaxId);

    internal record TaxIdSelection(string? Code, string? Value);
}
