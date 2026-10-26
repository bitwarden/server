using Bit.Seeder.Data.Enums;
using Bit.Seeder.Options;
using Xunit;

namespace Bit.SeederApi.IntegrationTest.Data;

public class StripeTestCatalogTests
{
    public static TheoryData<StripeTestCard> Cards => new(Enum.GetValues<StripeTestCard>());

    public static TheoryData<StripeTestAddress> Addresses => new(Enum.GetValues<StripeTestAddress>());

    [Theory]
    [MemberData(nameof(Cards))]
    public void Cards_NameParsesBackToItsMember(StripeTestCard card)
    {
        // Also catches two cards sharing a name: Parse would return the first for both.
        Assert.Equal(card, StripeTestCard.Parse(card.Name));
    }

    [Theory]
    [MemberData(nameof(Addresses))]
    public void Addresses_NameParsesBackToItsMember(StripeTestAddress address)
    {
        Assert.Equal(address, StripeTestAddress.Parse(address.Name));
    }

    [Fact]
    public void Cards_UnknownName_ThrowsListingEveryValidName()
    {
        var ex = Assert.Throws<ArgumentException>(() => StripeTestCard.Parse("bogus"));

        Assert.Contains("'bogus'", ex.Message);
        Assert.All(Enum.GetValues<StripeTestCard>(), c => Assert.Contains(c.Name, ex.Message));
    }

    [Fact]
    public void Addresses_UnknownName_ThrowsListingEveryValidName()
    {
        var ex = Assert.Throws<ArgumentException>(() => StripeTestAddress.Parse("bogus"));

        Assert.Contains("'bogus'", ex.Message);
        Assert.All(Enum.GetValues<StripeTestAddress>(), a => Assert.Contains(a.Name, ex.Message));
    }

    [Fact]
    public void Profile_ParseWithNoNames_IsVisaAtTheUsAddress()
    {
        var profile = StripeBillingProfile.Parse(null, "");

        Assert.Equal(new StripeBillingProfile(), profile);
        Assert.Equal("pm_card_visa", profile.Card.PaymentMethodId);
        Assert.Equal(("US", "43432", (string?)null), (profile.Address.Country, profile.Address.PostalCode, profile.Address.TaxId));
    }

    [Fact]
    public void Profile_ParseWithNames_SelectsThoseMembers()
    {
        var profile = StripeBillingProfile.Parse("fails-on-charge", "de-vat");

        Assert.Equal(StripeTestCard.FailsOnCharge, profile.Card);
        Assert.Equal(StripeTestAddress.GermanyWithVatId, profile.Address);
    }
}
