using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing.Enums;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.AdminConsole.Repositories.PolicyRepository;

public class GetPolicyDetailsByOrganizationIdAsyncTests
{
    [DatabaseTheory, DatabaseData]
    public async Task ShouldContainProviderData(
        IUserRepository userRepository,
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationRepository organizationRepository,
        IProviderRepository providerRepository,
        IProviderUserRepository providerUserRepository,
        IProviderOrganizationRepository providerOrganizationRepository,
        IPolicyRepository policyRepository)
    {
        // Arrange
        var user = await userRepository.CreateTestUserAsync();
        const PolicyType policyType = PolicyType.SingleOrg;

        var userOrgConnectedDirectly = await ArrangeDirectlyConnectedOrgByUserIdAsync(organizationUserRepository, organizationRepository, policyRepository, user, policyType);

        await ArrangeProvider();

        // Act
        var results = (await policyRepository.GetPolicyDetailsByOrganizationIdAsync(userOrgConnectedDirectly.OrganizationId, policyType)).ToList();

        // Assert
        Assert.Single(results);

        Assert.True(results.Single().IsProvider);

        async Task ArrangeProvider()
        {
            var provider = await providerRepository.CreateAsync(new Provider
            {
                Name = Guid.NewGuid().ToString(),
                Enabled = true
            });
            await providerUserRepository.CreateAsync(new ProviderUser
            {
                ProviderId = provider.Id,
                UserId = user.Id,
                Status = ProviderUserStatusType.Confirmed
            });
            await providerOrganizationRepository.CreateAsync(new ProviderOrganization
            {
                OrganizationId = userOrgConnectedDirectly.OrganizationId,
                ProviderId = provider.Id
            });
        }
    }

    [DatabaseTheory, DatabaseData]
    public async Task ShouldNotReturnOtherOrganizations_WhenUserIsNotConnected(
        IUserRepository userRepository,
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationRepository organizationRepository,
        IPolicyRepository policyRepository)
    {
        // Arrange
        var user = await userRepository.CreateTestUserAsync();

        const PolicyType policyType = PolicyType.SingleOrg;
        var userOrgConnectedDirectly = await ArrangeDirectlyConnectedOrgByUserIdAsync(organizationUserRepository, organizationRepository, policyRepository, user, policyType);

        var notConnectedOrg = await CreateEnterpriseOrg(organizationRepository);
        await policyRepository.CreateAsync(new Policy { OrganizationId = notConnectedOrg.Id, Enabled = true, Type = policyType });

        // Act
        var results = (await policyRepository.GetPolicyDetailsByOrganizationIdAsync(userOrgConnectedDirectly.OrganizationId, PolicyType.SingleOrg)).ToList();

        // Assert
        Assert.Single(results);

        Assert.Contains(results, result => result.OrganizationUserId == userOrgConnectedDirectly.Id
                                           && result.OrganizationId == userOrgConnectedDirectly.OrganizationId);
        Assert.DoesNotContain(results, result => result.OrganizationId == notConnectedOrg.Id);
    }

    [DatabaseTheory, DatabaseData]
    public async Task ShouldOnlyReturnInputPolicyType(
        IUserRepository userRepository,
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationRepository organizationRepository,
        IPolicyRepository policyRepository)
    {
        // Arrange
        var user = await userRepository.CreateTestUserAsync();

        const PolicyType inputPolicyType = PolicyType.SingleOrg;
        var orgUser = await ArrangeDirectlyConnectedOrgByUserIdAsync(organizationUserRepository, organizationRepository, policyRepository, user, inputPolicyType);

        const PolicyType notInputPolicyType = PolicyType.RequireSso;
        await policyRepository.CreateAsync(new Policy { OrganizationId = orgUser.OrganizationId, Enabled = true, Type = notInputPolicyType });

        // Act
        var results = (await policyRepository.GetPolicyDetailsByOrganizationIdAsync(orgUser.OrganizationId, inputPolicyType)).ToList();

        // Assert
        Assert.Single(results);

        Assert.Contains(results, result => result.OrganizationUserId == orgUser.Id
                                           && result.OrganizationId == orgUser.OrganizationId
                                           && result.PolicyType == inputPolicyType);

        Assert.DoesNotContain(results, result => result.PolicyType == notInputPolicyType);
    }


