
using Bit.Api.Auth.Models.Response.TwoFactor;
using Bit.Core.Entities;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Api.Test.Auth.Models.Response;

public class UserTwoFactorDuoResponseModelTests
{
    [Theory]
    [BitAutoData]
    public void User_WithDuo_ShouldBuildModel(User user)
    {
        // Arrange
        user.TwoFactorProviders = GetTwoFactorDuoProvidersJson();

        // Act
        var model = new TwoFactorDuoResponseModel(user);

        // Assert Even if both versions are present priority is given to v4 data
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
        var model = new TwoFactorDuoResponseModel(user);

        /// Assert
        Assert.False(model.Enabled);
    }

    [Theory]
    [BitAutoData]
    public void User_WithTwoFactorProvidersEmpty_ShouldFail(User user)
    {
        // Arrange
        user.TwoFactorProviders = "{}";

        // Act
        var model = new TwoFactorDuoResponseModel(user);

        /// Assert
        Assert.False(model.Enabled);
    }

    private string GetTwoFactorDuoProvidersJson()
    {
        return
            "{\"2\":{\"Enabled\":true,\"MetaData\":{\"ClientSecret\":\"secretClientSecret\",\"ClientId\":\"clientId\",\"Host\":\"example.com\"}}}";
    }
}
