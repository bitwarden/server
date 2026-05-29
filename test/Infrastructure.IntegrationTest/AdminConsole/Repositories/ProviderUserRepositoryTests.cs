using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.AdminConsole.Models.Data.Provider;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models.Data;
using Bit.Core.Auth.Repositories;
using Bit.Core.Entities;
using Bit.Core.Repositories;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.AdminConsole.Repositories;

public class ProviderUserRepositoryTests
{
    [Theory, DatabaseData]
    public async Task GetManyOrganizationDetailsByUserAsync_ShouldPopulatePropertiesCorrectly(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IProviderRepository providerRepository,
        IProviderUserRepository providerUserRepository,
        IProviderOrganizationRepository providerOrganizationRepository,
        ISsoConfigRepository ssoConfigRepository)
    {
        var user = await userRepository.CreateTestUserAsync();
        var organizationWithSso = await organizationRepository.CreateTestOrganizationAsync();
        var organizationWithoutSso = await organizationRepository.CreateTestOrganizationAsync();

        var provider = await providerRepository.CreateAsync(new Provider
        {
            Name = "Test Provider",
            Enabled = true,
            Type = ProviderType.Msp
        });

        var providerUser = await providerUserRepository.CreateAsync(new ProviderUser
        {
            ProviderId = provider.Id,
            UserId = user.Id,
            Status = ProviderUserStatusType.Confirmed,
            Type = ProviderUserType.ProviderAdmin
        });

        var providerOrganizationWithSso = await providerOrganizationRepository.CreateAsync(new ProviderOrganization
        {
            ProviderId = provider.Id,
            OrganizationId = organizationWithSso.Id
        });

        var providerOrganizationWithoutSso = await providerOrganizationRepository.CreateAsync(new ProviderOrganization
        {
            ProviderId = provider.Id,
            OrganizationId = organizationWithoutSso.Id
        });

        // Create SSO configuration for first organization only
        var serializedSsoConfigData = new SsoConfigurationData
        {
            MemberDecryptionType = MemberDecryptionType.KeyConnector,
            KeyConnectorUrl = "https://keyconnector.example.com"
        }.Serialize();

        var ssoConfig = await ssoConfigRepository.CreateAsync(new SsoConfig
        {
            OrganizationId = organizationWithSso.Id,
            Enabled = true,
            Data = serializedSsoConfigData
        });
        var results = (await providerUserRepository.GetManyOrganizationDetailsByUserAsync(user.Id, ProviderUserStatusType.Confirmed)).ToList();

        Assert.Equal(2, results.Count);

        var orgWithSsoDetails = results.Single(r => r.OrganizationId == organizationWithSso.Id);
        var orgWithoutSsoDetails = results.Single(r => r.OrganizationId == organizationWithoutSso.Id);

        // Verify all properties for both organizations
        AssertProviderOrganizationDetails(orgWithSsoDetails, organizationWithSso, user, provider, providerUser);
        AssertProviderOrganizationDetails(orgWithoutSsoDetails, organizationWithoutSso, user, provider, providerUser);

        // Organization without SSO should have null SSO properties
        Assert.Null(orgWithoutSsoDetails.SsoEnabled);
        Assert.Null(orgWithoutSsoDetails.SsoConfig);

        // Organization with SSO should have SSO properties populated
        Assert.True(orgWithSsoDetails.SsoEnabled);
        Assert.NotNull(orgWithSsoDetails.SsoConfig);
        Assert.Equal(serializedSsoConfigData, orgWithSsoDetails.SsoConfig);
    }

