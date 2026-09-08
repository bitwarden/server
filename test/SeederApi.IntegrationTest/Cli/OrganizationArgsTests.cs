using Bit.SeederUtility.Commands;
using Xunit;

namespace Bit.SeederApi.IntegrationTest.Cli;

public class OrganizationArgsTests
{
    [Theory]
    [InlineData("noatsign.example")]
    [InlineData("just-text")]
    public void Validate_OwnerEmailWithoutAtSign_Throws(string badEmail)
    {
        var args = BaseArgs();
        args.OwnerEmail = badEmail;
        var ex = Assert.Throws<ArgumentException>(args.Validate);
        Assert.Contains("--owner-email", ex.Message);
    }

    [Theory]
    [InlineData("ok@example.com")]
    [InlineData(null)]
    [InlineData("")]
    public void Validate_OwnerEmailValidOrEmpty_Passes(string? email)
    {
        var args = BaseArgs();
        args.OwnerEmail = email;
        args.Validate();
    }

    [Fact]
    public void ToOptions_PropagatesOwnerEmail()
    {
        var args = BaseArgs();
        args.OwnerEmail = "specific@bw.example";
        var options = args.ToOptions();
        Assert.Equal("specific@bw.example", options.OwnerEmail);
    }

    [Fact]
    public void ToOptions_NullOwnerEmail_StaysNull()
    {
        var args = BaseArgs();
        args.OwnerEmail = null;
        var options = args.ToOptions();
        Assert.Null(options.OwnerEmail);
    }

    [Fact]
    public void Validate_StripeBillingOnFreePlan_Throws()
    {
        var args = BaseArgs();
        args.PlanType = "free";
        args.StripeBilling = true;

        var ex = Assert.Throws<ArgumentException>(args.Validate);
        Assert.Contains("Free plan", ex.Message);
    }

    [Fact]
    public void Validate_StripeBillingOnPaidPlan_Passes()
    {
        var args = BaseArgs();
        args.PlanType = "teams-monthly";
        args.StripeBilling = true;

        args.Validate();
    }

    [Theory]
    [InlineData(true, null)]
    [InlineData(false, 7)]
    [InlineData(true, 7)]
    public void Validate_TrialFlagsWithoutStripeBilling_Throws(bool skipTrial, int? trialDays)
    {
        var args = BaseArgs();
        args.SkipTrial = skipTrial;
        args.TrialDays = trialDays;

        var ex = Assert.Throws<ArgumentException>(args.Validate);
        Assert.Contains("--stripe-billing", ex.Message);
    }

    [Fact]
    public void Validate_SkipTrialAndTrialDaysTogether_Throws()
    {
        var args = BaseArgs();
        args.StripeBilling = true;
        args.SkipTrial = true;
        args.TrialDays = 7;

        var ex = Assert.Throws<ArgumentException>(args.Validate);
        Assert.Contains("mutually exclusive", ex.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(31)]
    public void Validate_TrialDaysOutOfRange_Throws(int trialDays)
    {
        var args = BaseArgs();
        args.StripeBilling = true;
        args.TrialDays = trialDays;

        var ex = Assert.Throws<ArgumentException>(args.Validate);
        Assert.Contains("between 1 and 30", ex.Message);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(30)]
    public void Validate_TrialDaysAtRangeBoundaries_Passes(int trialDays)
    {
        var args = BaseArgs();
        args.StripeBilling = true;
        args.TrialDays = trialDays;

        args.Validate();
    }

    [Fact]
    public void ToOptions_WithoutStripeBilling_LeavesBillingNull()
    {
        // The zero-Stripe-calls default: null is what every seeding path reads as "no billing".
        Assert.Null(BaseArgs().ToOptions().StripeBilling);
    }

    [Fact]
    public void ToOptions_StripeBillingDefaults_ThirtyDayTrial()
    {
        var args = BaseArgs();
        args.StripeBilling = true;

        var billing = args.ToOptions().StripeBilling;

        Assert.NotNull(billing);
        Assert.False(billing.SkipTrial);
        Assert.Equal(30, billing.TrialDays);
    }

    [Fact]
    public void ToOptions_StripeBillingWithTrialDays_MapsTheValue()
    {
        var args = BaseArgs();
        args.StripeBilling = true;
        args.TrialDays = 14;

        var billing = args.ToOptions().StripeBilling;

        Assert.NotNull(billing);
        Assert.Equal(14, billing.TrialDays);
    }

    [Fact]
    public void ToOptions_SkipTrial_MapsTheFlag()
    {
        var args = BaseArgs();
        args.StripeBilling = true;
        args.SkipTrial = true;

        var billing = args.ToOptions().StripeBilling;

        Assert.NotNull(billing);
        Assert.True(billing.SkipTrial);
    }

    private static OrganizationArgs BaseArgs() => new()
    {
        Name = "Org",
        Domain = "demo.example",
        Users = 1,
        PlanType = "enterprise-annually",
        KdfIterations = 5_000,
    };
}
