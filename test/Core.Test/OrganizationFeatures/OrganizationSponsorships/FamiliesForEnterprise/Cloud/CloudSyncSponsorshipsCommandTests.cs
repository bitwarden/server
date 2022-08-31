using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data.Organizations.OrganizationSponsorships;
using Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.Cloud;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.Cloud;

[SutProviderCustomize]
public class CloudSyncSponsorshipsCommandTests : FamiliesForEnterpriseTestsBase
{

    [Theory]
    [BitAutoData]
    public async Task SyncOrganization_SponsoringOrgNotFound_ThrowsBadRequest(
        IEnumerable<OrganizationSponsorshipData> sponsorshipsData,
        SutProvider<CloudSyncSponsorshipsCommand> sutProvider)
    {
        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.SyncOrganization(null, sponsorshipsData));

        Assert.Contains("Failed to sync sponsorship - missing organization.", exception.Message);

        await sutProvider.GetDependency<IOrganizationSponsorshipRepository>()
            .DidNotReceiveWithAnyArgs()
            .UpsertManyAsync(default);
        await sutProvider.GetDependency<IOrganizationSponsorshipRepository>()
            .DidNotReceiveWithAnyArgs()
            .DeleteManyAsync(default);
    }

    [Theory]
    [BitAutoData]
    public async Task SyncOrganization_NoSponsorships_EarlyReturn(
        Organization organization,
        SutProvider<CloudSyncSponsorshipsCommand> sutProvider)
    {
        var result = await sutProvider.Sut.SyncOrganization(organization, Enumerable.Empty<OrganizationSponsorshipData>());

        Assert.Empty(result.Item1.SponsorshipsBatch);
        Assert.Empty(result.Item2);

        await sutProvider.GetDependency<IOrganizationSponsorshipRepository>()
            .DidNotReceiveWithAnyArgs()
            .UpsertManyAsync(default);
        await sutProvider.GetDependency<IOrganizationSponsorshipRepository>()
            .DidNotReceiveWithAnyArgs()
            .DeleteManyAsync(default);
    }

    [Theory]
    [BitMemberAutoData(nameof(NonEnterprisePlanTypes))]
    public async Task SyncOrganization_BadSponsoringOrgPlan_NoSync(
        PlanType planType,
        Organization organization, IEnumerable<OrganizationSponsorshipData> sponsorshipsData,
        SutProvider<CloudSyncSponsorshipsCommand> sutProvider)
    {
        organization.PlanType = planType;

        await sutProvider.Sut.SyncOrganization(organization, sponsorshipsData);

        await sutProvider.GetDependency<IOrganizationSponsorshipRepository>()
            .DidNotReceiveWithAnyArgs()
            .UpsertManyAsync(default);
        await sutProvider.GetDependency<IOrganizationSponsorshipRepository>()
            .DidNotReceiveWithAnyArgs()
            .DeleteManyAsync(default);
    }

    [Theory]
    [BitAutoData]
    public async Task SyncOrganization_Success_RecordsEvent(Organization organization,
        SutProvider<CloudSyncSponsorshipsCommand> sutProvider)
    {
        await sutProvider.Sut.SyncOrganization(organization, Array.Empty<OrganizationSponsorshipData>());

        await sutProvider.GetDependency<IEventService>().Received(1).LogOrganizationEventAsync(organization, EventType.Organization_SponsorshipsSynced, Arg.Any<DateTime?>());
    }

    [Theory]
    [BitAutoData]
    public async Task SyncOrganization_OneExisting_OneNew_Success(SutProvider<CloudSyncSponsorshipsCommand> sutProvider,
        Organization sponsoringOrganization, OrganizationSponsorship existingSponsorship, OrganizationSponsorship newSponsorship)
    {
        // Arrange
        sponsoringOrganization.Enabled = true;
        sponsoringOrganization.PlanType = PlanType.EnterpriseAnnually;

        existingSponsorship.ToDelete = false;
        newSponsorship.ToDelete = false;

        sutProvider.GetDependency<IOrganizationSponsorshipRepository>()
            .GetManyBySponsoringOrganizationAsync(sponsoringOrganization.Id)
            .Returns(new List<OrganizationSponsorship>
            {
                existingSponsorship,
            });

        // Act
        var (syncData, toEmailSponsorships) = await sutProvider.Sut.SyncOrganization(sponsoringOrganization, new[]
        {
            new OrganizationSponsorshipData(existingSponsorship),
            new OrganizationSponsorshipData(newSponsorship),
        });

        // Assert
        // Should have updated the cloud copy for each item given
        await sutProvider.GetDependency<IOrganizationSponsorshipRepository>()
            .Received(1)
            .UpsertManyAsync(Arg.Is<IEnumerable<OrganizationSponsorship>>(sponsorships => sponsorships.Count() == 2));

        // Neither were marked as delete, should not have deleted
        await sutProvider.GetDependency<IOrganizationSponsorshipRepository>()
            .DidNotReceiveWithAnyArgs()
            .DeleteManyAsync(default);

        // Only one sponsorship was new so it should only send one
        Assert.Single(toEmailSponsorships);
    }

    [Theory]
    [BitAutoData]
    public async Task SyncOrganization_TwoToDelete_OneCanDelete_Success(SutProvider<CloudSyncSponsorshipsCommand> sutProvider,
        Organization sponsoringOrganization, OrganizationSponsorship canDeleteSponsorship, OrganizationSponsorship cannotDeleteSponsorship)
    {
        // Arrange
        sponsoringOrganization.PlanType = PlanType.EnterpriseAnnually;

        canDeleteSponsorship.ToDelete = true;
        canDeleteSponsorship.SponsoredOrganizationId = null;

        cannotDeleteSponsorship.ToDelete = true;
        cannotDeleteSponsorship.SponsoredOrganizationId = Guid.NewGuid();

        sutProvider.GetDependency<IOrganizationSponsorshipRepository>()
            .GetManyBySponsoringOrganizationAsync(sponsoringOrganization.Id)
            .Returns(new List<OrganizationSponsorship>
            {
                canDeleteSponsorship,
                cannotDeleteSponsorship,
            });

        // Act
        var (syncData, toEmailSponsorships) = await sutProvider.Sut.SyncOrganization(sponsoringOrganization, new[]
        {
            new OrganizationSponsorshipData(canDeleteSponsorship),
            new OrganizationSponsorshipData(cannotDeleteSponsorship),
        });

        // Assert

        await sutProvider.GetDependency<IOrganizationSponsorshipRepository>()
            .Received(1)
            .UpsertManyAsync(Arg.Is<IEnumerable<OrganizationSponsorship>>(sponsorships => sponsorships.Count() == 2));

        // Deletes the sponsorship that had delete requested and is not sponsoring an org
        await sutProvider.GetDependency<IOrganizationSponsorshipRepository>()
            .Received(1)
            .DeleteManyAsync(Arg.Is<IEnumerable<Guid>>(toDeleteIds =>
                toDeleteIds.Count() == 1 && toDeleteIds.ElementAt(0) == canDeleteSponsorship.Id));
    }

    [Theory]
    [BitAutoData]
    public async Task SyncOrganization_BadData_DoesNotSave(SutProvider<CloudSyncSponsorshipsCommand> sutProvider,
        Organization sponsoringOrganization, OrganizationSponsorship badOrganizationSponsorship)
    {
        sponsoringOrganization.PlanType = PlanType.EnterpriseAnnually;

        badOrganizationSponsorship.ToDelete = true;
        badOrganizationSponsorship.LastSyncDate = null;

        sutProvider.GetDependency<IOrganizationSponsorshipRepository>()
            .GetManyBySponsoringOrganizationAsync(sponsoringOrganization.Id)
            .Returns(new List<OrganizationSponsorship>());

        var (syncData, toEmailSponsorships) = await sutProvider.Sut.SyncOrganization(sponsoringOrganization, new[]
        {
            new OrganizationSponsorshipData(badOrganizationSponsorship),
        });

        await sutProvider.GetDependency<IOrganizationSponsorshipRepository>()
            .DidNotReceiveWithAnyArgs()
            .UpsertManyAsync(default);

        await sutProvider.GetDependency<IOrganizationSponsorshipRepository>()
            .DidNotReceiveWithAnyArgs()
            .DeleteManyAsync(default);
    }

    [Theory]
    [BitAutoData]
    public async Task SyncOrganization_OrgDisabledForFourMonths_DoesNotSave(SutProvider<CloudSyncSponsorshipsCommand> sutProvider,
        Organization sponsoringOrganization, OrganizationSponsorship organizationSponsorship)
    {
        sponsoringOrganization.PlanType = PlanType.EnterpriseAnnually;
        sponsoringOrganization.Enabled = false;
        sponsoringOrganization.ExpirationDate = DateTime.UtcNow.AddDays(-120);

        organizationSponsorship.ToDelete = false;

        sutProvider.GetDependency<IOrganizationSponsorshipRepository>()
            .GetManyBySponsoringOrganizationAsync(sponsoringOrganization.Id)
            .Returns(new List<OrganizationSponsorship>());

        var (syncData, toEmailSponsorships) = await sutProvider.Sut.SyncOrganization(sponsoringOrganization, new[]
        {
            new OrganizationSponsorshipData(organizationSponsorship),
        });

        await sutProvider.GetDependency<IOrganizationSponsorshipRepository>()
            .DidNotReceiveWithAnyArgs()
            .UpsertManyAsync(default);

        await sutProvider.GetDependency<IOrganizationSponsorshipRepository>()
            .DidNotReceiveWithAnyArgs()
            .DeleteManyAsync(default);
    }
}