    [Theory, DatabaseData]
    public async Task GetManyByManyUsersAsync_WithMultipleUsers_ReturnsAllProviderUsers(
        IUserRepository userRepository,
        IProviderRepository providerRepository,
        IProviderUserRepository providerUserRepository)
    {
        var user1 = await userRepository.CreateTestUserAsync();
        var user2 = await userRepository.CreateTestUserAsync();
        var user3 = await userRepository.CreateTestUserAsync();

        var provider1 = await providerRepository.CreateAsync(new Provider
        {
            Name = "Test Provider 1",
            Enabled = true,
            Type = ProviderType.Msp
        });

        var provider2 = await providerRepository.CreateAsync(new Provider
        {
            Name = "Test Provider 2",
            Enabled = true,
            Type = ProviderType.Reseller
        });

        var providerUser1 = await providerUserRepository.CreateAsync(new ProviderUser
        {
            Id = Guid.NewGuid(),
            ProviderId = provider1.Id,
            UserId = user1.Id,
            Status = ProviderUserStatusType.Confirmed,
            Type = ProviderUserType.ProviderAdmin
        });

        var providerUser2 = await providerUserRepository.CreateAsync(new ProviderUser
        {
            Id = Guid.NewGuid(),
            ProviderId = provider1.Id,
            UserId = user2.Id,
            Status = ProviderUserStatusType.Invited,
            Type = ProviderUserType.ServiceUser
        });

        var providerUser3 = await providerUserRepository.CreateAsync(new ProviderUser
        {
            Id = Guid.NewGuid(),
            ProviderId = provider2.Id,
            UserId = user3.Id,
            Status = ProviderUserStatusType.Confirmed,
            Type = ProviderUserType.ProviderAdmin
        });

        var userIds = new[] { user1.Id, user2.Id, user3.Id };

        var results = (await providerUserRepository.GetManyByManyUsersAsync(userIds)).ToList();

        Assert.Equal(3, results.Count);
        Assert.Contains(results, pu => pu.Id == providerUser1.Id && pu.UserId == user1.Id);
        Assert.Contains(results, pu => pu.Id == providerUser2.Id && pu.UserId == user2.Id);
        Assert.Contains(results, pu => pu.Id == providerUser3.Id && pu.UserId == user3.Id);
    }

    [Theory, DatabaseData]
    public async Task GetManyByManyUsersAsync_WithSingleUser_ReturnsSingleProviderUser(
        IUserRepository userRepository,
        IProviderRepository providerRepository,
        IProviderUserRepository providerUserRepository)
    {
        var user = await userRepository.CreateTestUserAsync();

        var provider = await providerRepository.CreateAsync(new Provider
        {
            Name = "Test Provider",
            Enabled = true,
            Type = ProviderType.Msp
        });

        var providerUser = await providerUserRepository.CreateAsync(new ProviderUser
        {
            Id = Guid.NewGuid(),
            ProviderId = provider.Id,
            UserId = user.Id,
            Status = ProviderUserStatusType.Confirmed,
            Type = ProviderUserType.ProviderAdmin
        });

        var results = (await providerUserRepository.GetManyByManyUsersAsync([user.Id])).ToList();

        Assert.Single(results);
        Assert.Equal(user.Id, results[0].UserId);
        Assert.Equal(provider.Id, results[0].ProviderId);
    }

    [Theory, DatabaseData]
    public async Task GetManyByManyUsersAsync_WithUserHavingMultipleProviders_ReturnsAllProviderUsers(
        IUserRepository userRepository,
        IProviderRepository providerRepository,
        IProviderUserRepository providerUserRepository)
    {
        var user = await userRepository.CreateTestUserAsync();

        var provider1 = await providerRepository.CreateAsync(new Provider
        {
            Name = "Test Provider 1",
            Enabled = true,
            Type = ProviderType.Msp
        });

        var provider2 = await providerRepository.CreateAsync(new Provider
        {
            Name = "Test Provider 2",
            Enabled = true,
            Type = ProviderType.Reseller
        });

        var providerUser1 = await providerUserRepository.CreateAsync(new ProviderUser
        {
            ProviderId = provider1.Id,
            UserId = user.Id,
            Status = ProviderUserStatusType.Confirmed,
            Type = ProviderUserType.ProviderAdmin
        });

        var providerUser2 = await providerUserRepository.CreateAsync(new ProviderUser
        {
            ProviderId = provider2.Id,
            UserId = user.Id,
            Status = ProviderUserStatusType.Confirmed,
            Type = ProviderUserType.ServiceUser
        });

        var results = (await providerUserRepository.GetManyByManyUsersAsync([user.Id])).ToList();

        Assert.Equal(2, results.Count);
        Assert.Contains(results, pu => pu.Id == providerUser1.Id);
        Assert.Contains(results, pu => pu.Id == providerUser2.Id);
    }

    [Theory, DatabaseData]
    public async Task GetManyByManyUsersAsync_WithEmptyUserIds_ReturnsEmpty(
        IProviderUserRepository providerUserRepository)
    {
        var results = await providerUserRepository.GetManyByManyUsersAsync(Array.Empty<Guid>());

        Assert.Empty(results);
    }

