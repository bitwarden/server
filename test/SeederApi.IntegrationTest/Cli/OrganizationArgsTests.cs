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

    private static OrganizationArgs BaseArgs() => new()
    {
        Name = "Org",
        Domain = "demo.example",
        Users = 1,
        PlanType = "enterprise-annually",
        KdfIterations = 5_000,
    };
}
