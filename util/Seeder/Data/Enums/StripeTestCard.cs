namespace Bit.Seeder.Data.Enums;

/// <summary>
/// The Stripe test card a seeded organization or premium user pays with.
/// </summary>
/// <remarks>
/// Cards that simulate issuer declines aren't listed: Stripe refuses to attach them to a customer, so customer
/// creation fails before any state is seeded.
/// </remarks>
public enum StripeTestCard
{
    /// <summary>
    /// Visa 4242 4242 4242 4242 (<c>pm_card_visa</c>). Every charge succeeds.
    /// </summary>
    Visa,

    /// <summary>
    /// Mastercard 5555 5555 5555 4444 (<c>pm_card_mastercard</c>). Every charge succeeds.
    /// </summary>
    Mastercard,

    /// <summary>
    /// American Express 3714 496353 98431 (<c>pm_card_amex</c>). Every charge succeeds.
    /// </summary>
    AmericanExpress,

    /// <summary>
    /// Visa 4000 0000 0000 0341 (<c>pm_card_chargeCustomerFail</c>). Attaches to the customer, then every charge
    /// fails, so a subscription that charges at creation comes back <c>incomplete</c>.
    /// </summary>
    FailsOnCharge,

    /// <summary>
    /// Visa 4000 0027 6000 3184 (<c>pm_card_authenticationRequired</c>). Stripe requires 3D Secure authentication
    /// on every payment.
    /// </summary>
    RequiresAuthentication,
}

/// <summary>
/// Gives each <see cref="StripeTestCard"/> its catalog name and Stripe test payment method ID.
/// </summary>
/// <remarks>
/// The switch has no discard arm on purpose: CS8509 is a build error in this repo, so a card added without an
/// entry here fails to compile.
/// </remarks>
internal static class StripeTestCardCatalog
{
    extension(StripeTestCard card)
    {
        /// <summary>
        /// The name callers select the card by, such as <c>fails-on-charge</c>.
        /// </summary>
        internal string Name => card.Entry.Name;

        /// <summary>
        /// The Stripe test payment method ID, such as <c>pm_card_visa</c>.
        /// </summary>
        internal string PaymentMethodId => card.Entry.PaymentMethodId;

        private (string Name, string PaymentMethodId) Entry => card switch
        {
            StripeTestCard.Visa => ("visa", "pm_card_visa"),
            StripeTestCard.Mastercard => ("mastercard", "pm_card_mastercard"),
            StripeTestCard.AmericanExpress => ("amex", "pm_card_amex"),
            StripeTestCard.FailsOnCharge => ("fails-on-charge", "pm_card_chargeCustomerFail"),
            StripeTestCard.RequiresAuthentication => ("requires-authentication", "pm_card_authenticationRequired"),
        };
    }

    extension(StripeTestCard)
    {
        /// <summary>
        /// Parses a catalog name, ignoring case.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when no card has that name; the message lists every valid name.</exception>
        internal static StripeTestCard Parse(string name)
        {
            foreach (var card in Enum.GetValues<StripeTestCard>())
            {
                if (string.Equals(card.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return card;
                }
            }

            throw new ArgumentException(
                $"Unknown Stripe test card '{name}'. Use: {string.Join(", ", Enum.GetValues<StripeTestCard>().Select(c => c.Name))}");
        }
    }
}
