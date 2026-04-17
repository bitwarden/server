using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Bit.Core.Utilities;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.AdminConsole.Repositories.OrganizationUserRepository;

public class GetByUserIdWithPolicyDetailsTests
{
    [Theory, DatabaseData]
    public async Task GetByUserIdWithPolicyDetailsAsync_WithConfirmedUser_ReturnsPolicy(
        IUserRepository userRepository,
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationRepository organizationRepository,
        IPolicyRepository policyRepository)
    {
        // Arrange
        var user = await userRepository.CreateTestUserAsync();
        var org = await organizationRepository.CreateAsync(new Organization
        {
            Name = "Test Org",
            BillingEmail = "billing@example.com",
            Plan = "Test",
        });
        var orgUser = new OrganizationUser
        {
            OrganizationId = org.Id,
            UserId = user.Id,
            Status = OrganizationUserStatusType.Confirmed,
            Type = OrganizationUserType.User,
            Email = null
        };
        await organizationUserRepository.CreateAsync(orgUser);
        await policyRepository.CreateAsync(new Policy
        {
            OrganizationId = org.Id,
            Enabled = true,
            Type = PolicyType.SingleOrg,
            Data = CoreHelpers.ClassToJsonData(new { Setting = "value" })
        });

        // Act
        var result = await organizationUserRepository.GetByUserIdWithPolicyDetailsAsync(user.Id, PolicyType.SingleOrg);

        // Assert
        var policyDetails = result.Single();
        Assert.Equal(orgUser.Id, policyDetails.OrganizationUserId);
        Assert.Equal(org.Id, policyDetails.OrganizationId);
        Assert.Equal(PolicyType.SingleOrg, policyDetails.PolicyType);
        Assert.True(policyDetails.PolicyEnabled);
        Assert.Equal(OrganizationUserType.User, policyDetails.OrganizationUserType);
        Assert.Equal(OrganizationUserStatusType.Confirmed, policyDetails.OrganizationUserStatus);
        Assert.False(policyDetails.IsProvider);
    }

    [Theory, DatabaseData]
    public async Task GetByUserIdWithPolicyDetailsAsync_WithAcceptedUser_ReturnsPolicy(
        IUserRepository userRepository,
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationRepository organizationRepository,
        IPolicyRepository policyRepository)
    {
        // Arrange
        var user = await userRepository.CreateTestUserAsync();
        var org = await organizationRepository.CreateAsync(new Organization
        {
            Name = "Test Org",
            BillingEmail = "billing@example.com",
            Plan = "Test",
        });
        var orgUser = new OrganizationUser
        {
            OrganizationId = org.Id,
            UserId = user.Id,
            Status = OrganizationUserStatusType.Accepted,
            Type = OrganizationUserType.Admin,
            Email = null
        };
        await organizationUserRepository.CreateAsync(orgUser);
        await policyRepository.CreateAsync(new Policy
        {
            OrganizationId = org.Id,
            Enabled = false, // Note: disabled policy
            Type = PolicyType.RequireSso,
        });

        // Act
        var result = await organizationUserRepository.GetByUserIdWithPolicyDetailsAsync(user.Id, PolicyType.RequireSso);

        // Assert
        var policyDetails = result.Single();
        Assert.Equal(orgUser.Id, policyDetails.OrganizationUserId);
        Assert.False(policyDetails.PolicyEnabled); // Should return even if disabled
    }

    [Theory, DatabaseData]
    public async Task GetByUserIdWithPolicyDetailsAsync_WithInvitedUser_ReturnsPolicy(
        IUserRepository userRepository,
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationRepository organizationRepository,
        IPolicyRepository policyRepository)
    {
        // Arrange
        var user = await userRepository.CreateTestUserAsync();
        var org = await organizationRepository.CreateAsync(new Organization
        {
            Name = "Test Org",
            BillingEmail = "billing@example.com",
            Plan = "Test",
        });
        var orgUser = new OrganizationUser
        {
            OrganizationId = org.Id,
            UserId = null, // invited users have null userId
            Status = OrganizationUserStatusType.Invited,
            Type = OrganizationUserType.User,
            Email = user.Email  // invited users have matching Email
        };
        await organizationUserRepository.CreateAsync(orgUser);
        await policyRepository.CreateAsync(new Policy
        {
            OrganizationId = org.Id,
            Enabled = true,
            Type = PolicyType.TwoFactorAuthentication,
        });

        // Act
        var result = await organizationUserRepository.GetByUserIdWithPolicyDetailsAsync(user.Id, PolicyType.TwoFactorAuthentication);

        // Assert
        var policyDetails = result.Single();
        Assert.Equal(orgUser.Id, policyDetails.OrganizationUserId);
        Assert.Equal(OrganizationUserStatusType.Invited, policyDetails.OrganizationUserStatus);
    }

