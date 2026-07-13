using Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.InviteLinks;

public class InviteLinkCodeValidatorTests
{
    [Fact]
    public void CodesMatch_SameCode_ReturnsTrue()
    {
        var code = Guid.NewGuid().ToString();
        Assert.True(InviteLinkCodeValidator.CodesMatch(code, code));
    }

    [Fact]
    public void CodesMatch_DifferentCodes_ReturnsFalse()
    {
        Assert.False(InviteLinkCodeValidator.CodesMatch(
            Guid.NewGuid().ToString(),
            Guid.NewGuid().ToString()));
    }

    [Theory]
    [InlineData(null, "some-code")]
    [InlineData("some-code", null)]
    [InlineData(null, null)]
    public void CodesMatch_NullInput_ReturnsFalse(string? provided, string? stored)
    {
        Assert.False(InviteLinkCodeValidator.CodesMatch(provided, stored));
    }

    [Theory]
    [InlineData("", "some-code")]
    [InlineData("some-code", "")]
    [InlineData("", "")]
    public void CodesMatch_EmptyInput_ReturnsFalse(string provided, string stored)
    {
        Assert.False(InviteLinkCodeValidator.CodesMatch(provided, stored));
    }

    [Fact]
    public void CodesMatch_CaseSensitive_ReturnsFalse()
    {
        var code = Guid.NewGuid().ToString();
        Assert.False(InviteLinkCodeValidator.CodesMatch(code, code.ToUpperInvariant()));
    }
}
