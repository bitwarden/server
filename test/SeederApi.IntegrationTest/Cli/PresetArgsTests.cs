using Bit.SeederUtility.Commands;
using Xunit;

namespace Bit.SeederApi.IntegrationTest.Cli;

public class PresetArgsTests
{
    [Theory]
    [InlineData("noatsign.example")]
    [InlineData("just-text")]
    public void Validate_OwnerEmailWithoutAtSign_Throws(string badEmail)
    {
        var args = new PresetArgs { Name = "any", OwnerEmail = badEmail };
        var ex = Assert.Throws<ArgumentException>(args.Validate);
        Assert.Contains("--owner-email", ex.Message);
    }

    [Theory]
    [InlineData("ok@example.com")]
    [InlineData("with+tag@bitwarden.example")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_OwnerEmailValidOrEmpty_Passes(string? email)
    {
        var args = new PresetArgs { Name = "any", OwnerEmail = email };
        args.Validate();
    }

    [Fact]
    public void Validate_OrgNameUnvalidated_Passes()
    {
        // OrgName has no format constraints — anything (including null/empty) is accepted.
        var args = new PresetArgs { Name = "any", OrgName = "" };
        args.Validate();

        args = new PresetArgs { Name = "any", OrgName = "Anything goes 🎉" };
        args.Validate();
    }

    [Theory]
    [InlineData(true, null)]
    [InlineData(false, 7)]
    [InlineData(true, 7)]
    public void Validate_TrialFlagsWithoutStripeBilling_Throws(bool skipTrial, int? trialDays)
    {
        var args = new PresetArgs { Name = "any", SkipTrial = skipTrial, TrialDays = trialDays };

        var ex = Assert.Throws<ArgumentException>(args.Validate);
        Assert.Contains("--stripe-billing", ex.Message);
    }

    [Fact]
    public void Validate_SkipTrialAndTrialDaysTogether_Throws()
    {
        var args = new PresetArgs { Name = "any", StripeBilling = true, SkipTrial = true, TrialDays = 7 };

        var ex = Assert.Throws<ArgumentException>(args.Validate);
        Assert.Contains("mutually exclusive", ex.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(31)]
    public void Validate_TrialDaysOutOfRange_Throws(int trialDays)
    {
        var args = new PresetArgs { Name = "any", StripeBilling = true, TrialDays = trialDays };

        var ex = Assert.Throws<ArgumentException>(args.Validate);
        Assert.Contains("between 1 and 30", ex.Message);
    }

    [Fact]
    public void Validate_TrialFlagsWithListAndNoName_StillThrows()
    {
        // --list short-circuits the rest of Validate, so the trial checks have to precede it or
        // `--list --trial-days 99` would pass silently.
        var args = new PresetArgs { List = true, TrialDays = 99 };

        Assert.Throws<ArgumentException>(args.Validate);
    }

    [Fact]
    public void Validate_StripeBillingWithTrialDays_Passes()
    {
        var args = new PresetArgs { Name = "any", StripeBilling = true, TrialDays = 14 };
        args.Validate();
    }

    [Fact]
    public void ToStripeBillingOptions_WithoutFlag_IsNull()
    {
        Assert.Null(new PresetArgs { Name = "any" }.ToStripeBillingOptions());
    }

    [Fact]
    public void ToStripeBillingOptions_WithFlag_MapsTrialConfiguration()
    {
        var options = new PresetArgs { Name = "any", StripeBilling = true, TrialDays = 14 }
            .ToStripeBillingOptions();

        Assert.NotNull(options);
        Assert.False(options.SkipTrial);
        Assert.Equal(14, options.TrialDays);
    }

    [Fact]
    public void ToStripeBillingOptions_SkipTrial_DefaultsTrialDaysUnused()
    {
        var options = new PresetArgs { Name = "any", StripeBilling = true, SkipTrial = true }
            .ToStripeBillingOptions();

        Assert.NotNull(options);
        Assert.True(options.SkipTrial);
    }
}
