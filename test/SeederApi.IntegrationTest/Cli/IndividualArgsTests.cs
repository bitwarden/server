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
    public void ToOptions_StripeBillingOnPremium_MapsTheFlag()
    {
        var args = new IndividualArgs { Subscription = "premium", StripeBilling = true };
        args.Validate();

        Assert.True(args.ToOptions().StripeBilling);
    }
}
