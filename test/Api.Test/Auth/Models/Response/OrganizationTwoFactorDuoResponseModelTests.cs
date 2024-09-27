
using Bit.Api.Auth.Models.Response.TwoFactor;
using Bit.Core.AdminConsole.Entities;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Api.Test.Auth.Models.Response;

public class OrganizationTwoFactorDuoResponseModelTests
{
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
        Assert.Equal("secret************", model.ClientSecret);
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
        organization.TwoFactorProviders = null;

        // Act
        var model = new TwoFactorDuoResponseModel(organization);

        /// Assert
        Assert.False(model.Enabled);
    }

    [Theory]
    [BitAutoData]
    public void Organization_WithTwoFactorProvidersEmpty_ShouldFail(Organization organization)
    {
        // Arrange
        organization.TwoFactorProviders = "{}";

        // Act
        var model = new TwoFactorDuoResponseModel(organization);

        /// Assert
        Assert.False(model.Enabled);
    }

    private string GetTwoFactorOrganizationDuoProvidersJson()
    {
        return
            "{\"6\":{\"Enabled\":true,\"MetaData\":{\"ClientSecret\":\"secretClientSecret\",\"ClientId\":\"clientId\",\"Host\":\"example.com\"}}}";
    }
}
