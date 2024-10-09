using Bit.Api.Auth.Models.Request;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models;
using Bit.Core.Entities;
using Xunit;

namespace Bit.Api.Test.Auth.Models.Request;

public class UserTwoFactorDuoRequestModelTests
{
    [Fact]
    public void ShouldAddOrUpdateTwoFactorProvider_WhenExistingProviderDoesNotExist()
    {
        // Arrange
        var existingUser = new User();
        var model = new UpdateTwoFactorDuoRequestModel
        {
            ClientId = "clientId",
            ClientSecret = "clientSecret",
            Host = "example.com"
        };

        // Act
        var result = model.ToUser(existingUser);

        // Assert
        Assert.True(result.GetTwoFactorProviders().ContainsKey(TwoFactorProviderType.Duo));
        Assert.Equal("clientId", result.GetTwoFactorProviders()[TwoFactorProviderType.Duo].MetaData["ClientId"]);
        Assert.Equal("clientSecret", result.GetTwoFactorProviders()[TwoFactorProviderType.Duo].MetaData["ClientSecret"]);
        Assert.Equal("example.com", result.GetTwoFactorProviders()[TwoFactorProviderType.Duo].MetaData["Host"]);
        Assert.True(result.GetTwoFactorProviders()[TwoFactorProviderType.Duo].Enabled);
    }

    [Fact]
    public void ShouldUpdateTwoFactorProvider_WhenExistingProviderExists()
    {
        // Arrange
        var existingUser = new User();
        existingUser.SetTwoFactorProviders(new Dictionary<TwoFactorProviderType, TwoFactorProvider>
        {
            { TwoFactorProviderType.Duo, new TwoFactorProvider() }
        });
        var model = new UpdateTwoFactorDuoRequestModel
        {
            ClientId = "newClientId",
            ClientSecret = "newClientSecret",
            Host = "newExample.com"
        };

        // Act
        var result = model.ToUser(existingUser);

        // Assert
        Assert.True(result.GetTwoFactorProviders().ContainsKey(TwoFactorProviderType.Duo));
        Assert.Equal("newClientId", result.GetTwoFactorProviders()[TwoFactorProviderType.Duo].MetaData["ClientId"]);
        Assert.Equal("newClientSecret", result.GetTwoFactorProviders()[TwoFactorProviderType.Duo].MetaData["ClientSecret"]);
        Assert.Equal("newExample.com", result.GetTwoFactorProviders()[TwoFactorProviderType.Duo].MetaData["Host"]);
        Assert.True(result.GetTwoFactorProviders()[TwoFactorProviderType.Duo].Enabled);
    }
}
