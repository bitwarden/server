using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.Repositories;

public class OrganizationRepositoryTests
{
    [DatabaseTheory, DatabaseData]
    public async Task CreateAsync_Works(IOrganizationRepository organizationRepository)
    {
        var createdOrganization = await organizationRepository.CreateAsync(new Organization
        {
            Name = "Test Org", // TODO: EF doesn't enforce this as not null
            BillingEmail = "email@test.com", // TODO: EF doesn't enforce this as not null
            Plan = "Free", // TODO: EF doesn't enforce this as not null
        });

        var fromDatabaseOrganization = await organizationRepository.GetByIdAsync(createdOrganization.Id);

        Assert.NotNull(fromDatabaseOrganization);

        Assert.Equal("Test Org", fromDatabaseOrganization.Name);
        Assert.Equal("email@test.com", fromDatabaseOrganization.BillingEmail);
        Assert.Equal("Free", fromDatabaseOrganization.Plan);
    }

    [DatabaseTheory, DatabaseData]
    public async Task GetByIdentifierAsync_Works(IOrganizationRepository organizationRepository)
    {
        var nonSearchIdentifier = Guid.NewGuid().ToString();
        var searchIdentifier = Guid.NewGuid().ToString();

        var org1 = await organizationRepository.CreateAsync(new Organization
        {
            Name = "Test Org #1", // TODO: EF doesn't enforce this as not null
            BillingEmail = "email@test.com", // TODO: EF doesn't enforce this as not null
            Plan = "Free", // TODO: EF doesn't enforce this as not null
            Identifier = nonSearchIdentifier, // TODO: EF doesn't enforce this as unique
        });

        var org2 = await organizationRepository.CreateAsync(new Organization
        {
            Name = "Test Org #2", // TODO: EF doesn't enforce this as not null
            BillingEmail = "email@test.com", // TODO: EF doesn't enforce this as not null
            Plan = "Free", // TODO: EF doesn't enforce this as not null
            Identifier = searchIdentifier,
        });

        var retrievedOrganization = await organizationRepository.GetByIdentifierAsync(searchIdentifier);

        Assert.NotNull(retrievedOrganization);
        Assert.Equal(org2.Id, retrievedOrganization.Id);
    }

    [DatabaseTheory, DatabaseData]
    public async Task GetManyByEnabledAsync_Works(IOrganizationRepository organizationRepository)
    {
        var createdNotEnabledOrg = await organizationRepository.CreateAsync(new Organization
        {
            Name = "Test Org", // TODO: EF doesn't enforce this as not null
            BillingEmail = "email@test.com", // TODO: EF doesn't enforce this as not null
            Plan = "Free", // TODO: EF doesn't enforce this as not null
            Enabled = false,
        });

        var createdEnabledOrg = await organizationRepository.CreateAsync(new Organization
        {
            Name = "Test Org", // TODO: EF doesn't enforce this as not null
            BillingEmail = "email@test.com", // TODO: EF doesn't enforce this as not null
            Plan = "Free", // TODO: EF doesn't enforce this as not null
            Enabled = true,
        });

        var enabledOrgs = await organizationRepository.GetManyByEnabledAsync();
        Assert.Contains(enabledOrgs, o => o.Id == createdEnabledOrg.Id);
        Assert.DoesNotContain(enabledOrgs, o => o.Id == createdNotEnabledOrg.Id);
    }

    [DatabaseTheory, DatabaseData]
    public async Task SearchUnassignedToProviderAsync_Works(IOrganizationRepository organizationRepository,
        IUserRepository userRepository,
        IServiceProvider services)
    {
        var organizationName = $"test_provider_{Guid.NewGuid()}";

        var orgUser = await services.CreateOrganizationUserAsync();
        var organization = await services.UpdateOrganizationAsync(orgUser.OrganizationId, organization =>
        {
            organization.Name = organizationName;
            organization.PlanType = PlanType.EnterpriseAnnually;
        });

        var user = await userRepository.GetByIdAsync(orgUser.UserId!.Value);

        var matchingOrganizations = await organizationRepository.SearchUnassignedToProviderAsync(organizationName[5..20],
            user.Email[..10], 0, 20);

        Assert.Contains(matchingOrganizations, o => o.Name == organizationName);
    }
}
