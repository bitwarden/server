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

public class GetPolicyDetailsByUserIdsAndPolicyTypeTests
{
    [DatabaseTheory]
    [DatabaseData]
    public async Task ShouldReturnCorrectPolicyDetailsForAcceptedUsersAsync(
        IUserRepository userRepository,
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationRepository organizationRepository,
        IPolicyRepository policyRepository)
    {
        // Arrange
        var user1 = await userRepository.CreateAsync(new User
        {
            Name = "Test User 1",
            Email = $"test1+{Guid.NewGuid()}@example.com",
            ApiKey = "TEST_API_KEY",
            SecurityStamp = Guid.NewGuid().ToString(),
        });

        var user2 = await userRepository.CreateAsync(new User
        {
            Name = "Test User 2",
            Email = $"test2+{Guid.NewGuid()}@example.com",
            ApiKey = "TEST_API_KEY",
            SecurityStamp = Guid.NewGuid().ToString(),
        });

        var organization = await CreateEnterpriseOrgAsync(organizationRepository);
        var policy = await policyRepository.CreateAsync(new Policy
        {
            OrganizationId = organization.Id,
            Type = PolicyType.TwoFactorAuthentication,
            Data = "{\"require\":true}",
            Enabled = true,
        });

        var orgUser1 = await organizationUserRepository.CreateAsync(new OrganizationUser
        {
            OrganizationId = organization.Id,
            UserId = user1.Id,
            Status = OrganizationUserStatusType.Confirmed,
            Type = OrganizationUserType.User,
        });

        var orgUser2 = await organizationUserRepository.CreateAsync(new OrganizationUser
        {
            OrganizationId = organization.Id,
            UserId = user2.Id,
            Status = OrganizationUserStatusType.Accepted,
            Type = OrganizationUserType.User,
        });

        // Act
        var results = await policyRepository.GetPolicyDetailsByUserIdsAndPolicyType(
            [user1.Id, user2.Id],
            PolicyType.TwoFactorAuthentication);

        // Assert
        var resultsList = results.ToList();
        Assert.Equal(2, resultsList.Count);

        var result1 = resultsList.First(r => r.UserId == user1.Id);
        Assert.Equal(orgUser1.Id, result1.OrganizationUserId);
        Assert.Equal(organization.Id, result1.OrganizationId);
        Assert.Equal(PolicyType.TwoFactorAuthentication, result1.PolicyType);
        Assert.Equal(policy.Data, result1.PolicyData);
        Assert.Equal(OrganizationUserStatusType.Confirmed, result1.OrganizationUserStatus);

        var result2 = resultsList.First(r => r.UserId == user2.Id);
        Assert.Equal(orgUser2.Id, result2.OrganizationUserId);
        Assert.Equal(organization.Id, result2.OrganizationId);
        Assert.Equal(PolicyType.TwoFactorAuthentication, result2.PolicyType);
        Assert.Equal(policy.Data, result2.PolicyData);
        Assert.Equal(OrganizationUserStatusType.Accepted, result2.OrganizationUserStatus);
    }

