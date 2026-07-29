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
}
