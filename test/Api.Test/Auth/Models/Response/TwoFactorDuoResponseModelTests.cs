
using Bit.Api.Auth.Models.Response.TwoFactor;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Entities;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Api.Test.Auth.Models.Response;

public class TwoFactorDuoResponseModelTests
{
    #region User
    [Theory]
    [BitAutoData]
    public void User_WithDuoV4_ShouldBuildModel(User user)
    {
        // Arrange
        user.TwoFactorProviders = GetTwoFactorDuoV4ProvidersJson();

        // Act
        var model = new TwoFactorDuoResponseModel(user);

        // Assert if v4 data Ikey and Skey are set to clientId and clientSecret
        Assert.NotNull(model);
        Assert.Equal("clientId", model.ClientId);
        Assert.Equal("clientSecret", model.ClientSecret);
        Assert.Equal("clientId", model.IntegrationKey);
        Assert.Equal("clientSecret", model.SecretKey);
    }

    [Theory]
    [BitAutoData]
    public void User_WithDuov2_ShouldBuildModel(User user)
    {
        // Arrange
        user.TwoFactorProviders = GetTwoFactorDuoV2ProvidersJson();

        // Act
        var model = new TwoFactorDuoResponseModel(user);

        // Assert if only v2 data clientId and clientSecret are set to Ikey and Skey
        Assert.NotNull(model);
        Assert.Equal("IKey", model.ClientId);
        Assert.Equal("SKey", model.ClientSecret);
        Assert.Equal("IKey", model.IntegrationKey);
        Assert.Equal("SKey", model.SecretKey);
    }

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
        Assert.Equal("clientSecret", model.ClientSecret);
        Assert.Equal("clientId", model.IntegrationKey);
        Assert.Equal("clientSecret", model.SecretKey);
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
    #endregion

    #region Organization
    [Theory]
    [BitAutoData]
    public void Organization_WithDuoV4_ShouldBuildModel(Organization organization)
    {
        // Arrange
        organization.TwoFactorProviders = GetTwoFactorOrganizationDuoV4ProvidersJson();

        // Act
        var model = new TwoFactorDuoResponseModel(organization);

        // Assert if v4 data Ikey and Skey are set to clientId and clientSecret
        Assert.NotNull(model);
        Assert.Equal("clientId", model.ClientId);
        Assert.Equal("clientSecret", model.ClientSecret);
        Assert.Equal("clientId", model.IntegrationKey);
        Assert.Equal("clientSecret", model.SecretKey);
    }

    [Theory]
    [BitAutoData]
    public void Organization_WithDuoV2_ShouldBuildModel(Organization organization)
    {
        // Arrange
        organization.TwoFactorProviders = GetTwoFactorOrganizationDuoV2ProvidersJson();

        // Act
        var model = new TwoFactorDuoResponseModel(organization);

        // Assert if only v2 data clientId and clientSecret are set to Ikey and Sk
        Assert.NotNull(model);
        Assert.Equal("IKey", model.ClientId);
        Assert.Equal("SKey", model.ClientSecret);
        Assert.Equal("IKey", model.IntegrationKey);
        Assert.Equal("SKey", model.SecretKey);
    }

    [Theory]
    [BitAutoData]
    public void Organization_WithDuo_ShouldBuildModel(Organization organization)
    {
        // Arrange
        organization.TwoFactorProviders = GetTwoFactorOrganizationDuoProvidersJson();

        // Act
        var model = new TwoFactorDuoResponseModel(organization);

        /// Assert Even if both versions are present priority is given to v4 data
        Assert.NotNull(model);
        Assert.Equal("clientId", model.ClientId);
        Assert.Equal("clientSecret", model.ClientSecret);
        Assert.Equal("clientId", model.IntegrationKey);
        Assert.Equal("clientSecret", model.SecretKey);
    }

    [Theory]
    [BitAutoData]
    public void Organization_WithDuoEmpty_ShouldFail(Organization organization)
    {
        // Arrange
        organization.TwoFactorProviders = "{\"6\" : {}}";

        // Act
        var model = new TwoFactorDuoResponseModel(organization);

        /// Assert
        Assert.False(model.Enabled);
    }

    [Theory]
    [BitAutoData]
    public void Organization_WithTwoFactorProvidersNull_ShouldFail(Organization organization)
    {
        // Arrange
        organization.TwoFactorProviders = "{\"6\" : {}}";

        // Act
        var model = new TwoFactorDuoResponseModel(organization);

        /// Assert
        Assert.False(model.Enabled);
    }
    #endregion

    #region Private Methods
    private string GetTwoFactorDuoProvidersJson()
    {
        return "{\"2\":{\"Enabled\":true,\"MetaData\":{\"SKey\":\"SKey\",\"IKey\":\"IKey\",\"ClientSecret\":\"clientSecret\",\"ClientId\":\"clientId\",\"Host\":\"example.com\"}}}";
    }

    private string GetTwoFactorDuoV4ProvidersJson()
    {
        return "{\"2\":{\"Enabled\":true,\"MetaData\":{\"ClientSecret\":\"clientSecret\",\"ClientId\":\"clientId\",\"Host\":\"example.com\"}}}";
    }

    private string GetTwoFactorDuoV2ProvidersJson()
    {
        return "{\"2\":{\"Enabled\":true,\"MetaData\":{\"SKey\":\"SKey\",\"IKey\":\"IKey\",\"Host\":\"example.com\"}}}";
    }

    private string GetTwoFactorOrganizationDuoProvidersJson()
    {
        return "{\"6\":{\"Enabled\":true,\"MetaData\":{\"SKey\":\"SKey\",\"IKey\":\"IKey\",\"ClientSecret\":\"clientSecret\",\"ClientId\":\"clientId\",\"Host\":\"example.com\"}}}";
    }

    private string GetTwoFactorOrganizationDuoV4ProvidersJson()
    {
        return "{\"6\":{\"Enabled\":true,\"MetaData\":{\"ClientSecret\":\"clientSecret\",\"ClientId\":\"clientId\",\"Host\":\"example.com\"}}}";
    }

    private string GetTwoFactorOrganizationDuoV2ProvidersJson()
    {
        return "{\"6\":{\"Enabled\":true,\"MetaData\":{\"SKey\":\"SKey\",\"IKey\":\"IKey\",\"Host\":\"example.com\"}}}";
    }
    #endregion
}