    [DatabaseTheory, DatabaseData]
    public async Task WhenDirectlyConnectedUserHasUserId_ShouldReturnOtherConnectedOrganizationPolicies(
       IUserRepository userRepository,
       IOrganizationUserRepository organizationUserRepository,
       IOrganizationRepository organizationRepository,
       IPolicyRepository policyRepository)
    {
        // Arrange
        var user = await userRepository.CreateTestUserAsync();
        const PolicyType policyType = PolicyType.SingleOrg;

        var userOrgConnectedDirectly = await ArrangeDirectlyConnectedOrgByUserIdAsync(organizationUserRepository, organizationRepository, policyRepository, user, policyType);

        var userOrgConnectedByEmail = await ArrangeOtherOrgConnectedByEmailAsync(organizationUserRepository, organizationRepository, policyRepository, user, policyType);

        var userOrgConnectedByUserId = await ArrangeOtherOrgConnectedByUserIdAsync(organizationUserRepository, organizationRepository, policyRepository, user, policyType);

        // Act
        var results = (await policyRepository.GetPolicyDetailsByOrganizationIdAsync(userOrgConnectedDirectly.OrganizationId, policyType)).ToList();

        // Assert
        const int expectedCount = 3;
        Assert.Equal(expectedCount, results.Count);

        AssertPolicyDetailUserConnections(results, userOrgConnectedDirectly, userOrgConnectedByEmail, userOrgConnectedByUserId);
    }

    [DatabaseTheory, DatabaseData]
    public async Task WhenDirectlyConnectedUserHasEmail_ShouldReturnOtherConnectedOrganizationPolicies(
        IUserRepository userRepository,
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationRepository organizationRepository,
        IPolicyRepository policyRepository)
    {
        // Arrange
        var user = await userRepository.CreateTestUserAsync();
        const PolicyType policyType = PolicyType.SingleOrg;

        var userOrgConnectedDirectly = await ArrangeDirectlyConnectedOrgByEmailAsync(organizationUserRepository, organizationRepository, policyRepository, user, policyType);

        var userOrgConnectedByEmail = await ArrangeOtherOrgConnectedByEmailAsync(organizationUserRepository, organizationRepository, policyRepository, user, policyType);

        var userOrgConnectedByUserId = await ArrangeOtherOrgConnectedByUserIdAsync(organizationUserRepository, organizationRepository, policyRepository, user, policyType);

        // Act
        var results = (await policyRepository.GetPolicyDetailsByOrganizationIdAsync(userOrgConnectedDirectly.OrganizationId, policyType)).ToList();

        // Assert
        AssertPolicyDetailUserConnections(results, userOrgConnectedDirectly, userOrgConnectedByEmail, userOrgConnectedByUserId);
    }

    [DatabaseTheory, DatabaseData]
    public async Task ShouldReturnUserIds(
        IUserRepository userRepository,
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationRepository organizationRepository,
        IPolicyRepository policyRepository)
    {
        // Arrange
        var user1 = await userRepository.CreateTestUserAsync();
        var user2 = await userRepository.CreateTestUserAsync();
        const PolicyType policyType = PolicyType.SingleOrg;

        var organization = await CreateEnterpriseOrg(organizationRepository);
        await policyRepository.CreateAsync(new Policy { OrganizationId = organization.Id, Enabled = true, Type = policyType });

        var orgUser1 = await organizationUserRepository.CreateTestOrganizationUserAsync(organization, user1);
        var orgUser2 = await organizationUserRepository.CreateTestOrganizationUserAsync(organization, user2);

        // Act
        var results = (await policyRepository.GetPolicyDetailsByOrganizationIdAsync(organization.Id, policyType)).ToList();

        // Assert
        Assert.Equal(2, results.Count);

        Assert.Contains(results, result => result.OrganizationUserId == orgUser1.Id
                                           && result.UserId == orgUser1.UserId
                                           && result.OrganizationId == orgUser1.OrganizationId);

        Assert.Contains(results, result => result.OrganizationUserId == orgUser2.Id
                                           && result.UserId == orgUser2.UserId
                                           && result.OrganizationId == orgUser2.OrganizationId);
    }


