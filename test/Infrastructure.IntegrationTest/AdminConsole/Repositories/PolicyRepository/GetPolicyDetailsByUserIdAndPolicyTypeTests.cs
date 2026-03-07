using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing.Enums;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.AdminConsole.Repositories.PolicyRepository;

public class GetPolicyDetailsByUserIdAndPolicyTypeTests
{
    [Theory]
    [DatabaseData]
    public async Task GetPolicyDetailsByUserIdAndPolicyTypeAsync_WithConfirmedUser_ReturnsPolicyDetails(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        IPolicyRepository policyRepository)
    {
        // Arrange
        var user = await userRepository.CreateAsync(GetDefaultUser());
        var org = await CreateEnterpriseOrgAsync(organizationRepository);
        var policy = await policyRepository.CreateAsync(new Policy
        {
            OrganizationId = org.Id,
            Type = PolicyType.TwoFactorAuthentication,
            Data = string.Empty,
            Enabled = true
        });
        var orgUser = await organizationUserRepository.CreateAsync(GetConfirmedOrganizationUser(org, user));

        // Act
        var results = await policyRepository.GetPolicyDetailsByUserIdAndPolicyTypeAsync(
            user.Id,
            PolicyType.TwoFactorAuthentication);

        // Assert
        var resultsList = results.ToList();
        Assert.Single(resultsList);
        var result = resultsList.First();
        Assert.Equal(orgUser.Id, result.OrganizationUserId);
        Assert.Equal(org.Id, result.OrganizationId);
        Assert.Equal(PolicyType.TwoFactorAuthentication, result.PolicyType);
        Assert.Equal(policy.Data, result.PolicyData);
        Assert.Equal(OrganizationUserStatusType.Confirmed, result.OrganizationUserStatus);
        Assert.Equal(OrganizationUserType.User, result.OrganizationUserType);
        Assert.False(result.IsProvider);

        // Cleanup
        await organizationRepository.DeleteAsync(org);
        await userRepository.DeleteAsync(user);
    }

    [Theory]
    [DatabaseData]
    public async Task GetPolicyDetailsByUserIdAndPolicyTypeAsync_WithAcceptedUser_ReturnsPolicyDetails(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        IPolicyRepository policyRepository)
    {
        // Arrange
        var user = await userRepository.CreateAsync(GetDefaultUser());
        var org = await CreateEnterpriseOrgAsync(organizationRepository);
        var policy = await policyRepository.CreateAsync(new Policy
        {
            OrganizationId = org.Id,
            Type = PolicyType.MasterPassword,
            Data = "{\"minComplexity\":4}",
            Enabled = true
        });
        var orgUser = await organizationUserRepository.CreateAsync(GetAcceptedOrganizationUser(org, user));

        // Act
        var results = await policyRepository.GetPolicyDetailsByUserIdAndPolicyTypeAsync(
            user.Id,
            PolicyType.MasterPassword);

        // Assert
        var resultsList = results.ToList();
        Assert.Single(resultsList);
        var result = resultsList.First();
        Assert.Equal(orgUser.Id, result.OrganizationUserId);
        Assert.Equal(org.Id, result.OrganizationId);
        Assert.Equal(PolicyType.MasterPassword, result.PolicyType);
        Assert.Equal(policy.Data, result.PolicyData);
        Assert.Equal(OrganizationUserStatusType.Accepted, result.OrganizationUserStatus);
        Assert.False(result.IsProvider);

        // Cleanup
        await organizationRepository.DeleteAsync(org);
        await userRepository.DeleteAsync(user);
    }

    [Theory]
    [DatabaseData]
    public async Task GetPolicyDetailsByUserIdAndPolicyTypeAsync_WithInvitedUser_ReturnsPolicyDetails(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        IPolicyRepository policyRepository)
    {
        // Arrange
        var user = await userRepository.CreateAsync(GetDefaultUser());
        var org = await CreateEnterpriseOrgAsync(organizationRepository);
        var policy = await policyRepository.CreateAsync(new Policy
        {
            OrganizationId = org.Id,
            Type = PolicyType.RequireSso,
            Data = string.Empty,
            Enabled = true
        });
        var orgUser = await organizationUserRepository.CreateAsync(GetInvitedOrganizationUser(org, user));

        // Act
        var results = await policyRepository.GetPolicyDetailsByUserIdAndPolicyTypeAsync(
            user.Id,
            PolicyType.RequireSso);

        // Assert
        var resultsList = results.ToList();
        Assert.Single(resultsList);
        var result = resultsList.First();
        Assert.Equal(orgUser.Id, result.OrganizationUserId);
        Assert.Equal(org.Id, result.OrganizationId);
        Assert.Equal(PolicyType.RequireSso, result.PolicyType);
        Assert.Equal(OrganizationUserStatusType.Invited, result.OrganizationUserStatus);
        Assert.False(result.IsProvider);

        // Cleanup
        await organizationRepository.DeleteAsync(org);
        await userRepository.DeleteAsync(user);
    }

