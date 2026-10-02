using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.AdminConsole.Repositories.PolicyRepository;

public class GetManyConfirmedAcceptedByUserIdAsyncTests
{
    [Theory, DatabaseData]
    public async Task ReturnsPolicies_WhenUserIsConfirmed(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        IPolicyRepository policyRepository)
    {
        // Arrange
        var user = await userRepository.CreateTestUserAsync();
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        await organizationUserRepository.CreateConfirmedTestOrganizationUserAsync(organization, user);
        var policy = await policyRepository.CreateAsync(new Policy
        {
            OrganizationId = organization.Id,
            Type = PolicyType.TwoFactorAuthentication,
            Enabled = true
        });

        // Act
        var results = await policyRepository.GetManyConfirmedAcceptedByUserIdAsync(user.Id);

        // Assert
        Assert.Contains(results, p => p.Id == policy.Id);
    }

    [Theory, DatabaseData]
    public async Task ReturnsPolicies_WhenUserIsAccepted(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        IPolicyRepository policyRepository)
    {
        // Arrange
        var user = await userRepository.CreateTestUserAsync();
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        await organizationUserRepository.CreateAcceptedTestOrganizationUserAsync(organization, user);
        var policy = await policyRepository.CreateAsync(new Policy
        {
            OrganizationId = organization.Id,
            Type = PolicyType.TwoFactorAuthentication,
            Enabled = true
        });

        // Act
        var results = await policyRepository.GetManyConfirmedAcceptedByUserIdAsync(user.Id);

        // Assert
        Assert.Contains(results, p => p.Id == policy.Id);
    }

    [Theory, DatabaseData]
    public async Task ReturnsPoliciesAcrossMultipleOrganizations_WhenUserIsConfirmedOrAccepted(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        IPolicyRepository policyRepository)
    {
        // Arrange
        var user = await userRepository.CreateTestUserAsync();

        var confirmedOrg = await organizationRepository.CreateTestOrganizationAsync();
        await organizationUserRepository.CreateConfirmedTestOrganizationUserAsync(confirmedOrg, user);
        var confirmedPolicy = await policyRepository.CreateAsync(new Policy
        {
            OrganizationId = confirmedOrg.Id,
            Type = PolicyType.TwoFactorAuthentication,
            Enabled = true
        });

        var acceptedOrg = await organizationRepository.CreateTestOrganizationAsync();
        await organizationUserRepository.CreateAcceptedTestOrganizationUserAsync(acceptedOrg, user);
        var acceptedPolicy = await policyRepository.CreateAsync(new Policy
        {
            OrganizationId = acceptedOrg.Id,
            Type = PolicyType.TwoFactorAuthentication,
            Enabled = true
        });

        // Act
        var results = await policyRepository.GetManyConfirmedAcceptedByUserIdAsync(user.Id);

        // Assert
        Assert.Contains(results, p => p.Id == confirmedPolicy.Id);
        Assert.Contains(results, p => p.Id == acceptedPolicy.Id);
    }

    [Theory, DatabaseData]
    public async Task DoesNotReturnPolicies_WhenUserIsInvited(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        IPolicyRepository policyRepository)
    {
        // Arrange
        var user = await userRepository.CreateTestUserAsync();
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        await organizationUserRepository.CreateAsync(new OrganizationUser
        {
            OrganizationId = organization.Id,
            UserId = null,
            Email = user.Email,
            Status = OrganizationUserStatusType.Invited,
            Type = OrganizationUserType.User
        });
        var policy = await policyRepository.CreateAsync(new Policy
        {
            OrganizationId = organization.Id,
            Type = PolicyType.TwoFactorAuthentication,
            Enabled = true
        });

        // Act
        var results = await policyRepository.GetManyConfirmedAcceptedByUserIdAsync(user.Id);

        // Assert
        Assert.DoesNotContain(results, p => p.Id == policy.Id);
    }

    [Theory, DatabaseData]
    public async Task DoesNotReturnPolicies_WhenUserIsRevoked(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        IPolicyRepository policyRepository)
    {
        // Arrange
        var user = await userRepository.CreateTestUserAsync();
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        await organizationUserRepository.CreateRevokedTestOrganizationUserAsync(organization, user);
        var policy = await policyRepository.CreateAsync(new Policy
        {
            OrganizationId = organization.Id,
            Type = PolicyType.TwoFactorAuthentication,
            Enabled = true
        });

        // Act
        var results = await policyRepository.GetManyConfirmedAcceptedByUserIdAsync(user.Id);

        // Assert
        Assert.DoesNotContain(results, p => p.Id == policy.Id);
    }

    [Theory, DatabaseData]
    public async Task DoesNotReturnPolicies_ForOtherUsers(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        IPolicyRepository policyRepository)
    {
        // Arrange
        var targetUser = await userRepository.CreateTestUserAsync();
        var otherUser = await userRepository.CreateTestUserAsync();

        var organization = await organizationRepository.CreateTestOrganizationAsync();
        await organizationUserRepository.CreateConfirmedTestOrganizationUserAsync(organization, otherUser);
        var policy = await policyRepository.CreateAsync(new Policy
        {
            OrganizationId = organization.Id,
            Type = PolicyType.TwoFactorAuthentication,
            Enabled = true
        });

        // Act
        var results = await policyRepository.GetManyConfirmedAcceptedByUserIdAsync(targetUser.Id);

        // Assert
        Assert.DoesNotContain(results, p => p.Id == policy.Id);
    }
}
