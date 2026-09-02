using Bit.Core.AdminConsole.Entities;
using Xunit;

namespace Bit.Core.Test.AdminConsole.Entities;

public class OrganizationInviteLinkTests
{
    [Fact]
    public void CodeMatches_SameCode_ReturnsTrue()
    {
        var code = Guid.NewGuid().ToString();
        var link = new OrganizationInviteLink { Code = code };

        Assert.True(link.CodeMatches(code));
    }

    [Fact]
    public void CodeMatches_DifferentCodes_ReturnsFalse()
    {
        var link = new OrganizationInviteLink { Code = Guid.NewGuid().ToString() };

        Assert.False(link.CodeMatches(Guid.NewGuid().ToString()));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void CodeMatches_NullOrEmptyProvidedCode_ReturnsFalse(string? providedCode)
    {
        var link = new OrganizationInviteLink { Code = Guid.NewGuid().ToString() };

        Assert.False(link.CodeMatches(providedCode));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void CodeMatches_NullOrEmptyStoredCode_ReturnsFalse(string? storedCode)
    {
        var link = new OrganizationInviteLink { Code = storedCode! };

        Assert.False(link.CodeMatches("some-code"));
    }

    [Fact]
    public void CodeMatches_CaseSensitive_ReturnsFalse()
    {
        var code = Guid.NewGuid().ToString();
        var link = new OrganizationInviteLink { Code = code };

        Assert.False(link.CodeMatches(code.ToUpperInvariant()));
    }
}
