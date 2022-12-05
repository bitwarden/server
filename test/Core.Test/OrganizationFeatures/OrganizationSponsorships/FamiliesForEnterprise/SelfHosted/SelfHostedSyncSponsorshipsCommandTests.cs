using System.Text.Json;
using AutoFixture;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Models.Api.Response.OrganizationSponsorships;
using Bit.Core.Models.Data.Organizations.OrganizationSponsorships;
using Bit.Core.Models.OrganizationConnectionConfigs;
using Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.SelfHosted;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using Bit.Core.Test.AutoFixture.OrganizationSponsorshipFixtures;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using RichardSzalay.MockHttp;
using Xunit;

namespace Bit.Core.Test.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.SelfHosted;

public class SelfHostedSyncSponsorshipsCommandTests : FamiliesForEnterpriseTestsBase
{

    public static SutProvider<SelfHostedSyncSponsorshipsCommand> GetSutProvider(bool enableCloudCommunication = true, string identityResponse = null, string apiResponse = null)
    {
        var fixture = new Fixture().WithAutoNSubstitutionsAutoPopulatedProperties();
        fixture.AddMockHttp();

        var settings = fixture.Create<IGlobalSettings>();
        settings.SelfHosted = true;
        settings.EnableCloudCommunication = enableCloudCommunication;

        var apiUri = fixture.Create<Uri>();
        var identityUri = fixture.Create<Uri>();
        settings.Installation.ApiUri.Returns(apiUri.ToString());
        settings.Installation.IdentityUri.Returns(identityUri.ToString());

        var apiHandler = new MockHttpMessageHandler();
        var identityHandler = new MockHttpMessageHandler();
        var syncUri = string.Concat(apiUri, "organization/sponsorship/sync");
        var tokenUri = string.Concat(identityUri, "connect/token");

        apiHandler.When(HttpMethod.Post, syncUri)
            .Respond("application/json", apiResponse);
        identityHandler.When(HttpMethod.Post, tokenUri)
            .Respond("application/json", identityResponse ?? "{\"access_token\":\"string\",\"expires_in\":3600,\"token_type\":\"Bearer\",\"scope\":\"string\"}");


        var apiHttp = apiHandler.ToHttpClient();
        var identityHttp = identityHandler.ToHttpClient();

        var mockHttpClientFactory = Substitute.For<IHttpClientFactory>();
        mockHttpClientFactory.CreateClient(Arg.Is("client")).Returns(apiHttp);
        mockHttpClientFactory.CreateClient(Arg.Is("identity")).Returns(identityHttp);

        return new SutProvider<SelfHostedSyncSponsorshipsCommand>(fixture)
            .SetDependency(settings)
            .SetDependency(mockHttpClientFactory)
            .Create();
    }

    [Theory]
    [BitAutoData]
    public async Task SyncOrganization_BillingSyncKeyDisabled_ThrowsBadRequest(
        Guid cloudOrganizationId, OrganizationConnection billingSyncConnection)
    {
        var sutProvider = GetSutProvider();
        billingSyncConnection.Enabled = false;
        billingSyncConnection.SetConfig(new BillingSyncConfig
        {
            BillingSyncKey = "okslkcslkjf"
        });

        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.SyncOrganization(billingSyncConnection.OrganizationId, cloudOrganizationId, billingSyncConnection));

        Assert.Contains($"Connection disabled", exception.Message);

