using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing.Enums;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.AdminConsole.Repositories.PolicyRepository;

public class GetPolicyDetailsWithStateByUserIdAndPolicyTypeTests
{
    [Theory]
    [DatabaseData]
    public async Task GetPolicyDetailsWithStateByUserIdAndPolicyTypeAsync_WithNoPolicyRow_ReturnsRowWithNullEnabled(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        IPolicyRepository policyRepository)
    {
        // Arrange — an org the user belongs to, but no policy row of this type
        var user = await userRepository.CreateAsync(GetDefaultUser());
        var org = await CreateEnterpriseOrgAsync(organizationRepository);
        var orgUser = await organizationUserRepository.CreateAsync(GetConfirmedOrganizationUser(org, user));

        // Act
        var results = await policyRepository.GetPolicyDetailsWithStateByUserIdAndPolicyTypeAsync(
            user.Id,
            PolicyType.TwoFactorAuthentication);

        // Assert — a row is returned even though no policy exists; Enabled is null and there is no data
        var result = Assert.Single(results);
        Assert.Equal(orgUser.Id, result.OrganizationUserId);
        Assert.Equal(org.Id, result.OrganizationId);
        Assert.Equal(PolicyType.TwoFactorAuthentication, result.PolicyType);
        Assert.Null(result.Enabled);
        Assert.Null(result.PolicyData);
    }

    [Theory]
    [DatabaseData]
    public async Task GetPolicyDetailsWithStateByUserIdAndPolicyTypeAsync_WithEnabledPolicy_ReturnsEnabledTrue(
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
        await organizationUserRepository.CreateAsync(GetConfirmedOrganizationUser(org, user));

        // Act
        var results = await policyRepository.GetPolicyDetailsWithStateByUserIdAndPolicyTypeAsync(
            user.Id,
            PolicyType.MasterPassword);

        // Assert
        var result = Assert.Single(results);
        Assert.True(result.Enabled);
        Assert.Equal(policy.Data, result.PolicyData);
    }

    [Theory]
    [DatabaseData]
    public async Task GetPolicyDetailsWithStateByUserIdAndPolicyTypeAsync_WithDisabledPolicy_ReturnsEnabledFalse(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        IPolicyRepository policyRepository)
    {
        // Arrange — a disabled row must be returned (not filtered out), so callers can distinguish it from "no row"
        var user = await userRepository.CreateAsync(GetDefaultUser());
        var org = await CreateEnterpriseOrgAsync(organizationRepository);
        await policyRepository.CreateAsync(new Policy
        {
            OrganizationId = org.Id,
            Type = PolicyType.SingleOrg,
            Enabled = false
        });
        await organizationUserRepository.CreateAsync(GetConfirmedOrganizationUser(org, user));

        // Act
        var results = await policyRepository.GetPolicyDetailsWithStateByUserIdAndPolicyTypeAsync(
            user.Id,
            PolicyType.SingleOrg);

        // Assert
        var result = Assert.Single(results);
        Assert.False(result.Enabled);
    }

    [Theory]
    [DatabaseData]
    public async Task GetPolicyDetailsWithStateByUserIdAndPolicyTypeAsync_WithInvitedUserAndNoRow_ReturnsRowWithNullEnabled(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        IPolicyRepository policyRepository)
    {
        // Arrange — invited user matched by email, no policy row
        var user = await userRepository.CreateAsync(GetDefaultUser());
        var org = await CreateEnterpriseOrgAsync(organizationRepository);
        var orgUser = await organizationUserRepository.CreateAsync(GetInvitedOrganizationUser(org, user));

        // Act
        var results = await policyRepository.GetPolicyDetailsWithStateByUserIdAndPolicyTypeAsync(
            user.Id,
            PolicyType.RequireSso);

        // Assert
        var result = Assert.Single(results);
        Assert.Equal(orgUser.Id, result.OrganizationUserId);
        Assert.Equal(OrganizationUserStatusType.Invited, result.OrganizationUserStatus);
        Assert.Null(result.Enabled);
    }

