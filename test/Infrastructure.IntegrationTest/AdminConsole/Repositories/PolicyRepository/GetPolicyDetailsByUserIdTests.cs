using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing.Enums;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Bit.Core.Utilities;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.AdminConsole.Repositories.PolicyRepository;

public class GetPolicyDetailsByUserIdTests
{
    [DatabaseTheory, DatabaseData]
    public async Task GetPolicyDetailsByUserId_NonInvitedUsers_Works(
        IUserRepository userRepository,
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationRepository organizationRepository,
        IPolicyRepository policyRepository)
    {
        // Arrange
        // OrgUser1 - owner of org1 - confirmed
        var user = await userRepository.CreateTestUserAsync();
        var org1 = await CreateEnterpriseOrg(organizationRepository);
        var orgUser1 = new OrganizationUser
        {
            OrganizationId = org1.Id,
            UserId = user.Id,
            Status = OrganizationUserStatusType.Confirmed,
            Type = OrganizationUserType.Owner,
            Email = null    // confirmed OrgUsers use the email on the User table
        };
        await organizationUserRepository.CreateAsync(orgUser1);
        await policyRepository.CreateAsync(new Policy
        {
            OrganizationId = org1.Id,
            Enabled = true,
            Type = PolicyType.SingleOrg,
            Data = CoreHelpers.ClassToJsonData(new TestPolicyData { BoolSetting = true, IntSetting = 5 })
        });

        // OrgUser2 - custom user of org2 - accepted
        var org2 = await CreateEnterpriseOrg(organizationRepository);
        var orgUser2 = new OrganizationUser
        {
            OrganizationId = org2.Id,
            UserId = user.Id,
            Status = OrganizationUserStatusType.Accepted,
            Type = OrganizationUserType.Custom,
            Email = null    // accepted OrgUsers use the email on the User table
        };
        orgUser2.SetPermissions(new Permissions
        {
            ManagePolicies = true
        });
        await organizationUserRepository.CreateAsync(orgUser2);
        await policyRepository.CreateAsync(new Policy
        {
            OrganizationId = org2.Id,
            Enabled = true,
            Type = PolicyType.SingleOrg,
            Data = CoreHelpers.ClassToJsonData(new TestPolicyData { BoolSetting = false, IntSetting = 15 })
        });

        // Act
        var policyDetails = (await policyRepository.GetPolicyDetailsByUserId(user.Id)).ToList();

        // Assert
        Assert.Equal(2, policyDetails.Count);

        var actualPolicyDetails1 = policyDetails.Find(p => p.OrganizationUserId == orgUser1.Id);
        var expectedPolicyDetails1 = new PolicyDetails
        {
            OrganizationUserId = orgUser1.Id,
            OrganizationId = org1.Id,
            PolicyType = PolicyType.SingleOrg,
            PolicyData = CoreHelpers.ClassToJsonData(new TestPolicyData { BoolSetting = true, IntSetting = 5 }),
            OrganizationUserType = OrganizationUserType.Owner,
            OrganizationUserStatus = OrganizationUserStatusType.Confirmed,
            OrganizationUserPermissionsData = null,
            IsProvider = false
        };
        Assert.Equivalent(expectedPolicyDetails1, actualPolicyDetails1);
        Assert.Equivalent(expectedPolicyDetails1.GetDataModel<TestPolicyData>(), new TestPolicyData { BoolSetting = true, IntSetting = 5 });

        var actualPolicyDetails2 = policyDetails.Find(p => p.OrganizationUserId == orgUser2.Id);
        var expectedPolicyDetails2 = new PolicyDetails
        {
            OrganizationUserId = orgUser2.Id,
            OrganizationId = org2.Id,
            PolicyType = PolicyType.SingleOrg,
            PolicyData = CoreHelpers.ClassToJsonData(new TestPolicyData { BoolSetting = false, IntSetting = 15 }),
            OrganizationUserType = OrganizationUserType.Custom,
            OrganizationUserStatus = OrganizationUserStatusType.Accepted,
            OrganizationUserPermissionsData = CoreHelpers.ClassToJsonData(new Permissions { ManagePolicies = true }),
            IsProvider = false
        };
        Assert.Equivalent(expectedPolicyDetails2, actualPolicyDetails2);
        Assert.Equivalent(expectedPolicyDetails2.GetDataModel<TestPolicyData>(), new TestPolicyData { BoolSetting = false, IntSetting = 15 });
        Assert.Equivalent(new Permissions { ManagePolicies = true }, actualPolicyDetails2.GetOrganizationUserCustomPermissions(), strict: true);
    }