        await sutProvider.GetDependency<IOrganizationSponsorshipRepository>()
            .DidNotReceiveWithAnyArgs()
            .DeleteManyAsync(default);
        await sutProvider.GetDependency<IOrganizationSponsorshipRepository>()
            .DidNotReceiveWithAnyArgs()
            .UpsertManyAsync(default);
    }

    [Theory]
    [BitAutoData]
    public async Task SyncOrganization_BillingSyncConfigEmpty_ThrowsBadRequest(
        Guid cloudOrganizationId, OrganizationConnection billingSyncConnection)
    {
        var sutProvider = GetSutProvider();
        billingSyncConnection.Config = "";

        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.SyncOrganization(billingSyncConnection.OrganizationId, cloudOrganizationId, billingSyncConnection));

        Assert.Contains($"No saved Connection config", exception.Message);

        await sutProvider.GetDependency<IOrganizationSponsorshipRepository>()
            .DidNotReceiveWithAnyArgs()
            .DeleteManyAsync(default);
        await sutProvider.GetDependency<IOrganizationSponsorshipRepository>()
            .DidNotReceiveWithAnyArgs()
            .UpsertManyAsync(default);
    }

    [Theory]
    [BitAutoData]
    public async Task SyncOrganization_CloudCommunicationDisabled_EarlyReturn(
        Guid cloudOrganizationId, OrganizationConnection billingSyncConnection)
    {
        var sutProvider = GetSutProvider(false);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.SyncOrganization(billingSyncConnection.OrganizationId, cloudOrganizationId, billingSyncConnection));

        Assert.Contains($"Cloud communication is disabled", exception.Message);

        await sutProvider.GetDependency<IOrganizationSponsorshipRepository>()
            .DidNotReceiveWithAnyArgs()
            .DeleteManyAsync(default);
        await sutProvider.GetDependency<IOrganizationSponsorshipRepository>()
            .DidNotReceiveWithAnyArgs()
            .UpsertManyAsync(default);
    }

    [Theory]
    [OrganizationSponsorshipCustomize]
    [BitAutoData]
    public async Task SyncOrganization_SyncsSponsorships(
        Guid cloudOrganizationId, OrganizationConnection billingSyncConnection, IEnumerable<OrganizationSponsorship> sponsorships)
    {
        var syncJsonResponse = JsonSerializer.Serialize(new OrganizationSponsorshipSyncResponseModel(
            new OrganizationSponsorshipSyncData
            {
                SponsorshipsBatch = sponsorships.Select(o => new OrganizationSponsorshipData(o))
            }));

        var sutProvider = GetSutProvider(apiResponse: syncJsonResponse);
        billingSyncConnection.SetConfig(new BillingSyncConfig
        {
            BillingSyncKey = "okslkcslkjf"
        });
        sutProvider.GetDependency<IOrganizationSponsorshipRepository>()
            .GetManyBySponsoringOrganizationAsync(Arg.Any<Guid>()).Returns(sponsorships.ToList());

        await sutProvider.Sut.SyncOrganization(billingSyncConnection.OrganizationId, cloudOrganizationId, billingSyncConnection);

        await sutProvider.GetDependency<IOrganizationSponsorshipRepository>()
            .DidNotReceiveWithAnyArgs()
            .DeleteManyAsync(default);
        await sutProvider.GetDependency<IOrganizationSponsorshipRepository>()
            .Received(1)
            .UpsertManyAsync(Arg.Any<IEnumerable<OrganizationSponsorship>>());
    }

    [Theory]
    [OrganizationSponsorshipCustomize(ToDelete = true)]
    [BitAutoData]
    public async Task SyncOrganization_DeletesSponsorships(
        Guid cloudOrganizationId, OrganizationConnection billingSyncConnection, IEnumerable<OrganizationSponsorship> sponsorships)
    {
        var syncJsonResponse = JsonSerializer.Serialize(new OrganizationSponsorshipSyncResponseModel(
            new OrganizationSponsorshipSyncData
            {
                SponsorshipsBatch = sponsorships.Select(o => new OrganizationSponsorshipData(o) { CloudSponsorshipRemoved = true })
            }));

        var sutProvider = GetSutProvider(apiResponse: syncJsonResponse);
        billingSyncConnection.SetConfig(new BillingSyncConfig
        {
            BillingSyncKey = "okslkcslkjf"
        });
        sutProvider.GetDependency<IOrganizationSponsorshipRepository>()
            .GetManyBySponsoringOrganizationAsync(Arg.Any<Guid>()).Returns(sponsorships.ToList());

        await sutProvider.Sut.SyncOrganization(billingSyncConnection.OrganizationId, cloudOrganizationId, billingSyncConnection);

        await sutProvider.GetDependency<IOrganizationSponsorshipRepository>()
            .Received(1)
            .DeleteManyAsync(Arg.Any<IEnumerable<Guid>>());
        await sutProvider.GetDependency<IOrganizationSponsorshipRepository>()
            .DidNotReceiveWithAnyArgs()
            .UpsertManyAsync(default);
    }
}
