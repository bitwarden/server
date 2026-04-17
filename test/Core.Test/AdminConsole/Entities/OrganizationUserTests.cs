using Bit.Core.Entities;
using Xunit;

namespace Bit.Core.Test.AdminConsole.Entities;

public class OrganizationUserTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void IsValidResetPasswordKey_InvalidKeys_ReturnsFalse(string? resetPasswordKey)
    {
        Assert.False(OrganizationUser.IsValidResetPasswordKey(resetPasswordKey));
    }

    [Fact]
    public void IsValidResetPasswordKey_ValidKey_ReturnsTrue()
    {
        Assert.True(OrganizationUser.IsValidResetPasswordKey("validKey"));
    }

    [Fact]
    public void IsEnrolledInAccountRecovery_NullKey_ReturnsFalse()
    {
        var orgUser = new OrganizationUser { ResetPasswordKey = null };

        Assert.False(orgUser.IsEnrolledInAccountRecovery());
    }

    [Fact]
    public void IsEnrolledInAccountRecovery_ValidKey_ReturnsTrue()
    {
        var orgUser = new OrganizationUser { ResetPasswordKey = "validKey" };

        Assert.True(orgUser.IsEnrolledInAccountRecovery());
    }
}