    [Theory]
    [DatabaseData]
    public async Task GetPolicyDetailsWithStateByUserIdAndPolicyTypeAsync_WithMultipleOrganizations_ReturnsRowPerOrg(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        IPolicyRepository policyRepository)
    {
        // Arrange — one org has an enabled row, the other has none
        var user = await userRepository.CreateAsync(GetDefaultUser());
        var orgWithPolicy = await CreateEnterpriseOrgAsync(organizationRepository);
        var orgWithoutPolicy = await CreateEnterpriseOrgAsync(organizationRepository);

        await policyRepository.CreateAsync(new Policy
        {
            OrganizationId = orgWithPolicy.Id,
            Type = PolicyType.SingleOrg,
            Enabled = true
        });

        await organizationUserRepository.CreateAsync(GetConfirmedOrganizationUser(orgWithPolicy, user));
        await organizationUserRepository.CreateAsync(GetConfirmedOrganizationUser(orgWithoutPolicy, user));

        // Act
        var results = (await policyRepository.GetPolicyDetailsWithStateByUserIdAndPolicyTypeAsync(
            user.Id,
            PolicyType.SingleOrg)).ToList();

        // Assert
        Assert.Equal(2, results.Count);
        Assert.True(results.First(r => r.OrganizationId == orgWithPolicy.Id).Enabled);
        Assert.Null(results.First(r => r.OrganizationId == orgWithoutPolicy.Id).Enabled);
    }

    [Theory]
    [DatabaseData]
    public async Task GetPolicyDetailsWithStateByUserIdAndPolicyTypeAsync_WithDisabledOrganization_ReturnsEmpty(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        IPolicyRepository policyRepository)
    {
        // Arrange
        var user = await userRepository.CreateAsync(GetDefaultUser());
        var org = await organizationRepository.CreateAsync(GetOrganization(usePolicies: true, enabled: false));
        await organizationUserRepository.CreateAsync(GetConfirmedOrganizationUser(org, user));

        // Act
        var results = await policyRepository.GetPolicyDetailsWithStateByUserIdAndPolicyTypeAsync(
            user.Id,
            PolicyType.PasswordGenerator);

        // Assert — a disabled organization contributes no rows, even for a default-on policy
        Assert.Empty(results);
    }

    [Theory]
    [DatabaseData]
    public async Task GetPolicyDetailsWithStateByUserIdAndPolicyTypeAsync_WithOrganizationNotUsingPolicies_ReturnsEmpty(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        IPolicyRepository policyRepository)
    {
        // Arrange
        var user = await userRepository.CreateAsync(GetDefaultUser());
        var org = await organizationRepository.CreateAsync(GetOrganization(usePolicies: false, enabled: true));
        await organizationUserRepository.CreateAsync(GetConfirmedOrganizationUser(org, user));

        // Act
        var results = await policyRepository.GetPolicyDetailsWithStateByUserIdAndPolicyTypeAsync(
            user.Id,
            PolicyType.MaximumVaultTimeout);

        // Assert
        Assert.Empty(results);
    }

    private static async Task<Organization> CreateEnterpriseOrgAsync(IOrganizationRepository orgRepo)
        => await orgRepo.CreateAsync(GetOrganization(usePolicies: true, enabled: true));

    private static Organization GetOrganization(bool usePolicies, bool enabled) => new()
    {
        Name = "Test Organization",
        BillingEmail = $"billing+{Guid.NewGuid()}@example.com",
        Plan = "EnterpriseAnnually",
        PlanType = PlanType.EnterpriseAnnually,
        Seats = 10,
        MaxCollections = 10,
        UsePolicies = usePolicies,
        UseDirectory = true,
        UseTotp = true,
        Use2fa = true,
        UseApi = true,
        SelfHost = true,
        Enabled = enabled
    };

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

    private static OrganizationUser GetInvitedOrganizationUser(Organization organization, User user) => new()
    {
        OrganizationId = organization.Id,
        UserId = null, // Invited users don't have UserId
        Email = user.Email,
        Status = OrganizationUserStatusType.Invited,
        Type = OrganizationUserType.User
    };
}
