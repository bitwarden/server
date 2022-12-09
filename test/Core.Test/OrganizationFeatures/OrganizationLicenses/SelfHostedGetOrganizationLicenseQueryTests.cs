using System.Text.Json;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Models.Business;
using Bit.Core.Models.OrganizationConnectionConfigs;
using Bit.Core.OrganizationFeatures.OrganizationLicenses;
using Bit.Core.Settings;
using Bit.Core.Test.AutoFixture;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Bit.Test.Common.Helpers;
using Xunit;

namespace Bit.Core.Test.OrganizationFeatures.OrganizationLicenses;

[SutProviderCustomize]
public class SelfHostedGetOrganizationLicenseQueryTests
{
    private static SutProvider<SelfHostedGetOrganizationLicenseQuery> GetSutProvider(BillingSyncConfig config,
        string apiResponse = null)
    {
        return new SutProvider<SelfHostedGetOrganizationLicenseQuery>()
            .ConfigureBaseIdentityClientService($"licenses/organization/{config.CloudOrganizationId}",
                HttpMethod.Get, apiResponse: apiResponse);
    }

    [Theory]
    [BitAutoData]
    [OrganizationLicenseCustomize]
    public async void GetLicenseAsync_Success(Organization organization,
        OrganizationConnection<BillingSyncConfig> billingSyncConnection, BillingSyncConfig config, OrganizationLicense license)
    {
        var sutProvider = GetSutProvider(config, JsonSerializer.Serialize(license));
        billingSyncConnection.Enabled = true;
        billingSyncConnection.Config = config;

        var result = await sutProvider.Sut.GetLicenseAsync(organization, billingSyncConnection);
        AssertHelper.AssertPropertyEqual(result, license);
    }

    [Theory]
    [BitAutoData]
    public async void GetLicenseAsync_WhenNotSelfHosted_Throws(Organization organization,
        OrganizationConnection billingSyncConnection, BillingSyncConfig config)
    {
        var sutProvider = GetSutProvider(config);
        sutProvider.GetDependency<IGlobalSettings>().SelfHosted = false;

        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.GetLicenseAsync(organization, billingSyncConnection));
        Assert.Contains("only available for self-hosted", exception.Message);
    }

    [Theory]
    [BitAutoData]
    public async void GetLicenseAsync_WhenCloudCommunicationDisabled_Throws(Organization organization,
        OrganizationConnection billingSyncConnection, BillingSyncConfig config)
    {
        var sutProvider = GetSutProvider(config);
        sutProvider.GetDependency<IGlobalSettings>().EnableCloudCommunication = false;

        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.GetLicenseAsync(organization, billingSyncConnection));
        Assert.Contains("Cloud communication is disabled", exception.Message);
    }

    [Theory]
    [BitAutoData]
    public async void GetLicenseAsync_WhenCantUseConnection_Throws(Organization organization,
        OrganizationConnection<BillingSyncConfig> billingSyncConnection, BillingSyncConfig config)
    {
        var sutProvider = GetSutProvider(config);
        billingSyncConnection.Enabled = false;

        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.GetLicenseAsync(organization, billingSyncConnection));
        Assert.Contains("Connection disabled", exception.Message);
    }

    [Theory]
    [BitAutoData]
    public async void GetLicenseAsync_WhenNullResponse_Throws(Organization organization,
        OrganizationConnection<BillingSyncConfig> billingSyncConnection, BillingSyncConfig config)
    {
        var sutProvider = GetSutProvider(config);
        billingSyncConnection.Enabled = true;
        billingSyncConnection.Config = config;

        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.GetLicenseAsync(organization, billingSyncConnection));
        Assert.Contains("Organization License sync failed", exception.Message);
    }
}
