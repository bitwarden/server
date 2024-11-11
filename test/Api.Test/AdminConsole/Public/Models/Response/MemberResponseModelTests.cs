using Bit.Api.AdminConsole.Public.Models.Response;
using Bit.Core.Entities;
using Bit.Core.Models.Data;
using NSubstitute;
using Xunit;

namespace Bit.Api.Test.AdminConsole.Public.Models.Response;


public class MemberResponseModelTests
{
    [Fact]
    public void ResetPasswordEnrolled_ShouldBeTrue_WhenUserHasResetPasswordKey()
    {
        // Arrange
        var user = Substitute.For<OrganizationUser>();
        var collections = Substitute.For<IEnumerable<CollectionAccessSelection>>();
        user.ResetPasswordKey = "none-empty";


        // Act
        var sut = new MemberResponseModel(user, collections);

        // Assert
        Assert.True(sut.ResetPasswordEnrolled);
    }

    [Fact]
    public void ResetPasswordEnrolled_ShouldBeFalse_WhenUserDoesNotHaveResetPasswordKey()
    {
        // Arrange
        var user = Substitute.For<OrganizationUser>();
        var collections = Substitute.For<IEnumerable<CollectionAccessSelection>>();

        // Act
        var sut = new MemberResponseModel(user, collections);

        // Assert
        Assert.False(sut.ResetPasswordEnrolled);
    }
}
