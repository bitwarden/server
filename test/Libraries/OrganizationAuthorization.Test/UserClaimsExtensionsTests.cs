using System.Security.Claims;
using Bit.Api.AdminConsole.Authorization;
using Bit.Test.Common.AutoFixture.Attributes;
using Duende.IdentityModel;
using Xunit;

namespace Bit.Api.Test.AdminConsole.Authorization;

public class UserClaimsExtensionsTests
{
    [Theory, BitAutoData]
    public void GetUserId_SubjectClaimIsAGuid_ReturnsUserId(Guid userId)
    {
        var user = new ClaimsPrincipal(
            new ClaimsIdentity([new Claim(JwtClaimTypes.Subject, userId.ToString())]));

        Assert.Equal(userId, user.GetUserId());
    }

    [Fact]
    public void GetUserId_NoSubjectClaim_ReturnsNull()
    {
        Assert.Null(new ClaimsPrincipal().GetUserId());
    }

    [Theory]
    [InlineData("")]
    [InlineData("malformed guid")]
    public void GetUserId_SubjectClaimIsNotAGuid_ReturnsNull(string subject)
    {
        var user = new ClaimsPrincipal(
            new ClaimsIdentity([new Claim(JwtClaimTypes.Subject, subject)]));

        Assert.Null(user.GetUserId());
    }

    /// <summary>
    /// The claim type must stay in sync with ClaimsIdentityOptions.UserIdClaimType, which
    /// AddCustomIdentityServices configures as JwtClaimTypes.Subject.
    /// </summary>
    [Theory, BitAutoData]
    public void GetUserId_OnlyReadsTheSubjectClaim(Guid userId)
    {
        var user = new ClaimsPrincipal(
            new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId.ToString())]));

        Assert.Null(user.GetUserId());
    }
}
