using Bit.Api.Auth.Models.Response.TwoFactor;
using Bit.Core.Entities;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Api.Test.Auth.Models.Response;

public class UserTwoFactorDuoResponseModelTests
{
    [Theory]
    [BitAutoData]
    public void User_WithDuo_UserNull_ThrowsArgumentException(User user)
    {
        // Arrange
        user.TwoFactorProviders = GetTwoFactorDuoProvidersJson();

        // Act
        try
        {
            var model = new TwoFactorDuoResponseModel(null as User);
        }
        catch (ArgumentNullException e)
        {
            // Assert
            Assert.Equal("Value cannot be null. (Parameter 'user')", e.Message);
        }
    }

    [Theory]
    [BitAutoData]
    public void User_WithDuo_ShouldBuildModel(User user)
    {
        // Arrange
        user.TwoFactorProviders = GetTwoFactorDuoProvidersJson();

        // Act
        var model = new TwoFactorDuoResponseModel(user);

        // Assert
        Assert.NotNull(model);
        Assert.Equal("clientId", model.ClientId);
        Assert.Equal("secret************", model.ClientSecret);
    }

    [Theory]
    [BitAutoData]
    public void User_WithDuoEmpty_ShouldFail(User user)
    {
        // Arrange
        user.TwoFactorProviders = "{\"2\" : {}}";

        // Act
        var model = new TwoFactorDuoResponseModel(user);

        /// Assert
        Assert.False(model.Enabled);
    }

    [Theory]
    [BitAutoData]
    public void User_WithTwoFactorProvidersNull_ShouldFail(User user)
    {
        // Arrange
        user.TwoFactorProviders = null;

        // Act
        try
        {
            var model = new TwoFactorDuoResponseModel(user);
        }
        catch (Exception ex)
        {
            // Assert
            Assert.IsType<ArgumentNullException>(ex);
        }
    }

    private string GetTwoFactorDuoProvidersJson()
    {
        return "{\"2\":{\"Enabled\":true,\"MetaData\":{\"ClientSecret\":\"secretClientSecret\",\"ClientId\":\"clientId\",\"Host\":\"example.com\"}}}";
    }
}