    [DatabaseTheory]
    [DatabaseData]
    public async Task ShouldReturnCorrectPolicyDetailsForInvitedUsersAsync(
        IUserRepository userRepository,
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationRepository organizationRepository,
        IPolicyRepository policyRepository)
    {
        // Arrange
        var user1 = await userRepository.CreateAsync(new User
        {
            Name = "Test User 1",
            Email = $"test1+{Guid.NewGuid()}@example.com",
            ApiKey = "TEST_API_KEY",
            SecurityStamp = Guid.NewGuid().ToString(),
        });

        var user2 = await userRepository.CreateAsync(new User
        {
            Name = "Test User 2",
            Email = $"test2+{Guid.NewGuid()}@example.com",
            ApiKey = "TEST_API_KEY",
            SecurityStamp = Guid.NewGuid().ToString(),
        });

        var organization = await CreateEnterpriseOrgAsync(organizationRepository);
        var policy = await policyRepository.CreateAsync(new Policy
        {
            OrganizationId = organization.Id,
            Type = PolicyType.MasterPassword,
            Data = "{\"minComplexity\":4}",
            Enabled = true,
        });

        // Create invited org users (matching by email, not UserId)
        var orgUser1 = await organizationUserRepository.CreateAsync(new OrganizationUser
        {
            OrganizationId = organization.Id,
            UserId = null, // Invited users don't have UserId
            Email = user1.Email,
            Status = OrganizationUserStatusType.Invited,
            Type = OrganizationUserType.User,
        });

        var orgUser2 = await organizationUserRepository.CreateAsync(new OrganizationUser
        {
            OrganizationId = organization.Id,
            UserId = null, // Invited users don't have UserId
            Email = user2.Email,
            Status = OrganizationUserStatusType.Invited,
            Type = OrganizationUserType.User,
        });

        // Act
        var results = await policyRepository.GetPolicyDetailsByUserIdsAndPolicyType(
            [user1.Id, user2.Id],
            PolicyType.MasterPassword);

        // Assert
        var resultsList = results.ToList();
        Assert.Equal(2, resultsList.Count);

        var result1 = resultsList.First(r => r.UserId == user1.Id);
        Assert.Equal(orgUser1.Id, result1.OrganizationUserId);
        Assert.Equal(organization.Id, result1.OrganizationId);
        Assert.Equal(PolicyType.MasterPassword, result1.PolicyType);
        Assert.Equal(OrganizationUserStatusType.Invited, result1.OrganizationUserStatus);

        var result2 = resultsList.First(r => r.UserId == user2.Id);
        Assert.Equal(orgUser2.Id, result2.OrganizationUserId);
        Assert.Equal(organization.Id, result2.OrganizationId);
        Assert.Equal(PolicyType.MasterPassword, result2.PolicyType);
        Assert.Equal(OrganizationUserStatusType.Invited, result2.OrganizationUserStatus);
    }

    [DatabaseTheory]
    [DatabaseData]
    public async Task ShouldContainProviderDataAsync(
        IUserRepository userRepository,
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationRepository organizationRepository,
        IProviderRepository providerRepository,
        IProviderUserRepository providerUserRepository,
        IProviderOrganizationRepository providerOrganizationRepository,
        IPolicyRepository policyRepository)
    {
        // Arrange
        var user = await userRepository.CreateAsync(new User
        {
            Name = "Test User",
            Email = $"test+{Guid.NewGuid()}@example.com",
            ApiKey = "TEST_API_KEY",
            SecurityStamp = Guid.NewGuid().ToString(),
        });

        var organization = await CreateEnterpriseOrgAsync(organizationRepository);
        var policy = await policyRepository.CreateAsync(new Policy
        {
            OrganizationId = organization.Id,
            Type = PolicyType.SingleOrg,
            Data = "{}",
            Enabled = true,
        });

        var orgUser = await organizationUserRepository.CreateAsync(new OrganizationUser
        {
            OrganizationId = organization.Id,
            UserId = user.Id,
            Status = OrganizationUserStatusType.Confirmed,
            Type = OrganizationUserType.User
        });

        var provider = await providerRepository.CreateAsync(new Provider
        {
            Name = "Test Provider",
            BusinessName = "Test Provider Business",
            BusinessAddress1 = "123 Test St",
            BusinessAddress2 = "Suite 456",
            BusinessAddress3 = "Floor 7",
            BusinessCountry = "US",
            BusinessTaxNumber = "123456789",
            BillingEmail = $"billing+{Guid.NewGuid()}@example.com",
        });

        await providerUserRepository.CreateAsync(new ProviderUser
        {
            ProviderId = provider.Id,
            UserId = user.Id,
            Status = ProviderUserStatusType.Confirmed,
            Type = ProviderUserType.ProviderAdmin,
        });

        await providerOrganizationRepository.CreateAsync(new ProviderOrganization
        {
            ProviderId = provider.Id,
            OrganizationId = organization.Id,
        });

        // Act
        var results = await policyRepository.GetPolicyDetailsByUserIdsAndPolicyType(
            [user.Id],
            PolicyType.SingleOrg);

        // Assert
        var resultsList = results.ToList();
        Assert.Single(resultsList);

        var result = resultsList.First();
        Assert.True(result.IsProvider);
        Assert.Equal(user.Id, result.UserId);
        Assert.Equal(organization.Id, result.OrganizationId);
    }

