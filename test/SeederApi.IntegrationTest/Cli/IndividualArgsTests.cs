using Bit.Seeder.Data.Enums;
using Bit.Seeder.Options;
using Bit.SeederUtility.Commands;
using Xunit;

namespace Bit.SeederApi.IntegrationTest.Cli;

public class IndividualArgsTests
{
    [Fact]
    public void Validate_StripeBillingWithFreeSubscription_Throws()
    {
        var args = new IndividualArgs { Subscription = "free", StripeBilling = true };

        var ex = Assert.Throws<ArgumentException>(args.Validate);
        Assert.Contains("--subscription premium", ex.Message);
    }

    [Fact]
    public void Validate_StripeBillingWithSelfHosted_Throws()
    {
        var args = new IndividualArgs { Subscription = "premium", StripeBilling = true, SelfHosted = true };

        var ex = Assert.Throws<ArgumentException>(args.Validate);
        Assert.Contains("--self-hosted", ex.Message);
    }

    [Fact]
    public void ToOptions_StripeBillingOnPremium_MapsTheDefaultProfile()
    {
        var args = new IndividualArgs { Subscription = "premium", StripeBilling = true };
        args.Validate();

        Assert.Equal(new StripeBillingProfile(), args.ToOptions().StripeBilling);
    }

    [Fact]
    public void ToOptions_WithoutStripeBilling_LeavesBillingNull()
    {
        var args = new IndividualArgs { Subscription = "premium" };
        args.Validate();

        Assert.Null(args.ToOptions().StripeBilling);
    }

    [Theory]
    [InlineData("visa", null)]
    [InlineData(null, "de")]
    public void Validate_CardOrAddressWithoutStripeBilling_Throws(string? card, string? address)
    {
        var args = new IndividualArgs { Subscription = "premium", StripeCard = card, StripeAddress = address };

        var ex = Assert.Throws<ArgumentException>(args.Validate);
        Assert.Contains("--stripe-billing", ex.Message);
    }

    [Theory]
    [InlineData("bogus", null)]
    [InlineData(null, "bogus")]
    public void Validate_UnknownCardOrAddress_Throws(string? card, string? address)
    {
        var args = new IndividualArgs
        {
            Subscription = "premium",
            StripeBilling = true,
            StripeCard = card,
            StripeAddress = address,
        };

        var ex = Assert.Throws<ArgumentException>(args.Validate);
        Assert.Contains("'bogus'", ex.Message);
    }

    [Fact]
    public void ToOptions_CardAndAddress_MapIntoTheProfile()
    {
        var args = new IndividualArgs
        {
            Subscription = "premium",
            StripeBilling = true,
            StripeCard = "mastercard",
            StripeAddress = "de",
        };
        args.Validate();

        Assert.Equal(
            new StripeBillingProfile { Card = StripeTestCard.Mastercard, Address = StripeTestAddress.Germany },
            args.ToOptions().StripeBilling);
    }
}
