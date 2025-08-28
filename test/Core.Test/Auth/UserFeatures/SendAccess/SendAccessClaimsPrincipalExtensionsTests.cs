using System.Security.Claims;
using Bit.Core.Auth.UserFeatures.SendAccess;
using Bit.Core.Identity;
using Xunit;

namespace Bit.Core.Test.Auth.UserFeatures.SendAccess;

public class SendAccessClaimsPrincipalExtensionsTests
{
    [Fact]
    public void GetSendId_ReturnsGuid_WhenClaimIsPresentAndValid()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var claims = new[] { new Claim(Claims.SendAccessClaims.SendId, guid.ToString()) };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims));

        // Act
        var result = principal.GetSendId();

        // Assert
        Assert.Equal(guid, result);
    }

    [Fact]
    public void GetSendId_ThrowsInvalidOperationException_WhenClaimIsMissing()
    {
        // Arrange
        var principal = new ClaimsPrincipal(new ClaimsIdentity());

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => principal.GetSendId());
        Assert.Equal("Send ID claim not found.", ex.Message);
    }

    [Fact]
    public void GetSendId_ThrowsInvalidOperationException_WhenClaimValueIsInvalid()
    {
        // Arrange
        var claims = new[] { new Claim(Claims.SendAccessClaims.SendId, "not-a-guid") };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims));

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => principal.GetSendId());
        Assert.Equal("Invalid Send ID claim value.", ex.Message);
    }

    [Fact]
    public void GetSendId_ThrowsArgumentNullException_WhenPrincipalIsNull()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => SendAccessClaimsPrincipalExtensions.GetSendId(null));
    }
}