    private async Task<OrganizationUser> ArrangeOtherOrgConnectedByUserIdAsync(IOrganizationUserRepository organizationUserRepository,
        IOrganizationRepository organizationRepository, IPolicyRepository policyRepository, User user,
        PolicyType policyType)
    {
        var organization = await CreateEnterpriseOrg(organizationRepository);

        var organizationUser = await organizationUserRepository.CreateTestOrganizationUserAsync(organization, user);
        await policyRepository.CreateAsync(new Policy { OrganizationId = organization.Id, Enabled = true, Type = policyType });

        return organizationUser;
    }

    private async Task<OrganizationUser> ArrangeDirectlyConnectedOrgByUserIdAsync(IOrganizationUserRepository organizationUserRepository,
        IOrganizationRepository organizationRepository, IPolicyRepository policyRepository, User user,
        PolicyType policyType)
    {
        var organization = await CreateEnterpriseOrg(organizationRepository);

        var organizationUser = await organizationUserRepository.CreateTestOrganizationUserAsync(organization, user);

        await policyRepository.CreateAsync(new Policy { OrganizationId = organization.Id, Enabled = true, Type = policyType });

        return organizationUser;
    }

    private static void AssertPolicyDetailUserConnections(List<OrganizationPolicyDetails> results,
        OrganizationUser userOrgConnectedDirectly,
        OrganizationUser userOrgConnectedByEmail,
        OrganizationUser userOrgConnectedByUserId)
    {
        Assert.Contains(results, result => result.OrganizationUserId == userOrgConnectedDirectly.Id
                                           && result.OrganizationId == userOrgConnectedDirectly.OrganizationId);
        Assert.Contains(results, result => result.OrganizationUserId == userOrgConnectedByEmail.Id
                                           && result.OrganizationId == userOrgConnectedByEmail.OrganizationId);
        Assert.Contains(results, result => result.OrganizationUserId == userOrgConnectedByUserId.Id
                                           && result.OrganizationId == userOrgConnectedByUserId.OrganizationId);
    }

    private async Task<OrganizationUser> ArrangeOtherOrgConnectedByEmailAsync(IOrganizationUserRepository organizationUserRepository,
        IOrganizationRepository organizationRepository, IPolicyRepository policyRepository, User user,
        PolicyType policyType)
    {
        var organization = await CreateEnterpriseOrg(organizationRepository);
        var organizationUser = new OrganizationUser
        {
            OrganizationId = organization.Id,
            UserId = null,
            Status = OrganizationUserStatusType.Invited,
            Type = OrganizationUserType.Custom,
            Email = user.Email
        };
        await organizationUserRepository.CreateAsync(organizationUser);
        await policyRepository.CreateAsync(new Policy { OrganizationId = organization.Id, Enabled = true, Type = policyType });

        return organizationUser;
    }

    private async Task<OrganizationUser> ArrangeDirectlyConnectedOrgByEmailAsync(IOrganizationUserRepository organizationUserRepository,
        IOrganizationRepository organizationRepository, IPolicyRepository policyRepository, User user,
        PolicyType policyType)
    {
        var organization = await CreateEnterpriseOrg(organizationRepository);
        var organizationUser = new OrganizationUser
        {
            OrganizationId = organization.Id,
            UserId = null,
            Status = OrganizationUserStatusType.Invited,
            Type = OrganizationUserType.Custom,
            Email = user.Email
        };
        await organizationUserRepository.CreateAsync(organizationUser);

        await policyRepository.CreateAsync(new Policy { OrganizationId = organization.Id, Enabled = true, Type = policyType });

        return organizationUser;
    }

    private Task<Organization> CreateEnterpriseOrg(IOrganizationRepository orgRepo)
        => orgRepo.CreateAsync(new Organization
        {
            Name = System.Guid.NewGuid().ToString(),
            BillingEmail = "billing@example.com",
            Plan = "Test",
            PlanType = PlanType.EnterpriseAnnually,
            UsePolicies = true
        });
}
