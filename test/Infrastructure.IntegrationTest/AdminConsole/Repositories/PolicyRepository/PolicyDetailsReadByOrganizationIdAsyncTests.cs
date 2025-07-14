using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing.Enums;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.AdminConsole.Repositories.PolicyRepository;

public class PolicyDetailsReadByOrganizationIdAsyncTests
{
    [DatabaseTheory, DatabaseData]
    public async Task PolicyDetailsReadByOrganizationIdAsync(
        IUserRepository userRepository,
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationRepository organizationRepository,
        IPolicyRepository policyRepository)
    {
        // Arrange
        var userA = await userRepository.CreateTestUserAsync();
        var org1 = await organizationRepository.CreateTestOrganizationAsync();
        // direct OrgUser in Org1
        var userAOrg1 = await organizationUserRepository.CreateTestOrganizationUserAsync(org1, userA);
        const PolicyType policyType = PolicyType.SingleOrg;

        await policyRepository.CreateAsync(new Policy { OrganizationId = org1.Id, Enabled = true, Type = policyType });

        // Org2 via UserId → UserId
        var org2 = await CreateEnterpriseOrg(organizationRepository);
        var userAOrg2 = new OrganizationUser
        {
            OrganizationId = org2.Id,
            UserId = userA.Id,
            Status = OrganizationUserStatusType.Confirmed,
            Type = OrganizationUserType.Custom,
            Email = null
        };
        await organizationUserRepository.CreateAsync(userAOrg2);
        await policyRepository.CreateAsync(new Policy { OrganizationId = org2.Id, Enabled = true, Type = policyType });

        // Act
        var results = (await policyRepository.PolicyDetailsReadByOrganizationIdAsync(org1.Id, policyType)).ToList();

        // Assert
        Assert.Contains(results, result => result.OrganizationUserId == userAOrg1.Id && result.OrganizationId == userAOrg1.OrganizationId);
        Assert.Contains(results, result => result.OrganizationUserId == userAOrg2.Id && result.OrganizationId == userAOrg2.OrganizationId);

    }

    [DatabaseTheory, DatabaseData]
    public async Task PolicyDetailsReadByOrganizationIdAsync_OnlyReturnInputPolicyType(
        IUserRepository userRepository,
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationRepository organizationRepository,
        IPolicyRepository policyRepository)
    {
        // Arrange
        var userA = await userRepository.CreateTestUserAsync();
        var org1 = await CreateEnterpriseOrg(organizationRepository);

        var orgUser1 = await organizationUserRepository.CreateTestOrganizationUserAsync(org1, userA);

        const PolicyType inputPolicyType = PolicyType.SingleOrg;
        await policyRepository.CreateAsync(new Policy { OrganizationId = org1.Id, Enabled = true, Type = inputPolicyType });

        const PolicyType notInputPolicyType = PolicyType.RequireSso;
        await policyRepository.CreateAsync(new Policy { OrganizationId = org1.Id, Enabled = true, Type = notInputPolicyType });

        // Act
        var results = (await policyRepository.PolicyDetailsReadByOrganizationIdAsync(org1.Id, inputPolicyType)).ToList();

        // Assert
        Assert.Contains(results, result => result.OrganizationUserId == orgUser1.Id
                                      && result.OrganizationId == orgUser1.OrganizationId
                                      && result.PolicyType == inputPolicyType);

        Assert.DoesNotContain(results, result => result.PolicyType == notInputPolicyType);
    }


