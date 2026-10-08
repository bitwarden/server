using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums.Partnerships;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Repositories;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.AdminConsole.Repositories;

public class OrganizationPartnershipRepositoryTests
{
    [Theory, DatabaseData]
    public async Task CreateAsync_RoundTripsAllFields(
        IOrganizationPartnershipRepository repository,
        IOrganizationRepository organizationRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var creationDate = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        var partnership = new OrganizationPartnership
        {
            OrganizationId = organization.Id,
            Name = "Partner",
            Status = PartnershipStatus.Inactive,
            SponsoredPlanType = SponsoredPlanType.Premium,
            BindingMode = PartnershipBindingMode.Oidc,
            IdentityBindingConfiguration = "{\"claim\":\"sub\"}",
            CreationDate = creationDate,
            RevisionDate = creationDate.AddMinutes(1),
        };
        partnership.SetRegisteredReturnOrigins(["https://partner.example.com"]);

        await repository.CreateAsync(partnership);
        var result = await repository.GetByIdAsync(partnership.Id);

        Assert.NotNull(result);
        Assert.Equal(organization.Id, result.OrganizationId);
        Assert.Equal("Partner", result.Name);
        Assert.Equal(PartnershipStatus.Inactive, result.Status);
        Assert.Equal(SponsoredPlanType.Premium, result.SponsoredPlanType);
        Assert.Equal(PartnershipBindingMode.Oidc, result.BindingMode);
        Assert.Equal("{\"claim\":\"sub\"}", result.IdentityBindingConfiguration);
        Assert.Equal(["https://partner.example.com"], result.GetRegisteredReturnOrigins());
        Assert.Equal(creationDate, result.CreationDate);
        Assert.Equal(creationDate.AddMinutes(1), result.RevisionDate);
    }

    [Theory, DatabaseData]
    public async Task CreateAsync_DuplicateOrganizationId_Throws(
        IOrganizationPartnershipRepository repository,
        IOrganizationRepository organizationRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        await CreatePartnershipAsync(repository, organization);

        await Assert.ThrowsAnyAsync<Exception>(() => CreatePartnershipAsync(repository, organization));
    }

    [Theory, DatabaseData]
    public async Task GetByOrganizationIdAsync_ReturnsPartnership(
        IOrganizationPartnershipRepository repository,
        IOrganizationRepository organizationRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var partnership = await CreatePartnershipAsync(repository, organization);

        var result = await repository.GetByOrganizationIdAsync(organization.Id);

        Assert.NotNull(result);
        Assert.Equal(partnership.Id, result.Id);
    }

    [Theory, DatabaseData]
    public async Task GetByOrganizationIdAsync_NoPartnership_ReturnsNull(
        IOrganizationPartnershipRepository repository,
        IOrganizationRepository organizationRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();

        var result = await repository.GetByOrganizationIdAsync(organization.Id);

        Assert.Null(result);
    }

    [Theory, DatabaseData]
    public async Task ReplaceAsync_UpdatesFields(
        IOrganizationPartnershipRepository repository,
        IOrganizationRepository organizationRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var partnership = await CreatePartnershipAsync(repository, organization);

        partnership.Name = "Renamed";
        partnership.Status = PartnershipStatus.Inactive;
        partnership.BindingMode = PartnershipBindingMode.Oidc;
        partnership.IdentityBindingConfiguration = "{\"claim\":\"sub\"}";
        partnership.SetRegisteredReturnOrigins(["https://a.example.com", "https://b.example.com"]);
        await repository.ReplaceAsync(partnership);

        var result = await repository.GetByIdAsync(partnership.Id);

        Assert.NotNull(result);
        Assert.Equal("Renamed", result.Name);
        Assert.Equal(PartnershipStatus.Inactive, result.Status);
        Assert.Equal(PartnershipBindingMode.Oidc, result.BindingMode);
        Assert.Equal("{\"claim\":\"sub\"}", result.IdentityBindingConfiguration);
        Assert.Equal(["https://a.example.com", "https://b.example.com"], result.GetRegisteredReturnOrigins());
    }

    [Theory, DatabaseData]
    public async Task DeleteAsync_RemovesPartnership(
        IOrganizationPartnershipRepository repository,
        IOrganizationRepository organizationRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var partnership = await CreatePartnershipAsync(repository, organization);

        await repository.DeleteAsync(partnership);

        Assert.Null(await repository.GetByIdAsync(partnership.Id));
    }

    [Theory, DatabaseData]
    public async Task DeleteAsync_DeletesPartnershipsEntitlements(
        IOrganizationPartnershipRepository repository,
        IOrganizationPartnershipEntitlementRepository entitlementRepository,
        IOrganizationRepository organizationRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var partnership = await CreatePartnershipAsync(repository, organization);
        var entitlement = await entitlementRepository.CreateAsync(new OrganizationPartnershipEntitlement
        {
            OrganizationPartnershipId = partnership.Id,
            ExternalId = "customer-1",
            ExternalIdHash = string.Empty,
            State = PartnershipEntitlementState.Provisioned,
            LastAppliedEffectiveDate = DateTime.UtcNow,
        });

        await repository.DeleteAsync(partnership);

        Assert.Null(await entitlementRepository.GetByIdAsync(entitlement.Id));
    }

    [Theory, DatabaseData]
    public async Task DeleteOrganization_DeletesPartnership(
        IOrganizationPartnershipRepository repository,
        IOrganizationRepository organizationRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var partnership = await CreatePartnershipAsync(repository, organization);

        await organizationRepository.DeleteAsync(organization);

        Assert.Null(await repository.GetByIdAsync(partnership.Id));
    }

    private static Task<OrganizationPartnership> CreatePartnershipAsync(
        IOrganizationPartnershipRepository repository, Organization organization)
        => repository.CreateAsync(new OrganizationPartnership
        {
            OrganizationId = organization.Id,
            Name = "Partner",
            Status = PartnershipStatus.Active,
            SponsoredPlanType = SponsoredPlanType.Premium,
            BindingMode = PartnershipBindingMode.Token,
        });
}