    [DatabaseTheory]
    [DatabaseData]
    public async Task ShouldOnlyReturnInputPolicyType(
        IUserRepository userRepository,
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationRepository organizationRepository,
        IPolicyRepository policyRepository)
    {
        // Arrange
        var user = await userRepository.CreateAsync(new User
        {
            Name = "Test User",
            Email = $"test+{Guid.NewGuid()}@example.com",
            ApiKey = "TEST_API_KEY",
            SecurityStamp = Guid.NewGuid().ToString(),
        });

        var organization = await CreateEnterpriseOrgAsync(organizationRepository);

        // Create multiple policies
        var twoFactorPolicy = await policyRepository.CreateAsync(new Policy
        {
            OrganizationId = organization.Id,
            Type = PolicyType.TwoFactorAuthentication,
            Data = "{\"require\":true}",
            Enabled = true,
        });

        var masterPasswordPolicy = await policyRepository.CreateAsync(new Policy
        {
            OrganizationId = organization.Id,
            Type = PolicyType.MasterPassword,
            Data = "{\"minComplexity\":4}",
            Enabled = true,
        });

        var orgUser = await organizationUserRepository.CreateAsync(new OrganizationUser
        {
            OrganizationId = organization.Id,
            UserId = user.Id,
            Status = OrganizationUserStatusType.Confirmed,
            Type = OrganizationUserType.User,
        });

        // Act - Request only TwoFactorAuthentication policy
        var results = await policyRepository.GetPolicyDetailsByUserIdsAndPolicyType(
            [user.Id],
            PolicyType.TwoFactorAuthentication);

        // Assert
        var resultsList = results.ToList();
        Assert.Single(resultsList);
        Assert.All(resultsList, r => Assert.Equal(PolicyType.TwoFactorAuthentication, r.PolicyType));
    }

    [DatabaseTheory]
    [DatabaseData]
    public async Task ShouldNotReturnDisabledPoliciesAsync(
        IUserRepository userRepository,
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationRepository organizationRepository,
        IPolicyRepository policyRepository)
    {
        // Arrange
        var user = await userRepository.CreateAsync(new User
        {
            Name = "Test User",
            Email = $"test+{Guid.NewGuid()}@example.com",
            ApiKey = "TEST_API_KEY",
            SecurityStamp = Guid.NewGuid().ToString(),
        });

        var organization = await CreateEnterpriseOrgAsync(organizationRepository);
        var policy = await policyRepository.CreateAsync(new Policy
        {
            OrganizationId = organization.Id,
            Type = PolicyType.DisableSend,
            Data = "{}",
            Enabled = false, // Disabled policy
        });

        var orgUser = await organizationUserRepository.CreateAsync(new OrganizationUser
        {
            OrganizationId = organization.Id,
            UserId = user.Id,
            Status = OrganizationUserStatusType.Confirmed,
            Type = OrganizationUserType.User,
        });

        // Act
        var results = await policyRepository.GetPolicyDetailsByUserIdsAndPolicyType(
            [user.Id],
            PolicyType.DisableSend);

        // Assert
        Assert.Empty(results);
    }

    [DatabaseTheory]
    [DatabaseData]
    public async Task ShouldNotReturnResultsForDisabledOrganizationsAsync(
        IUserRepository userRepository,
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationRepository organizationRepository,
        IPolicyRepository policyRepository)
    {
        // Arrange
        var user = await userRepository.CreateAsync(new User
        {
            Name = "Test User",
            Email = $"test+{Guid.NewGuid()}@example.com",
            ApiKey = "TEST_API_KEY",
            SecurityStamp = Guid.NewGuid().ToString(),
        });

        var organization = await organizationRepository.CreateAsync(new Organization
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
            Enabled = false, // Disabled organization
        });

        var policy = await policyRepository.CreateAsync(new Policy
        {
            OrganizationId = organization.Id,
            Type = PolicyType.RequireSso,
            Data = "{}",
            Enabled = true,
        });

        var orgUser = await organizationUserRepository.CreateAsync(new OrganizationUser
        {
            OrganizationId = organization.Id,
            UserId = user.Id,
            Status = OrganizationUserStatusType.Confirmed,
            Type = OrganizationUserType.User,
        });

        // Act
        var results = await policyRepository.GetPolicyDetailsByUserIdsAndPolicyType(
            [user.Id],
            PolicyType.RequireSso);