    [Theory]
    [DatabaseData]
    public async Task GetPolicyDetailsByUserIdAndPolicyTypeAsync_WithMultipleOrganizations_ReturnsAllPolicyDetails(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        IPolicyRepository policyRepository)
    {
        // Arrange
        var user = await userRepository.CreateAsync(GetDefaultUser());
        var org1 = await CreateEnterpriseOrgAsync(organizationRepository);
        var org2 = await CreateEnterpriseOrgAsync(organizationRepository);

        var policy1 = await policyRepository.CreateAsync(new Policy
        {
            OrganizationId = org1.Id,
            Type = PolicyType.SingleOrg,
            Data = string.Empty,
            Enabled = true
        });
        var policy2 = await policyRepository.CreateAsync(new Policy
        {
            OrganizationId = org2.Id,
            Type = PolicyType.SingleOrg,
            Data = string.Empty,
            Enabled = true
        });

        var orgUser1 = await organizationUserRepository.CreateAsync(GetConfirmedOrganizationUser(org1, user));
        var orgUser2 = await organizationUserRepository.CreateAsync(GetConfirmedOrganizationUser(org2, user));

        // Act
        var results = await policyRepository.GetPolicyDetailsByUserIdAndPolicyTypeAsync(
            user.Id,
            PolicyType.SingleOrg);

        // Assert
        var resultsList = results.ToList();
        Assert.Equal(2, resultsList.Count);

        var result1 = resultsList.First(r => r.OrganizationId == org1.Id);
        Assert.Equal(orgUser1.Id, result1.OrganizationUserId);
        Assert.Equal(PolicyType.SingleOrg, result1.PolicyType);

        var result2 = resultsList.First(r => r.OrganizationId == org2.Id);
        Assert.Equal(orgUser2.Id, result2.OrganizationUserId);
        Assert.Equal(PolicyType.SingleOrg, result2.PolicyType);

        // Cleanup
        await organizationRepository.DeleteAsync(org1);
        await organizationRepository.DeleteAsync(org2);
        await userRepository.DeleteAsync(user);
    }

    [Theory]
    [DatabaseData]
    public async Task GetPolicyDetailsByUserIdAndPolicyTypeAsync_WithPolicyTypeFiltering_ReturnsOnlySpecifiedType(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        IPolicyRepository policyRepository)
    {
        // Arrange
        var user = await userRepository.CreateAsync(GetDefaultUser());
        var org = await CreateEnterpriseOrgAsync(organizationRepository);

        // Create multiple enabled policies
        await policyRepository.CreateAsync(new Policy
        {
            OrganizationId = org.Id,
            Type = PolicyType.TwoFactorAuthentication,
            Data = string.Empty,
            Enabled = true
        });
        await policyRepository.CreateAsync(new Policy
        {
            OrganizationId = org.Id,
            Type = PolicyType.MasterPassword,
            Data = string.Empty,
            Enabled = true
        });
        await policyRepository.CreateAsync(new Policy
        {
            OrganizationId = org.Id,
            Type = PolicyType.SingleOrg,
            Data = string.Empty,
            Enabled = true
        });

        await organizationUserRepository.CreateAsync(GetConfirmedOrganizationUser(org, user));

        // Act - Request only TwoFactorAuthentication policy
        var results = await policyRepository.GetPolicyDetailsByUserIdAndPolicyTypeAsync(
            user.Id,
            PolicyType.TwoFactorAuthentication);

        // Assert
        var resultsList = results.ToList();
        Assert.Single(resultsList);
        Assert.All(resultsList, r => Assert.Equal(PolicyType.TwoFactorAuthentication, r.PolicyType));

        // Cleanup
        await organizationRepository.DeleteAsync(org);
        await userRepository.DeleteAsync(user);
    }

    [Theory]
    [DatabaseData]
    public async Task GetPolicyDetailsByUserIdAndPolicyTypeAsync_WithDisabledPolicy_ReturnsEmpty(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        IPolicyRepository policyRepository)
    {
        // Arrange
        var user = await userRepository.CreateAsync(GetDefaultUser());
        var org = await CreateEnterpriseOrgAsync(organizationRepository);

        await policyRepository.CreateAsync(new Policy
        {
            OrganizationId = org.Id,
            Type = PolicyType.DisableSend,
            Data = string.Empty,
            Enabled = false // Disabled policy
        });

        await organizationUserRepository.CreateAsync(GetConfirmedOrganizationUser(org, user));

        // Act
        var results = await policyRepository.GetPolicyDetailsByUserIdAndPolicyTypeAsync(
            user.Id,
            PolicyType.DisableSend);

        // Assert
        Assert.Empty(results);

        // Cleanup
        await organizationRepository.DeleteAsync(org);
        await userRepository.DeleteAsync(user);
    }