    [DatabaseTheory, DatabaseData]
    public async Task GetPolicyDetailsByUserId_InvitedUser_Works(
        IUserRepository userRepository,
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationRepository organizationRepository,
        IPolicyRepository policyRepository)
    {
        // Arrange
        var user = await userRepository.CreateTestUserAsync();
        var org = await CreateEnterpriseOrg(organizationRepository);
        var orgUser = new OrganizationUser
        {
            OrganizationId = org.Id,
            UserId = null, // invited users have null userId
            Status = OrganizationUserStatusType.Invited,
            Type = OrganizationUserType.Custom,
            Email = user.Email  // invited users have matching Email
        };
        await organizationUserRepository.CreateAsync(orgUser);
        await policyRepository.CreateAsync(new Policy
        {
            OrganizationId = org.Id,
            Enabled = true,
            Type = PolicyType.SingleOrg,
        });

        // Act
        var actualPolicyDetails = await policyRepository.GetPolicyDetailsByUserId(user.Id);

        // Assert
        var expectedPolicyDetails = new PolicyDetails
        {
            OrganizationUserId = orgUser.Id,
            OrganizationId = org.Id,
            PolicyType = PolicyType.SingleOrg,
            OrganizationUserType = OrganizationUserType.Custom,
            OrganizationUserStatus = OrganizationUserStatusType.Invited,
            IsProvider = false
        };

        Assert.Equivalent(expectedPolicyDetails, actualPolicyDetails.Single());
    }

    [DatabaseTheory, DatabaseData]
    public async Task GetPolicyDetailsByUserId_RevokedConfirmedUser_Works(
        IUserRepository userRepository,
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationRepository organizationRepository,
        IPolicyRepository policyRepository)
    {
        // Arrange
        var user = await userRepository.CreateTestUserAsync();
        var org = await CreateEnterpriseOrg(organizationRepository);
        // User has been confirmed to the org but then revoked
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
        var actualPolicyDetails = await policyRepository.GetPolicyDetailsByUserId(user.Id);

        // Assert
        var expectedPolicyDetails = new PolicyDetails
        {
            OrganizationUserId = orgUser.Id,
            OrganizationId = org.Id,
            PolicyType = PolicyType.SingleOrg,
            OrganizationUserType = OrganizationUserType.Owner,
            OrganizationUserStatus = OrganizationUserStatusType.Revoked,
            IsProvider = false
        };

        Assert.Equivalent(expectedPolicyDetails, actualPolicyDetails.Single());
    }

    [DatabaseTheory, DatabaseData]
    public async Task GetPolicyDetailsByUserId_RevokedInvitedUser_DoesntReturnPolicies(
        IUserRepository userRepository,
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationRepository organizationRepository,
        IPolicyRepository policyRepository)
    {
        // Arrange
        var user = await userRepository.CreateTestUserAsync();
        var org = await CreateEnterpriseOrg(organizationRepository);
        // User has been invited to the org but then revoked - without ever being confirmed and linked to a user.
        // This is an unhandled edge case because those users will go through policy enforcement later,
        // as part of accepting their invite after being restored. For now this is just documented as expected behavior.
        var orgUser = new OrganizationUser
        {
            OrganizationId = org.Id,
            UserId = null,
            Status = OrganizationUserStatusType.Revoked,
            Type = OrganizationUserType.Owner,
            Email = user.Email
        };
        await organizationUserRepository.CreateAsync(orgUser);
        await policyRepository.CreateAsync(new Policy
        {
            OrganizationId = org.Id,
            Enabled = true,
            Type = PolicyType.SingleOrg,
        });

        // Act
        var actualPolicyDetails = await policyRepository.GetPolicyDetailsByUserId(user.Id);

        Assert.Empty(actualPolicyDetails);
    }