    [Theory, DatabaseData]
    public async Task GetManyByManyUsersAsync_WithNonExistentUserIds_ReturnsEmpty(
        IProviderUserRepository providerUserRepository)
    {
        var nonExistentUserIds = new[] { Guid.NewGuid(), Guid.NewGuid() };

        var results = await providerUserRepository.GetManyByManyUsersAsync(nonExistentUserIds);

        Assert.Empty(results);
    }

    [Theory, DatabaseData]
    public async Task GetManyByManyUsersAsync_WithMixedExistentAndNonExistentUserIds_ReturnsOnlyExistent(
        IUserRepository userRepository,
        IProviderRepository providerRepository,
        IProviderUserRepository providerUserRepository)
    {
        var existingUser = await userRepository.CreateTestUserAsync();

        var provider = await providerRepository.CreateAsync(new Provider
        {
            Name = "Test Provider",
            Enabled = true,
            Type = ProviderType.Msp
        });

        var providerUser = await providerUserRepository.CreateAsync(new ProviderUser
        {
            ProviderId = provider.Id,
            UserId = existingUser.Id,
            Status = ProviderUserStatusType.Confirmed,
            Type = ProviderUserType.ProviderAdmin
        });

        var userIds = new[] { existingUser.Id, Guid.NewGuid(), Guid.NewGuid() };

        var results = (await providerUserRepository.GetManyByManyUsersAsync(userIds)).ToList();

        Assert.Single(results);
        Assert.Equal(existingUser.Id, results[0].UserId);
    }

    [Theory, DatabaseData]
    public async Task GetManyByManyUsersAsync_ReturnsAllStatuses(
        IUserRepository userRepository,
        IProviderRepository providerRepository,
        IProviderUserRepository providerUserRepository)
    {
        var user1 = await userRepository.CreateTestUserAsync();
        var user2 = await userRepository.CreateTestUserAsync();
        var user3 = await userRepository.CreateTestUserAsync();

        var provider = await providerRepository.CreateAsync(new Provider
        {
            Name = "Test Provider",
            Enabled = true,
            Type = ProviderType.Msp
        });

        await providerUserRepository.CreateAsync(new ProviderUser
        {
            ProviderId = provider.Id,
            UserId = user1.Id,
            Status = ProviderUserStatusType.Invited,
            Type = ProviderUserType.ServiceUser
        });

        await providerUserRepository.CreateAsync(new ProviderUser
        {
            ProviderId = provider.Id,
            UserId = user2.Id,
            Status = ProviderUserStatusType.Accepted,
            Type = ProviderUserType.ServiceUser
        });

        await providerUserRepository.CreateAsync(new ProviderUser
        {
            ProviderId = provider.Id,
            UserId = user3.Id,
            Status = ProviderUserStatusType.Confirmed,
            Type = ProviderUserType.ProviderAdmin
        });

        var userIds = new[] { user1.Id, user2.Id, user3.Id };

        var results = (await providerUserRepository.GetManyByManyUsersAsync(userIds)).ToList();

        Assert.Equal(3, results.Count);
        Assert.Contains(results, pu => pu.UserId == user1.Id && pu.Status == ProviderUserStatusType.Invited);
        Assert.Contains(results, pu => pu.UserId == user2.Id && pu.Status == ProviderUserStatusType.Accepted);
        Assert.Contains(results, pu => pu.UserId == user3.Id && pu.Status == ProviderUserStatusType.Confirmed);
    }

    [Theory, DatabaseData]
    public async Task GetManyByManyUsersAsync_ReturnsAllProviderUserTypes(
        IUserRepository userRepository,
        IProviderRepository providerRepository,
        IProviderUserRepository providerUserRepository)
    {
        var user1 = await userRepository.CreateTestUserAsync();
        var user2 = await userRepository.CreateTestUserAsync();

        var provider = await providerRepository.CreateAsync(new Provider
        {
            Name = "Test Provider",
            Enabled = true,
            Type = ProviderType.Msp
        });

        await providerUserRepository.CreateAsync(new ProviderUser
        {
            ProviderId = provider.Id,
            UserId = user1.Id,
            Status = ProviderUserStatusType.Confirmed,
            Type = ProviderUserType.ServiceUser
        });

        await providerUserRepository.CreateAsync(new ProviderUser
        {
            ProviderId = provider.Id,
            UserId = user2.Id,
            Status = ProviderUserStatusType.Confirmed,
            Type = ProviderUserType.ProviderAdmin
        });

        var userIds = new[] { user1.Id, user2.Id };

        var results = (await providerUserRepository.GetManyByManyUsersAsync(userIds)).ToList();

        Assert.Equal(2, results.Count);
        Assert.Contains(results, pu => pu.UserId == user1.Id && pu.Type == ProviderUserType.ServiceUser);
        Assert.Contains(results, pu => pu.UserId == user2.Id && pu.Type == ProviderUserType.ProviderAdmin);
    }