    [Theory]
    [DatabaseData]
    public async Task GetPolicyDetailsByUserIdAndPolicyTypeAsync_WithDisabledOrganization_ReturnsEmpty(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        IPolicyRepository policyRepository)
    {
        // Arrange
        var user = await userRepository.CreateAsync(GetDefaultUser());
        var org = await organizationRepository.CreateAsync(new Organization
        {
            Name = "Test Organization",
            BillingEmail = $"billing+{Guid.NewGuid()}@example.com",
            Plan = "EnterpriseAnnually",
            PlanType = PlanType.EnterpriseAnnually,
            Seats = 10,
            MaxCollections = 10,
            UsePolicies = true,
            UseDirectory = true,
            UseTotp = true,
            Use2fa = true,
            UseApi = true,
            SelfHost = true,
            Enabled = false // Disabled organization
        });

        await policyRepository.CreateAsync(new Policy
        {
            OrganizationId = org.Id,
            Type = PolicyType.PasswordGenerator,
            Data = string.Empty,
            Enabled = true
        });

        await organizationUserRepository.CreateAsync(GetConfirmedOrganizationUser(org, user));

        // Act
        var results = await policyRepository.GetPolicyDetailsByUserIdAndPolicyTypeAsync(
            user.Id,
            PolicyType.PasswordGenerator);

        // Assert
        Assert.Empty(results);

        // Cleanup
        await organizationRepository.DeleteAsync(org);
        await userRepository.DeleteAsync(user);
    }

    [Theory]
    [DatabaseData]
    public async Task GetPolicyDetailsByUserIdAndPolicyTypeAsync_WithOrganizationNotUsingPolicies_ReturnsEmpty(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        IPolicyRepository policyRepository)
    {
        // Arrange
        var user = await userRepository.CreateAsync(GetDefaultUser());
        var org = await organizationRepository.CreateAsync(new Organization
        {
            Name = "Test Organization",
            BillingEmail = $"billing+{Guid.NewGuid()}@example.com",
            Plan = "EnterpriseAnnually",
            PlanType = PlanType.EnterpriseAnnually,
            Seats = 10,
            MaxCollections = 10,
            UsePolicies = false, // Not using policies
            UseDirectory = true,
            UseTotp = true,
            Use2fa = true,
            UseApi = true,
            SelfHost = true,
            Enabled = true
        });

        await policyRepository.CreateAsync(new Policy
        {
            OrganizationId = org.Id,
            Type = PolicyType.MaximumVaultTimeout,
            Data = string.Empty,
            Enabled = true
        });

        await organizationUserRepository.CreateAsync(GetConfirmedOrganizationUser(org, user));

        // Act
        var results = await policyRepository.GetPolicyDetailsByUserIdAndPolicyTypeAsync(
            user.Id,
            PolicyType.MaximumVaultTimeout);

        // Assert
        Assert.Empty(results);

        // Cleanup
        await organizationRepository.DeleteAsync(org);
        await userRepository.DeleteAsync(user);
    }

    [Theory]
    [DatabaseData]
    public async Task GetPolicyDetailsByUserIdAndPolicyTypeAsync_WithProviderUser_SetsIsProviderFlag(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        IProviderRepository providerRepository,
        IProviderUserRepository providerUserRepository,
        IProviderOrganizationRepository providerOrganizationRepository,
        IPolicyRepository policyRepository)
    {
        // Arrange
        var user = await userRepository.CreateAsync(GetDefaultUser());
        var org = await CreateEnterpriseOrgAsync(organizationRepository);

        await policyRepository.CreateAsync(new Policy
        {
            OrganizationId = org.Id,
            Type = PolicyType.SingleOrg,
            Data = string.Empty,
            Enabled = true
        });

        await organizationUserRepository.CreateAsync(GetConfirmedOrganizationUser(org, user));

        var provider = await providerRepository.CreateAsync(new Provider
        {
            Name = "Test Provider",
            BusinessName = "Test Provider Business",
            BusinessAddress1 = "123 Test St",
            BusinessAddress2 = "Suite 456",
            BusinessAddress3 = "Floor 7",
            BusinessCountry = "US",
            BusinessTaxNumber = "123456789",
            BillingEmail = $"billing+{Guid.NewGuid()}@example.com"
        });

        await providerUserRepository.CreateAsync(new ProviderUser
        {
            ProviderId = provider.Id,
            UserId = user.Id,
            Status = ProviderUserStatusType.Confirmed,
            Type = ProviderUserType.ProviderAdmin
        });

        await providerOrganizationRepository.CreateAsync(new ProviderOrganization
        {
            ProviderId = provider.Id,
            OrganizationId = org.Id
        });

        // Act
        var results = await policyRepository.GetPolicyDetailsByUserIdAndPolicyTypeAsync(
            user.Id,
            PolicyType.SingleOrg);

        // Assert
        var resultsList = results.ToList();
        Assert.Single(resultsList);
        var result = resultsList.First();
        Assert.True(result.IsProvider);
        Assert.Equal(org.Id, result.OrganizationId);

        // Cleanup
        await organizationRepository.DeleteAsync(org);
        await userRepository.DeleteAsync(user);
    }

