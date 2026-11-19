namespace Bit.Seeder.Data.Enums;

/// <summary>
/// The billing address, and any tax ID, a seeded organization or premium user is billed at.
/// </summary>
/// <remarks>
/// Named by place rather than by tax outcome: the tax Stripe charges depends on the shared test account's tax
/// registrations, not on the address alone.
/// </remarks>
public enum StripeTestAddress
{
    /// <summary>
    /// United States, postal code 43432.
    /// </summary>
    UnitedStates,

    /// <summary>
    /// United States, postal code 99901 (Alaska).
    /// </summary>
    Alaska,

    /// <summary>
    /// Germany, postal code 10117.
    /// </summary>
    Germany,

    /// <summary>
    /// Germany, postal code 10117, with VAT ID <c>DE123456789</c>. Organizations only: a personal account takes
    /// no tax ID.
    /// </summary>
    GermanyWithVatId,

    /// <summary>
    /// Philippines, postal code 1640.
    /// </summary>
    Philippines,
}

/// <summary>
/// Gives each <see cref="StripeTestAddress"/> its catalog name and the address sent to Stripe.
/// </summary>
/// <remarks>
/// The switch has no discard arm on purpose: CS8509 is a build error in this repo, so an address added without
/// an entry here fails to compile.
/// </remarks>
internal static class StripeTestAddressCatalog
{
    extension(StripeTestAddress address)
    {
        /// <summary>
        /// The name callers select the address by, such as <c>de-vat</c>.
        /// </summary>
        internal string Name => address.Entry.Name;

        /// <summary>
        /// The billing country as an ISO 3166-1 alpha-2 code, such as <c>US</c>.
        /// </summary>
        internal string Country => address.Entry.Country;

        /// <summary>
        /// The billing postal code.
        /// </summary>
        internal string PostalCode => address.Entry.PostalCode;

        /// <summary>
        /// The tax ID billed with the address, or null when it has none.
        /// </summary>
        internal string? TaxId => address.Entry.TaxId;

        private (string Name, string Country, string PostalCode, string? TaxId) Entry => address switch
        {
            StripeTestAddress.UnitedStates => ("us", "US", "43432", null),
            StripeTestAddress.Alaska => ("us-alaska", "US", "99901", null),
            StripeTestAddress.Germany => ("de", "DE", "10117", null),
            StripeTestAddress.GermanyWithVatId => ("de-vat", "DE", "10117", "DE123456789"),
            StripeTestAddress.Philippines => ("ph", "PH", "1640", null),
        };
    }

    extension(StripeTestAddress)
    {
        /// <summary>
        /// Parses a catalog name, ignoring case.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when no address has that name; the message lists every valid name.</exception>
        internal static StripeTestAddress Parse(string name)
        {
            foreach (var address in Enum.GetValues<StripeTestAddress>())
            {
                if (string.Equals(address.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return address;
                }
            }

            throw new ArgumentException(
                $"Unknown Stripe test address '{name}'. Use: {string.Join(", ", Enum.GetValues<StripeTestAddress>().Select(a => a.Name))}");
        }
    }
}