    private static void AssertProviderOrganizationDetails(
        ProviderUserOrganizationDetails actual,
        Organization expectedOrganization,
        User expectedUser,
        Provider expectedProvider,
        ProviderUser expectedProviderUser)
    {
        // Organization properties
        Assert.Equal(expectedOrganization.Id, actual.OrganizationId);
        Assert.Equal(expectedUser.Id, actual.UserId);
        Assert.Equal(expectedOrganization.Name, actual.Name);
        Assert.Equal(expectedOrganization.UsePolicies, actual.UsePolicies);
        Assert.Equal(expectedOrganization.UseSso, actual.UseSso);
        Assert.Equal(expectedOrganization.UseKeyConnector, actual.UseKeyConnector);
        Assert.Equal(expectedOrganization.UseScim, actual.UseScim);
        Assert.Equal(expectedOrganization.UseGroups, actual.UseGroups);
        Assert.Equal(expectedOrganization.UseDirectory, actual.UseDirectory);
        Assert.Equal(expectedOrganization.UseEvents, actual.UseEvents);
        Assert.Equal(expectedOrganization.UseTotp, actual.UseTotp);
        Assert.Equal(expectedOrganization.Use2fa, actual.Use2fa);
        Assert.Equal(expectedOrganization.UseApi, actual.UseApi);
        Assert.Equal(expectedOrganization.UseResetPassword, actual.UseResetPassword);
        Assert.Equal(expectedOrganization.UsersGetPremium, actual.UsersGetPremium);
        Assert.Equal(expectedOrganization.UseCustomPermissions, actual.UseCustomPermissions);
        Assert.Equal(expectedOrganization.SelfHost, actual.SelfHost);
        Assert.Equal(expectedOrganization.Seats, actual.Seats);
        Assert.Equal(expectedOrganization.MaxCollections, actual.MaxCollections);
        Assert.Equal(expectedOrganization.MaxStorageGb, actual.MaxStorageGb);
        Assert.Equal(expectedOrganization.Identifier, actual.Identifier);
        Assert.Equal(expectedOrganization.PublicKey, actual.PublicKey);
        Assert.Equal(expectedOrganization.PrivateKey, actual.PrivateKey);
        Assert.Equal(expectedOrganization.Enabled, actual.Enabled);
        Assert.Equal(expectedOrganization.PlanType, actual.PlanType);
        Assert.Equal(expectedOrganization.LimitCollectionCreation, actual.LimitCollectionCreation);
        Assert.Equal(expectedOrganization.LimitCollectionDeletion, actual.LimitCollectionDeletion);
        Assert.Equal(expectedOrganization.LimitItemDeletion, actual.LimitItemDeletion);
        Assert.Equal(expectedOrganization.AllowAdminAccessToAllCollectionItems, actual.AllowAdminAccessToAllCollectionItems);
        Assert.Equal(expectedOrganization.UseRiskInsights, actual.UseRiskInsights);
        Assert.Equal(expectedOrganization.UseOrganizationDomains, actual.UseOrganizationDomains);
        Assert.Equal(expectedOrganization.UseAdminSponsoredFamilies, actual.UseAdminSponsoredFamilies);
        Assert.Equal(expectedOrganization.UseAutomaticUserConfirmation, actual.UseAutomaticUserConfirmation);

        // Provider-specific properties
        Assert.Equal(expectedProvider.Id, actual.ProviderId);
        Assert.Equal(expectedProvider.Name, actual.ProviderName);
        Assert.Equal(expectedProvider.Type, actual.ProviderType);
        Assert.Equal(expectedProviderUser.Id, actual.ProviderUserId);
        Assert.Equal(expectedProviderUser.Status, actual.Status);
        Assert.Equal(expectedProviderUser.Type, actual.Type);
    }


}
