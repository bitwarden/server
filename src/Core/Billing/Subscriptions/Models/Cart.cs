using System.Text.Json.Serialization;
using Bit.Core.Billing.Enums;
using Bit.Core.Utilities;

namespace Bit.Core.Billing.Subscriptions.Models;

public record CartItem
{
    /// <summary>
    /// The client-side translation key for the name of the cart item.
    /// </summary>
    public required string TranslationKey { get; init; }

    /// <summary>
    /// The quantity of the cart item.
    /// </summary>
    public required long Quantity { get; init; }

    /// <summary>
    /// The unit-cost of the cart item.
    /// </summary>
    public required decimal Cost { get; init; }

    /// <summary>
    /// An optional discount applied specifically to this cart item.
    /// </summary>
    public BitwardenDiscount? Discount { get; init; }
}

public record PasswordManagerCartItems
{
    /// <summary>
    /// The Password Manager seats in the cart.
    /// </summary>
    public required CartItem Seats { get; init; }

    /// <summary>
    /// The additional storage in the cart.
    /// </summary>
    public CartItem? AdditionalStorage { get; init; }
}

public record SecretsManagerCartItems
{
    /// <summary>
    /// The Secrets Manager seats in the cart.
    /// </summary>
    public required CartItem Seats { get; init; }

    /// <summary>
    /// The additional service accounts in the cart.
    /// </summary>
    public CartItem? AdditionalServiceAccounts { get; init; }
}

public record Cart
{
    /// <summary>
    /// The Password Manager items in the cart.
    /// </summary>
    public required PasswordManagerCartItems PasswordManager { get; init; }

    /// <summary>
    /// The Secrets Manager items in the cart.
    /// </summary>
    public SecretsManagerCartItems? SecretsManager { get; init; }

    /// <summary>
    /// The cart's billing cadence.
    /// </summary>
    [JsonConverter(typeof(EnumMemberJsonConverter<PlanCadenceType>))]
    public PlanCadenceType Cadence { get; init; }

    /// <summary>
    /// An optional discount applied to the entire cart.
    /// </summary>
    public BitwardenDiscount? Discount { get; init; }

    /// <summary>
    /// The estimated tax for the cart.
    /// </summary>
    public required decimal EstimatedTax { get; init; }
}