        // Assert
        Assert.Empty(results);
    }

    [DatabaseTheory]
    [DatabaseData]
    public async Task ShouldNotReturnResultsForOrganizationsNotUsingPoliciesAsync(
        IUserRepository userRepository,
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationRepository organizationRepository,
        IPolicyRepository policyRepository)
    {
        // Arrange
        var user = await userRepository.CreateAsync(new User
        {
            Name = "Test User",
            Email = $"test+{Guid.NewGuid()}@example.com",
            ApiKey = "TEST_API_KEY",
            SecurityStamp = Guid.NewGuid().ToString(),
        });

        var organization = await organizationRepository.CreateAsync(new Organization
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
            Enabled = true,
        });

        var policy = await policyRepository.CreateAsync(new Policy
        {
            OrganizationId = organization.Id,
            Type = PolicyType.PasswordGenerator,
            Data = "{}",
            Enabled = true
        });

        var orgUser = await organizationUserRepository.CreateAsync(new OrganizationUser
        {
            OrganizationId = organization.Id,
            UserId = user.Id,
            Status = OrganizationUserStatusType.Confirmed,
            Type = OrganizationUserType.User,
        });

        // Act
        var results = await policyRepository.GetPolicyDetailsByUserIdsAndPolicyType(
            [user.Id],
            PolicyType.PasswordGenerator);

        // Assert
        Assert.Empty(results);
    }

    [DatabaseTheory]
    [DatabaseData]
    public async Task ShouldReturnEmptyForEmptyUserIdsListAsync(
        IOrganizationRepository organizationRepository,
        IPolicyRepository policyRepository)
    {
        // Arrange
        var organization = await CreateEnterpriseOrgAsync(organizationRepository);
        var policy = await policyRepository.CreateAsync(new Policy
        {
            OrganizationId = organization.Id,
            Type = PolicyType.TwoFactorAuthentication,
            Data = "{}",
            Enabled = true,
        });

        // Act
        var results = await policyRepository.GetPolicyDetailsByUserIdsAndPolicyType(
            new List<Guid>(),
            PolicyType.TwoFactorAuthentication);

        // Assert
        Assert.Empty(results);
    }

    [DatabaseTheory]
    [DatabaseData]
    public async Task ShouldReturnResultsFromMultipleOrganizationsAsync(
        IUserRepository userRepository,
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationRepository organizationRepository,
        IPolicyRepository policyRepository)
    {
        // Arrange
        var user = await userRepository.CreateAsync(new User
        {
            Name = "Test User",
            Email = $"test+{Guid.NewGuid()}@example.com",
            ApiKey = "TEST_API_KEY",
            SecurityStamp = Guid.NewGuid().ToString(),
        });

        var organization1 = await CreateEnterpriseOrgAsync(organizationRepository);
        var organization2 = await CreateEnterpriseOrgAsync(organizationRepository);

        var policy1 = await policyRepository.CreateAsync(new Policy
        {
            OrganizationId = organization1.Id,
            Type = PolicyType.TwoFactorAuthentication,
            Data = "{}",
            Enabled = true,
        });

        var policy2 = await policyRepository.CreateAsync(new Policy
        {
            OrganizationId = organization2.Id,
            Type = PolicyType.TwoFactorAuthentication,
            Data = "{}",
            Enabled = true,
        });

        var orgUser1 = await organizationUserRepository.CreateAsync(new OrganizationUser
        {
            OrganizationId = organization1.Id,
            UserId = user.Id,
            Status = OrganizationUserStatusType.Confirmed,
            Type = OrganizationUserType.User,
        });

        var orgUser2 = await organizationUserRepository.CreateAsync(new OrganizationUser
        {
            OrganizationId = organization2.Id,
            UserId = user.Id,
            Status = OrganizationUserStatusType.Confirmed,
            Type = OrganizationUserType.User,
        });

        // Act
        var results = await policyRepository.GetPolicyDetailsByUserIdsAndPolicyType(
            [user.Id],
            PolicyType.TwoFactorAuthentication);

        // Assert
        var resultsList = results.ToList();
        Assert.Equal(2, resultsList.Count);

        var organizationIds = resultsList.Select(r => r.OrganizationId).ToList();
        Assert.Contains(organization1.Id, organizationIds);
        Assert.Contains(organization2.Id, organizationIds);
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
            Enabled = true,
        });
    }
}
