using Bit.Core.AdminConsole.Utilities;
using Xunit;

namespace Bit.Core.Test.AdminConsole.Utilities;

public class InviteLinkDomainValidatorTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("notanemail")]
    [InlineData("@nodomain")]
    public void IsEmailDomainAllowed_InvalidEmail_ReturnsFalse(string? email)
    {
        Assert.False(InviteLinkDomainValidator.IsEmailDomainAllowed(email, ["acme.com"]));
    }

    [Fact]
    public void IsEmailDomainAllowed_EmptyDomainList_ReturnsFalse()
    {
        Assert.False(InviteLinkDomainValidator.IsEmailDomainAllowed("user@acme.com", []));
    }

    [Fact]
    public void IsEmailDomainAllowed_DomainNotInList_ReturnsFalse()
    {
        Assert.False(InviteLinkDomainValidator.IsEmailDomainAllowed("user@other.com", ["acme.com"]));
    }

    [Theory]
    [InlineData("user@acme.com", "acme.com")]     // exact match
    [InlineData("user@ACME.COM", "acme.com")]     // email domain mixed case
    [InlineData("user@acme.com", "ACME.COM")]     // allowed domain mixed case
    public void IsEmailDomainAllowed_MatchingDomain_ReturnsTrue(string email, string allowedDomain)
    {
        Assert.True(InviteLinkDomainValidator.IsEmailDomainAllowed(email, [allowedDomain]));
    }
}
