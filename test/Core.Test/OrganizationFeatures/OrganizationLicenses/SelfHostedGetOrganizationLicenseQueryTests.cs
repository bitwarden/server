using System.Text.Json;
using AutoFixture;
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
using NSubstitute;
using RichardSzalay.MockHttp;
using Xunit;

namespace Bit.Core.Test.OrganizationFeatures.OrganizationLicenses;

[SutProviderCustomize]
public class SelfHostedGetOrganizationLicenseQueryTests
{
    public static SutProvider<SelfHostedGetOrganizationLicenseQuery> GetSutProvider(Guid cloudOrganizationId,
        string identityResponse = null, string apiResponse = null)
    {
        var fixture = new Fixture().WithAutoNSubstitutionsAutoPopulatedProperties();
        fixture.AddMockHttp();

        var settings = fixture.Create<IGlobalSettings>();
        settings.SelfHosted = true;
        settings.EnableCloudCommunication = true;

        var apiUri = fixture.Create<Uri>();
        var identityUri = fixture.Create<Uri>();
        settings.Installation.ApiUri.Returns(apiUri.ToString());
        settings.Installation.IdentityUri.Returns(identityUri.ToString());

        var apiHandler = new MockHttpMessageHandler();
        var identityHandler = new MockHttpMessageHandler();
        var syncUri = string.Concat(apiUri, $"licenses/organization/{cloudOrganizationId}");
        var tokenUri = string.Concat(identityUri, "connect/token");

        apiHandler.When(HttpMethod.Get, syncUri)
            .Respond("application/json", apiResponse);
        identityHandler.When(HttpMethod.Post, tokenUri)
            .Respond("application/json", identityResponse ?? "{\"access_token\":\"string\",\"expires_in\":3600,\"token_type\":\"Bearer\",\"scope\":\"string\"}");


        var apiHttp = apiHandler.ToHttpClient();
        var identityHttp = identityHandler.ToHttpClient();

        var mockHttpClientFactory = Substitute.For<IHttpClientFactory>();
        mockHttpClientFactory.CreateClient(Arg.Is("client")).Returns(apiHttp);
        mockHttpClientFactory.CreateClient(Arg.Is("identity")).Returns(identityHttp);

        return new SutProvider<SelfHostedGetOrganizationLicenseQuery>(fixture)
            .SetDependency(settings)
            .SetDependency(mockHttpClientFactory)
            .Create();
    }

    [Theory]
    [BitAutoData]
    [OrganizationLicenseCustomize]
    public async void GetLicenseAsync_Success(Organization organization,
        OrganizationConnection<BillingSyncConfig> billingSyncConnection, BillingSyncConfig config, OrganizationLicense license)
    {
        var sutProvider = GetSutProvider(config.CloudOrganizationId, apiResponse: JsonSerializer.Serialize(license));
        sutProvider.GetDependency<IGlobalSettings>().SelfHosted = true;
        sutProvider.GetDependency<IGlobalSettings>().EnableCloudCommunication = true;
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
        var sutProvider = GetSutProvider(config.CloudOrganizationId);
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
        var sutProvider = GetSutProvider(config.CloudOrganizationId);
        sutProvider.GetDependency<IGlobalSettings>().SelfHosted = true;
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
        var sutProvider = GetSutProvider(config.CloudOrganizationId);
        sutProvider.GetDependency<IGlobalSettings>().SelfHosted = true;
        sutProvider.GetDependency<IGlobalSettings>().EnableCloudCommunication = true;
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
        var sutProvider = GetSutProvider(config.CloudOrganizationId);
        sutProvider.GetDependency<IGlobalSettings>().SelfHosted = true;
        sutProvider.GetDependency<IGlobalSettings>().EnableCloudCommunication = true;
        billingSyncConnection.Enabled = true;
        billingSyncConnection.Config = config;

        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.GetLicenseAsync(organization, billingSyncConnection));
        Assert.Contains("Organization License sync failed", exception.Message);
    }
}