    [DatabaseTheory, DatabaseData]
    public async Task JimmyTodoTestFor_ShouldNotReturnUserIfTheOtherOrgDoesNotThePolicy(
        IUserRepository userRepository,
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationRepository organizationRepository,
        IPolicyRepository policyRepository)
    {
        // Arrange
        var userA = await userRepository.CreateTestUserAsync();
        var org1 = await CreateEnterpriseOrg(organizationRepository);
        // direct OrgUser in Org1
        var orgUser1 = await organizationUserRepository.CreateTestOrganizationUserAsync(org1, userA);
        await policyRepository.CreateAsync(new Policy { OrganizationId = org1.Id, Enabled = true, Type = PolicyType.SingleOrg });

        // Org2 via UserId → UserId
        var org2 = await CreateEnterpriseOrg(organizationRepository);

        await policyRepository.CreateAsync(new Policy { OrganizationId = org2.Id, Enabled = true, Type = PolicyType.SingleOrg });

        // Act
        var results = (await policyRepository.PolicyDetailsReadByOrganizationIdAsync(org1.Id, PolicyType.SingleOrg)).ToList();

        // Assert
        Assert.Contains(results, result => result.OrganizationUserId == orgUser1.Id && result.OrganizationId == orgUser1.OrganizationId);
        Assert.DoesNotContain(results, result => result.OrganizationId == org2.Id);

    }

    [DatabaseTheory, DatabaseData]
    public async Task JimmyDone_ShouldNotReturnUserOtherOrgsIfNoUserIsConnected(
        IUserRepository userRepository,
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationRepository organizationRepository,
        IPolicyRepository policyRepository)
    {
        // Arrange
        var userA = await userRepository.CreateTestUserAsync();
        var org1 = await CreateEnterpriseOrg(organizationRepository);

        var orgUser1 = await organizationUserRepository.CreateTestOrganizationUserAsync(org1, userA);
        await policyRepository.CreateAsync(new Policy { OrganizationId = org1.Id, Enabled = true, Type = PolicyType.SingleOrg });


        var org2 = await CreateEnterpriseOrg(organizationRepository);

        await policyRepository.CreateAsync(new Policy { OrganizationId = org2.Id, Enabled = true, Type = PolicyType.SingleOrg });

        // Act
        var results = (await policyRepository.PolicyDetailsReadByOrganizationIdAsync(org1.Id, PolicyType.SingleOrg)).ToList();

        // Assert
        Assert.Contains(results, result => result.OrganizationUserId == orgUser1.Id && result.OrganizationId == orgUser1.OrganizationId);
        Assert.DoesNotContain(results, result => result.OrganizationId == org2.Id);

    }



    [DatabaseTheory, DatabaseData]
    public async Task EmailToUserId_UseCase2_ReturnsOrg4Policy(
        IUserRepository userRepository,
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationRepository organizationRepository,
        IPolicyRepository policyRepository)
    {
        // Arrange
        var userC = await userRepository.CreateTestUserAsync();
        var org1 = await CreateEnterpriseOrg(organizationRepository);
        // invited OrgUser in Org1 by Email
        var orgUser1 = new OrganizationUser
        {
            OrganizationId = org1.Id,
            UserId = null,
            Status = OrganizationUserStatusType.Invited,
            Type = OrganizationUserType.Custom,
            Email = userC.Email
        };
        await organizationUserRepository.CreateAsync(orgUser1);
        await policyRepository.CreateAsync(new Policy { OrganizationId = org1.Id, Enabled = true, Type = PolicyType.SingleOrg });

        // Org4 via Email → UserId
        var org4 = await CreateEnterpriseOrg(organizationRepository);
        var orgUser4 = new OrganizationUser
        {
            OrganizationId = org4.Id,
            UserId = userC.Id,
            Status = OrganizationUserStatusType.Confirmed,
            Type = OrganizationUserType.Custom,
            Email = null
        };
        await organizationUserRepository.CreateAsync(orgUser4);
        await policyRepository.CreateAsync(new Policy { OrganizationId = org4.Id, Enabled = true, Type = PolicyType.SingleOrg });

        // Act
        var results = await policyRepository.PolicyDetailsReadByOrganizationIdAsync(org1.Id, PolicyType.SingleOrg);

        // Assert
        Assert.Contains(results, result => result.OrganizationUserId == orgUser4.Id && result.OrganizationId == org4.Id);
    }