    [Theory, DatabaseData]
    public async Task GetByUserIdWithPolicyDetailsAsync_WithRevokedUser_ReturnsPolicy(
        IUserRepository userRepository,
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationRepository organizationRepository,
        IPolicyRepository policyRepository)
    {
        // Arrange
        var user = await userRepository.CreateTestUserAsync();
        var org = await organizationRepository.CreateAsync(new Organization
        {
            Name = "Test Org",
            BillingEmail = "billing@example.com",
            Plan = "Test",
        });
        var orgUser = new OrganizationUser
        {
            OrganizationId = org.Id,
            UserId = user.Id,
            Status = OrganizationUserStatusType.Revoked,
            Type = OrganizationUserType.Owner,
            Email = null
        };
        await organizationUserRepository.CreateAsync(orgUser);
        await policyRepository.CreateAsync(new Policy
        {
            OrganizationId = org.Id,
            Enabled = true,
            Type = PolicyType.SingleOrg,
        });

        // Act
        var result = await organizationUserRepository.GetByUserIdWithPolicyDetailsAsync(user.Id, PolicyType.SingleOrg);

        // Assert
        var policyDetails = result.Single();
        Assert.Equal(OrganizationUserStatusType.Revoked, policyDetails.OrganizationUserStatus);
    }

    [Theory, DatabaseData]
    public async Task GetByUserIdWithPolicyDetailsAsync_WithMultipleOrganizations_ReturnsAllMatchingPolicies(
        IUserRepository userRepository,
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationRepository organizationRepository,
        IPolicyRepository policyRepository)
    {
        // Arrange
        var user = await userRepository.CreateTestUserAsync();

        // Org1 with SingleOrg policy
        var org1 = await organizationRepository.CreateAsync(new Organization
        {
            Name = "Org 1",
            BillingEmail = "billing@example.com",
            Plan = "Test",
        });
        var orgUser1 = new OrganizationUser
        {
            OrganizationId = org1.Id,
            UserId = user.Id,
            Status = OrganizationUserStatusType.Confirmed,
            Type = OrganizationUserType.User,
        };
        await organizationUserRepository.CreateAsync(orgUser1);
        await policyRepository.CreateAsync(new Policy
        {
            OrganizationId = org1.Id,
            Enabled = true,
            Type = PolicyType.SingleOrg,
        });

        // Org2 with SingleOrg policy
        var org2 = await organizationRepository.CreateAsync(new Organization
        {
            Name = "Org 2",
            BillingEmail = "billing2@example.com",
            Plan = "Test",
        });
        var orgUser2 = new OrganizationUser
        {
            OrganizationId = org2.Id,
            UserId = user.Id,
            Status = OrganizationUserStatusType.Confirmed,
            Type = OrganizationUserType.Admin,
        };
        await organizationUserRepository.CreateAsync(orgUser2);
        await policyRepository.CreateAsync(new Policy
        {
            OrganizationId = org2.Id,
            Enabled = true,
            Type = PolicyType.SingleOrg,
        });

        // Org3 with RequireSso policy (different type - should not be returned)
        var org3 = await organizationRepository.CreateAsync(new Organization
        {
            Name = "Org 3",
            BillingEmail = "billing3@example.com",
            Plan = "Test",
        });
        var orgUser3 = new OrganizationUser
        {
            OrganizationId = org3.Id,
            UserId = user.Id,
            Status = OrganizationUserStatusType.Confirmed,
            Type = OrganizationUserType.Owner,
        };
        await organizationUserRepository.CreateAsync(orgUser3);
        await policyRepository.CreateAsync(new Policy
        {
            OrganizationId = org3.Id,
            Enabled = true,
            Type = PolicyType.RequireSso,
        });

        // Act
        var result = (await organizationUserRepository.GetByUserIdWithPolicyDetailsAsync(user.Id, PolicyType.SingleOrg)).ToList();

        // Assert - should only get 2 policies (org1 and org2)
        Assert.Equal(2, result.Count);
        Assert.Contains(result, p => p.OrganizationId == org1.Id && p.OrganizationUserType == OrganizationUserType.User);
        Assert.Contains(result, p => p.OrganizationId == org2.Id && p.OrganizationUserType == OrganizationUserType.Admin);
        Assert.DoesNotContain(result, p => p.OrganizationId == org3.Id);
    }

    [Theory, DatabaseData]
    public async Task GetByUserIdWithPolicyDetailsAsync_WithNonExistingPolicyType_ReturnsEmpty(
        IUserRepository userRepository,
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationRepository organizationRepository,
        IPolicyRepository policyRepository)
    {
        // Arrange
        var user = await userRepository.CreateTestUserAsync();
        var org = await organizationRepository.CreateAsync(new Organization
        {
            Name = "Test Org",
            BillingEmail = "billing@example.com",
            Plan = "Test",
        });
        await organizationUserRepository.CreateTestOrganizationUserAsync(org, user);
        await policyRepository.CreateAsync(new Policy
        {
            OrganizationId = org.Id,
            Enabled = true,
            Type = PolicyType.SingleOrg,
        });

        // Act
        var result = await organizationUserRepository.GetByUserIdWithPolicyDetailsAsync(user.Id, PolicyType.RequireSso);

        // Assert
        Assert.Empty(result);
    }