    [Theory]
    [DatabaseData]
    public async Task GetPolicyDetailsByUserIdAndPolicyTypeAsync_WithNonExistentUser_ReturnsEmpty(
        IPolicyRepository policyRepository)
    {
        // Arrange
        var nonExistentUserId = Guid.NewGuid();

        // Act
        var results = await policyRepository.GetPolicyDetailsByUserIdAndPolicyTypeAsync(
            nonExistentUserId,
            PolicyType.TwoFactorAuthentication);

        // Assert
        Assert.Empty(results);
    }

    [Theory]
    [DatabaseData]
    public async Task GetPolicyDetailsByUserIdAndPolicyTypeAsync_WithCustomPermissions_PopulatesPermissionsData(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        IPolicyRepository policyRepository)
    {
        // Arrange
        var user = await userRepository.CreateAsync(GetDefaultUser());
        var org = await CreateEnterpriseOrgAsync(organizationRepository);

        await policyRepository.CreateAsync(new Policy
        {
            OrganizationId = org.Id,
            Type = PolicyType.SingleOrg,
            Data = string.Empty,
            Enabled = true
        });

        var customPermissions = "{\"accessReports\":true,\"manageGroups\":false}";
        var orgUser = await organizationUserRepository.CreateAsync(new OrganizationUser
        {
            OrganizationId = org.Id,
            UserId = user.Id,
            Status = OrganizationUserStatusType.Confirmed,
            Type = OrganizationUserType.Custom,
            Permissions = customPermissions
        });

        // Act
        var results = await policyRepository.GetPolicyDetailsByUserIdAndPolicyTypeAsync(
            user.Id,
            PolicyType.SingleOrg);

        // Assert
        var resultsList = results.ToList();
        Assert.Single(resultsList);
        var result = resultsList.First();
        Assert.Equal(OrganizationUserType.Custom, result.OrganizationUserType);
        Assert.Equal(customPermissions, result.OrganizationUserPermissionsData);

        // Cleanup
        await organizationRepository.DeleteAsync(org);
        await userRepository.DeleteAsync(user);
    }

    private static async Task<Organization> CreateEnterpriseOrgAsync(IOrganizationRepository orgRepo)
    {
        return await orgRepo.CreateAsync(new Organization
        {
            Name = "Test Organization",
            BillingEmail = $"billing+{Guid.NewGuid()}@example.com",
            Plan = "EnterpriseAnnually",
            PlanType = PlanType.EnterpriseAnnually,
            Seats = 10,
            MaxCollections = 10,
            UsePolicies = true,
            UseDirectory = true,
            UseTotp = true,
            Use2fa = true,
            UseApi = true,
            SelfHost = true,
            Enabled = true
        });
    }

    private static User GetDefaultUser() => new()
    {
        Name = $"Test User {Guid.NewGuid()}",
        Email = $"test+{Guid.NewGuid()}@example.com",
        ApiKey = $"test.api.key.{Guid.NewGuid()}"[..30],
        SecurityStamp = Guid.NewGuid().ToString()
    };

    private static OrganizationUser GetConfirmedOrganizationUser(Organization organization, User user) => new()
    {
        OrganizationId = organization.Id,
        UserId = user.Id,
        Status = OrganizationUserStatusType.Confirmed,
        Type = OrganizationUserType.User
    };

    private static OrganizationUser GetAcceptedOrganizationUser(Organization organization, User user) => new()
    {
        OrganizationId = organization.Id,
        UserId = user.Id,
        Status = OrganizationUserStatusType.Accepted,
        Type = OrganizationUserType.User
    };

    private static OrganizationUser GetInvitedOrganizationUser(Organization organization, User user) => new()
    {
        OrganizationId = organization.Id,
        UserId = null, // Invited users don't have UserId
        Email = user.Email,
        Status = OrganizationUserStatusType.Invited,
        Type = OrganizationUserType.User
    };
}
