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
    [Theory]
    [DatabaseData]
    public async Task GetPolicyDetailsByUserIdsAndPolicyType_GivenTwoUsersForAnEnterpriseOrgWithTwoFactorEnabled_WhenUsersHaveBeenConfirmedOrAccepted_ThenShouldReturnCorrectPolicyDetailsAsync(
        IUserRepository userRepository,
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationRepository organizationRepository,
        IPolicyRepository policyRepository)
    {
        // Arrange
        var user1 = await userRepository.CreateAsync(GetDefaultUser());

        var user2 = await userRepository.CreateAsync(GetDefaultUser());

        var organization = await CreateEnterpriseOrgAsync(organizationRepository);

        var policy = await policyRepository.CreateAsync(new Policy
        {
            OrganizationId = organization.Id,
            Type = PolicyType.TwoFactorAuthentication,
            Data = string.Empty,
            Enabled = true
        });

        var orgUser1 = await organizationUserRepository.CreateAsync(GetAcceptedOrganizationUser(organization, user1));

        var orgUser2 = await organizationUserRepository.CreateAsync(GetConfirmedOrganizationUser(organization, user2));

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
        Assert.Equal(OrganizationUserStatusType.Accepted, result1.OrganizationUserStatus);

        var result2 = resultsList.First(r => r.UserId == user2.Id);
        Assert.Equal(orgUser2.Id, result2.OrganizationUserId);
        Assert.Equal(organization.Id, result2.OrganizationId);
        Assert.Equal(PolicyType.TwoFactorAuthentication, result2.PolicyType);
        Assert.Equal(policy.Data, result2.PolicyData);
        Assert.Equal(OrganizationUserStatusType.Confirmed, result2.OrganizationUserStatus);

        await organizationRepository.DeleteAsync(organization);
        await userRepository.DeleteManyAsync([user1, user2]);
    }

    [Theory]
    [DatabaseData]
    public async Task GetPolicyDetailsByUserIdsAndPolicyType_GivenTwoUsersForEnterpriseOrgWithMasterPasswordEnabled_WhenUsersHaveBeenInvited_ThenShouldReturnCorrectPolicyDetailsForInvitedUsersAsync(
        IUserRepository userRepository,
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationRepository organizationRepository,
        IPolicyRepository policyRepository)
    {
        // Arrange
        var user1 = await userRepository.CreateAsync(GetDefaultUser());

        var user2 = await userRepository.CreateAsync(GetDefaultUser());

        var organization = await CreateEnterpriseOrgAsync(organizationRepository);

        _ = await policyRepository.CreateAsync(new Policy
        {
            OrganizationId = organization.Id,
            Type = PolicyType.MasterPassword,
            Data = "{\"minComplexity\":4}",
            Enabled = true,
        });

        var orgUser1 = await organizationUserRepository.CreateAsync(GetInvitedOrganizationUser(organization, user1));

        var orgUser2 = await organizationUserRepository.CreateAsync(GetInvitedOrganizationUser(organization, user2));

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

        await organizationRepository.DeleteAsync(organization);
        await userRepository.DeleteManyAsync([user1, user2]);
    }

    [Theory]
    [DatabaseData]
    public async Task GetPolicyDetailsByUserIdsAndPolicyType_GivenConfirmedUserEnterpriseOrgWithPolicyEnabled_WhenUserIsAProvider_ThenShouldContainProviderDataAsync(
        IUserRepository userRepository,
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationRepository organizationRepository,
        IProviderRepository providerRepository,
        IProviderUserRepository providerUserRepository,
        IProviderOrganizationRepository providerOrganizationRepository,
        IPolicyRepository policyRepository)
    {
        // Arrange
        var user = await userRepository.CreateAsync(GetDefaultUser());

        var organization = await CreateEnterpriseOrgAsync(organizationRepository);

        _ = await policyRepository.CreateAsync(GetPolicy(PolicyType.SingleOrg, organization));

        _ = await organizationUserRepository.CreateAsync(GetConfirmedOrganizationUser(organization, user));

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
            OrganizationId = organization.Id
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

        await organizationRepository.DeleteAsync(organization);
        await userRepository.DeleteManyAsync([user]);
    }

    [Theory]
    [DatabaseData]
    public async Task GetPolicyDetailsByUserIdsAndPolicyType_GivenEnterpriseOrgWithTwoEnabledPolicies_WhenRequestingTwoFactor_ShouldOnlyReturnInputPolicyType(
        IUserRepository userRepository,
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationRepository organizationRepository,
        IPolicyRepository policyRepository)
    {
        // Arrange
        var user = await userRepository.CreateAsync(GetDefaultUser());

        var organization = await CreateEnterpriseOrgAsync(organizationRepository);

        // Create multiple policies
        _ = await policyRepository.CreateAsync(GetPolicy(PolicyType.TwoFactorAuthentication, organization));

        _ = await policyRepository.CreateAsync(GetPolicy(PolicyType.MasterPassword, organization));

        _ = await organizationUserRepository.CreateAsync(GetConfirmedOrganizationUser(organization, user));

        // Act - Request only TwoFactorAuthentication policy
        var results = await policyRepository.GetPolicyDetailsByUserIdsAndPolicyType(
            [user.Id],
            PolicyType.TwoFactorAuthentication);

        // Assert
        var resultsList = results.ToList();
        Assert.Single(resultsList);
        Assert.All(resultsList, r => Assert.Equal(PolicyType.TwoFactorAuthentication, r.PolicyType));

        await organizationRepository.DeleteAsync(organization);
        await userRepository.DeleteManyAsync([user]);
    }

    [Theory]
    [DatabaseData]
    public async Task GetPolicyDetailsByUserIdsAndPolicyType_GivenEnterpriseOrg_WhenSendPolicyIsDisabled_ShouldNotReturnDisabledPoliciesAsync(
        IUserRepository userRepository,
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationRepository organizationRepository,
        IPolicyRepository policyRepository)
    {
        // Arrange
        var user = await userRepository.CreateAsync(GetDefaultUser());

        var organization = await CreateEnterpriseOrgAsync(organizationRepository);
        _ = await policyRepository.CreateAsync(new Policy
        {
            OrganizationId = organization.Id,
            Type = PolicyType.DisableSend,
            Data = "{}",
            Enabled = false // Disabled policy
        });

        _ = await organizationUserRepository.CreateAsync(GetConfirmedOrganizationUser(organization, user));

        // Act
        var results = await policyRepository.GetPolicyDetailsByUserIdsAndPolicyType(
            [user.Id],
            PolicyType.DisableSend);

        // Assert
        Assert.Empty(results);
        await organizationRepository.DeleteAsync(organization);
        await userRepository.DeleteManyAsync([user]);
    }

    [Theory]
    [DatabaseData]
    public async Task GetPolicyDetailsByUserIdsAndPolicyType_GivenEnterpriseOrgWithPolicies_WhenOrgIsDisabled_ThenShouldNotReturnResults(
        IUserRepository userRepository,
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationRepository organizationRepository,
        IPolicyRepository policyRepository)
    {
        // Arrange
        var user = await userRepository.CreateAsync(GetDefaultUser());

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

        _ = await policyRepository.CreateAsync(GetPolicy(PolicyType.RequireSso, organization));

        _ = await organizationUserRepository.CreateAsync(GetConfirmedOrganizationUser(organization, user));

        // Act
        var results = await policyRepository.GetPolicyDetailsByUserIdsAndPolicyType(
            [user.Id],
            PolicyType.RequireSso);

        // Assert
        Assert.Empty(results);

        await organizationRepository.DeleteAsync(organization);
        await userRepository.DeleteManyAsync([user]);
    }

    [Theory]
    [DatabaseData]
    public async Task GetPolicyDetailsByUserIdsAndPolicyType_GivenOrganization_WhenNotUsingPolicies_ThenShouldNotReturnResults(
        IUserRepository userRepository,
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationRepository organizationRepository,
        IPolicyRepository policyRepository)
    {
        // Arrange
        var user = await userRepository.CreateAsync(GetDefaultUser());

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

        var policy = await policyRepository.CreateAsync(GetPolicy(PolicyType.PasswordGenerator, organization));

        _ = await organizationUserRepository.CreateAsync(GetConfirmedOrganizationUser(organization, user));

        // Act
        var results = await policyRepository.GetPolicyDetailsByUserIdsAndPolicyType(
            [user.Id],
            PolicyType.PasswordGenerator);

        // Assert
        Assert.Empty(results);

        await organizationRepository.DeleteAsync(organization);
        await userRepository.DeleteManyAsync([user]);
    }

    [Theory]
    [DatabaseData]
    public async Task GetPolicyDetailsByUserIdsAndPolicyType_GivenOrganization_WhenRequestingWithNoUsers_ShouldReturnEmptyList(
        IOrganizationRepository organizationRepository,
        IPolicyRepository policyRepository)
    {
        // Arrange
        var organization = await CreateEnterpriseOrgAsync(organizationRepository);

        _ = await policyRepository.CreateAsync(GetPolicy(PolicyType.TwoFactorAuthentication, organization));

        // Act
        var results = await policyRepository.GetPolicyDetailsByUserIdsAndPolicyType(
            new List<Guid>(),
            PolicyType.TwoFactorAuthentication);

        // Assert
        Assert.Empty(results);
    }

    [Theory]
    [DatabaseData]
    public async Task GetPolicyDetailsByUserIdsAndPolicyType_GivenTwoOrganizations_WhenUserIsAMemberOfBoth_ShouldReturnResultsForBothOrganizations(
        IUserRepository userRepository,
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationRepository organizationRepository,
        IPolicyRepository policyRepository)
    {
        // Arrange
        var user = await userRepository.CreateAsync(GetDefaultUser());

        var organization1 = await CreateEnterpriseOrgAsync(organizationRepository);
        var organization2 = await CreateEnterpriseOrgAsync(organizationRepository);

        _ = await policyRepository.CreateAsync(GetPolicy(PolicyType.TwoFactorAuthentication, organization1));

        _ = await policyRepository.CreateAsync(GetPolicy(PolicyType.TwoFactorAuthentication, organization2));

        _ = await organizationUserRepository.CreateAsync(GetConfirmedOrganizationUser(organization1, user));

        _ = await organizationUserRepository.CreateAsync(GetConfirmedOrganizationUser(organization2, user));

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

    private static User GetDefaultUser() => new()
    {
        Name = $"Test User {Guid.NewGuid()}",
        Email = $"test+{Guid.NewGuid()}@example.com",
        ApiKey = $"test.api.key.{Guid.NewGuid()}"[..30],
        SecurityStamp = Guid.NewGuid().ToString()
    };

    private static OrganizationUser GetAcceptedOrganizationUser(Organization organization, User user) => new()
    {
        OrganizationId = organization.Id,
        UserId = user.Id,
        Status = OrganizationUserStatusType.Accepted,
        Type = OrganizationUserType.User
    };

    private static OrganizationUser GetConfirmedOrganizationUser(Organization organization, User user) => new()
    {
        OrganizationId = organization.Id,
        UserId = user.Id,
        Status = OrganizationUserStatusType.Confirmed,
        Type = OrganizationUserType.User
    };

    private static OrganizationUser GetInvitedOrganizationUser(Organization organization, User user) => new()
    {
        OrganizationId = organization.Id,
        UserId = null, // Invited users don't have UserId
        Email = user.Email,
        Status = OrganizationUserStatusType.Invited,
        Type = OrganizationUserType.User,
    };

    private static Policy GetPolicy(PolicyType policyType, Organization organization) => new()
    {
        OrganizationId = organization.Id,
        Type = policyType,
        Data = "{\"test\": \"value\"}",
        Enabled = true
    };
}