    [DatabaseTheory, DatabaseData]
    public async Task UserIdToEmail_UseCase3_ReturnsOrg3Policy(
        IUserRepository userRepository,
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationRepository organizationRepository,
        IPolicyRepository policyRepository)
    {
        // Arrange
        var userB = await userRepository.CreateTestUserAsync();
        var org1 = await CreateEnterpriseOrg(organizationRepository);
        // direct OrgUser in Org1
        var orgUser1 = new OrganizationUser
        {
            OrganizationId = org1.Id,
            UserId = userB.Id,
            Status = OrganizationUserStatusType.Confirmed,
            Type = OrganizationUserType.Custom,
            Email = null
        };
        await organizationUserRepository.CreateAsync(orgUser1);
        await policyRepository.CreateAsync(new Policy { OrganizationId = org1.Id, Enabled = true, Type = PolicyType.SingleOrg });

        // Org3 via UserId → Email
        var org3 = await CreateEnterpriseOrg(organizationRepository);
        var orgUser3 = new OrganizationUser
        {
            OrganizationId = org3.Id,
            UserId = null,
            Status = OrganizationUserStatusType.Invited,
            Type = OrganizationUserType.Custom,
            Email = userB.Email
        };
        await organizationUserRepository.CreateAsync(orgUser3);
        await policyRepository.CreateAsync(new Policy { OrganizationId = org3.Id, Enabled = true, Type = PolicyType.SingleOrg });

        // Act
        var results = await policyRepository.PolicyDetailsReadByOrganizationIdAsync(org1.Id, PolicyType.SingleOrg);

        // Assert
        Assert.Contains(results, result => result.OrganizationUserId == orgUser3.Id && result.OrganizationId == org3.Id);
    }

    [DatabaseTheory, DatabaseData]
    public async Task PolicyDetailsReadByOrganizationIdAsync_GetUsersByEmails(
        IUserRepository userRepository,
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationRepository organizationRepository,
        IPolicyRepository policyRepository)
    {
        // Arrange
        var userA = await userRepository.CreateTestUserAsync();
        var orgDirectlyConnected = await CreateEnterpriseOrg(organizationRepository);
        var userOrgDirectlyConnected = new OrganizationUser
        {
            OrganizationId = orgDirectlyConnected.Id,
            UserId = null,
            Status = OrganizationUserStatusType.Invited,
            Type = OrganizationUserType.Custom,
            Email = userA.Email
        };
        await organizationUserRepository.CreateAsync(userOrgDirectlyConnected);
        var policyType = PolicyType.SingleOrg;

        await policyRepository.CreateAsync(new Policy { OrganizationId = orgDirectlyConnected.Id, Enabled = true, Type = policyType });

        var org2 = await CreateEnterpriseOrg(organizationRepository);
        var userAOrg2 = new OrganizationUser
        {
            OrganizationId = org2.Id,
            UserId = null,
            Status = OrganizationUserStatusType.Invited,
            Type = OrganizationUserType.Custom,
            Email = userA.Email
        };
        await organizationUserRepository.CreateAsync(userAOrg2);
        await policyRepository.CreateAsync(new Policy { OrganizationId = org2.Id, Enabled = true, Type = policyType });



        var orgWithUserIdConnection = await CreateEnterpriseOrg(organizationRepository);

        var orgUserWithUserIdConnection = await organizationUserRepository.CreateTestOrganizationUserAsync(orgWithUserIdConnection, userA);
        await policyRepository.CreateAsync(new Policy { OrganizationId = orgWithUserIdConnection.Id, Enabled = true, Type = PolicyType.SingleOrg });


        // Act
        var results = (await policyRepository.PolicyDetailsReadByOrganizationIdAsync(orgWithUserIdConnection.Id, policyType)).ToList();

        // Assert
        Assert.Contains(results, result => result.OrganizationUserId == userAOrg2.Id && result.OrganizationId == org2.Id);
        Assert.Contains(results, result => result.OrganizationUserId == userAOrg2.Id && result.OrganizationId == org2.Id);
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