    [DatabaseTheory, DatabaseData]
    public async Task GetPolicyDetailsByUserId_SetsIsProvider(
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
        var org = await CreateEnterpriseOrg(organizationRepository);
        var orgUser = await organizationUserRepository.CreateTestOrganizationUserAsync(org, user);
        await policyRepository.CreateAsync(new Policy
        {
            OrganizationId = org.Id,
            Enabled = true,
            Type = PolicyType.SingleOrg,
        });

        // Arrange provider
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
        var actualPolicyDetails = await policyRepository.GetPolicyDetailsByUserId(user.Id);

        // Assert
        var expectedPolicyDetails = new PolicyDetails
        {
            OrganizationUserId = orgUser.Id,
            OrganizationId = org.Id,
            PolicyType = PolicyType.SingleOrg,
            OrganizationUserType = OrganizationUserType.Owner,
            OrganizationUserStatus = OrganizationUserStatusType.Confirmed,
            IsProvider = true
        };

        Assert.Equivalent(expectedPolicyDetails, actualPolicyDetails.Single());
    }

    [DatabaseTheory, DatabaseData]
    public async Task GetPolicyDetailsByUserId_IgnoresDisabledOrganizations(
        IUserRepository userRepository,
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationRepository organizationRepository,
        IPolicyRepository policyRepository)
    {
        // Arrange
        var user = await userRepository.CreateTestUserAsync();
        var org = await CreateEnterpriseOrg(organizationRepository);
        await organizationUserRepository.CreateTestOrganizationUserAsync(org, user);
        await policyRepository.CreateAsync(new Policy
        {
            OrganizationId = org.Id,
            Enabled = true,
            Type = PolicyType.SingleOrg,
        });

        // Org is disabled; its policies remain, but it is now inactive
        org.Enabled = false;
        await organizationRepository.ReplaceAsync(org);

        // Act
        var actualPolicyDetails = await policyRepository.GetPolicyDetailsByUserId(user.Id);

        // Assert
        Assert.Empty(actualPolicyDetails);
    }

    [DatabaseTheory, DatabaseData]
    public async Task GetPolicyDetailsByUserId_IgnoresDowngradedOrganizations(
        IUserRepository userRepository,
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationRepository organizationRepository,
        IPolicyRepository policyRepository)
    {
        // Arrange
        var user = await userRepository.CreateTestUserAsync();
        var org = await CreateEnterpriseOrg(organizationRepository);
        await organizationUserRepository.CreateTestOrganizationUserAsync(org, user);
        await policyRepository.CreateAsync(new Policy
        {
            OrganizationId = org.Id,
            Enabled = true,
            Type = PolicyType.SingleOrg,
        });

        // Org is downgraded; its policies remain but its plan no longer supports them
        org.UsePolicies = false;
        org.PlanType = PlanType.TeamsAnnually;
        await organizationRepository.ReplaceAsync(org);

        // Act
        var actualPolicyDetails = await policyRepository.GetPolicyDetailsByUserId(user.Id);

        // Assert
        Assert.Empty(actualPolicyDetails);
    }

    [DatabaseTheory, DatabaseData]
    public async Task GetPolicyDetailsByUserId_IgnoresDisabledPolicies(
        IUserRepository userRepository,
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationRepository organizationRepository,
        IPolicyRepository policyRepository)
    {
        // Arrange
        var user = await userRepository.CreateTestUserAsync();
        var org = await CreateEnterpriseOrg(organizationRepository);
        await organizationUserRepository.CreateTestOrganizationUserAsync(org, user);
        await policyRepository.CreateAsync(new Policy
        {
            OrganizationId = org.Id,
            Enabled = false,
            Type = PolicyType.SingleOrg,
        });

        // Act
        var actualPolicyDetails = await policyRepository.GetPolicyDetailsByUserId(user.Id);

        // Assert
        Assert.Empty(actualPolicyDetails);
    }

    private class TestPolicyData : IPolicyDataModel
    {
        public bool BoolSetting { get; set; }
        public int IntSetting { get; set; }
    }

    private Task<Organization> CreateEnterpriseOrg(IOrganizationRepository organizationRepository)
        => organizationRepository.CreateAsync(new Organization
        {
            Name = Guid.NewGuid().ToString(),
            BillingEmail = "billing@example.com", // TODO: EF does not enforce this being NOT NULL
            Plan = "Test", // TODO: EF does not enforce this being NOT NULl
            PlanType = PlanType.EnterpriseAnnually,
            UsePolicies = true
        });
}