    [Theory, DatabaseData]
    public async Task GetByUserIdWithPolicyDetailsAsync_WithProviderUser_ReturnsIsProviderTrue(
        IUserRepository userRepository,
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationRepository organizationRepository,
        IPolicyRepository policyRepository,
        IProviderRepository providerRepository,
        IProviderUserRepository providerUserRepository,
        IProviderOrganizationRepository providerOrganizationRepository)
    {
        // Arrange
        var user = await userRepository.CreateTestUserAsync();
        var org = await organizationRepository.CreateAsync(new Organization
        {
            Name = "Test Org",
            BillingEmail = "billing@example.com",
            Plan = "Test",
        });
        var orgUser = await organizationUserRepository.CreateTestOrganizationUserAsync(org, user);
        await policyRepository.CreateAsync(new Policy
        {
            OrganizationId = org.Id,
            Enabled = true,
            Type = PolicyType.SingleOrg,
        });
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
            OrganizationId = org.Id,
            ProviderId = provider.Id
        });

        // Act
        var result = await organizationUserRepository.GetByUserIdWithPolicyDetailsAsync(user.Id, PolicyType.SingleOrg);

        // Assert
        var policyDetails = result.Single();
        Assert.True(policyDetails.IsProvider);
    }

    [Theory, DatabaseData]
    public async Task GetByUserIdWithPolicyDetailsAsync_WithCustomUserWithPermissions_ReturnsPermissions(
        IUserRepository userRepository,
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationRepository organizationRepository,
        IPolicyRepository policyRepository)
    {
        // Arrange
        var user = await userRepository.CreateTestUserAsync();
        var org = await organizationRepository.CreateAsync(new Organization
        {
            Name = "Test Org",
            BillingEmail = "billing@example.com",
            Plan = "Test",
        });
        var orgUser = new OrganizationUser
        {
            OrganizationId = org.Id,
            UserId = user.Id,
            Status = OrganizationUserStatusType.Confirmed,
            Type = OrganizationUserType.Custom,
            Email = null
        };
        orgUser.SetPermissions(new Permissions
        {
            ManagePolicies = true,
            EditAnyCollection = true
        });
        await organizationUserRepository.CreateAsync(orgUser);
        await policyRepository.CreateAsync(new Policy
        {
            OrganizationId = org.Id,
            Enabled = true,
            Type = PolicyType.SingleOrg,
        });

        // Act
        var result = await organizationUserRepository.GetByUserIdWithPolicyDetailsAsync(user.Id, PolicyType.SingleOrg);

        // Assert
        var policyDetails = result.Single();
        Assert.NotNull(policyDetails.OrganizationUserPermissionsData);
        var permissions = CoreHelpers.LoadClassFromJsonData<Permissions>(policyDetails.OrganizationUserPermissionsData);
        Assert.True(permissions.ManagePolicies);
        Assert.True(permissions.EditAnyCollection);
    }

    [Theory, DatabaseData]
    public async Task GetByUserIdWithPolicyDetailsAsync_WhenNoPolicyExists_ReturnsEmpty(
        IUserRepository userRepository,
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationRepository organizationRepository)
    {
        // Arrange
        var user = await userRepository.CreateTestUserAsync();
        var org = await organizationRepository.CreateAsync(new Organization
        {
            Name = "Test Org",
            BillingEmail = "billing@example.com",
            Plan = "Test",
        });
        await organizationUserRepository.CreateTestOrganizationUserAsync(org, user);

        // Act
        var result = await organizationUserRepository.GetByUserIdWithPolicyDetailsAsync(user.Id, PolicyType.SingleOrg);

        // Assert
        Assert.Empty(result);
    }

    [Theory, DatabaseData]
    public async Task GetByUserIdWithPolicyDetailsAsync_WhenUserNotInOrg_ReturnsEmpty(
        IUserRepository userRepository,
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationRepository organizationRepository,
        IPolicyRepository policyRepository)
    {
        // Arrange
        var user = await userRepository.CreateTestUserAsync();
        var org = await organizationRepository.CreateAsync(new Organization
        {
            Name = "Test Org",
            BillingEmail = "billing@example.com",
            Plan = "Test",
        });
        await policyRepository.CreateAsync(new Policy
        {
            OrganizationId = org.Id,
            Enabled = true,
            Type = PolicyType.SingleOrg,
        });

        // Act
        var result = await organizationUserRepository.GetByUserIdWithPolicyDetailsAsync(user.Id, PolicyType.SingleOrg);

        // Assert
        Assert.Empty(result);
    }
}

