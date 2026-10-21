using Bit.Seeder.Data.Enums;

namespace Bit.Seeder.Options;

/// <summary>
/// The Stripe test card and billing address a seeded organization or premium user is billed with.
/// </summary>
/// <remarks>
/// Seat, storage, and Secrets Manager quantities are not part of the profile: they come from the seeded entity,
/// so an organization's subscription follows the seats it was created with, and a premium user is always one
/// seat.
/// </remarks>
public sealed record StripeBillingProfile
{
    /// <summary>
    /// The test card the customer pays with. Its outcome is the subscription's outcome.
    /// </summary>
    public StripeTestCard Card { get; init; } = StripeTestCard.Visa;

    /// <summary>
    /// The customer's billing address and any tax ID.
    /// </summary>
    public StripeTestAddress Address { get; init; } = StripeTestAddress.UnitedStates;

    /// <summary>
    /// Builds a profile from catalog names, such as <c>mastercard</c> and <c>de-vat</c>. A null or empty name
    /// keeps that field's default.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when a name is not in the catalog; the message lists the valid names.</exception>
    public static StripeBillingProfile Parse(string? card, string? address) => new()
    {
        Card = string.IsNullOrEmpty(card) ? StripeTestCard.Visa : StripeTestCard.Parse(card),
        Address = string.IsNullOrEmpty(address) ? StripeTestAddress.UnitedStates : StripeTestAddress.Parse(address),
    };
}
