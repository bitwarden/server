using System.Text.Json;
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
using Xunit;

namespace Bit.Core.Test.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.SelfHosted;

public class SelfHostedSyncSponsorshipsCommandTests : FamiliesForEnterpriseTestsBase
{
    private static SutProvider<SelfHostedSyncSponsorshipsCommand> GetSutProvider(string apiResponse = null)
    {
        return new SutProvider<SelfHostedSyncSponsorshipsCommand>()
            .ConfigureBaseIdentityClientService("organization/sponsorship/sync",
                HttpMethod.Post, apiResponse: apiResponse);
    }

    [Theory]
    [BitAutoData]
    public async Task SyncOrganization_BillingSyncConnectionDisabled_ThrowsBadRequest(
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
        var sutProvider = GetSutProvider();
        sutProvider.GetDependency<IGlobalSettings>().EnableCloudCommunication = false;

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

        var sutProvider = GetSutProvider(syncJsonResponse);

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

        var sutProvider = GetSutProvider(syncJsonResponse);
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
