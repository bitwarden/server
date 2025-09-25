using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models.Data;
using Bit.Core.Auth.Repositories;
using Bit.Core.Repositories;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.AdminConsole.Repositories;

public class ProviderUserRepositoryTests
{
    [Theory, DatabaseData]
    public async Task GetManyOrganizationDetailsByUserAsync_ShouldPopulateSsoPropertiesCorrectly(
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

        // Verify common properties for both organizations
        Assert.Equal(user.Id, orgWithSsoDetails.UserId);
        Assert.Equal(provider.Id, orgWithSsoDetails.ProviderId);
        Assert.Equal(provider.Name, orgWithSsoDetails.ProviderName);
        Assert.Equal(provider.Type, orgWithSsoDetails.ProviderType);

        Assert.Equal(user.Id, orgWithoutSsoDetails.UserId);
        Assert.Equal(provider.Id, orgWithoutSsoDetails.ProviderId);
        Assert.Equal(provider.Name, orgWithoutSsoDetails.ProviderName);
        Assert.Equal(provider.Type, orgWithoutSsoDetails.ProviderType);

        // Organization with SSO should have SSO properties populated
        Assert.True(orgWithSsoDetails.SsoEnabled);
        Assert.NotNull(orgWithSsoDetails.SsoConfig);
        Assert.Equal(serializedSsoConfigData, orgWithSsoDetails.SsoConfig);

        // Organization without SSO should have null SSO properties
        Assert.Null(orgWithoutSsoDetails.SsoEnabled);
        Assert.Null(orgWithoutSsoDetails.SsoConfig);

        // Cleanup
        await ssoConfigRepository.DeleteAsync(ssoConfig);
        await providerOrganizationRepository.DeleteAsync(providerOrganizationWithSso);
        await providerOrganizationRepository.DeleteAsync(providerOrganizationWithoutSso);
        await providerUserRepository.DeleteAsync(providerUser);
        await providerRepository.DeleteAsync(provider);
        await organizationRepository.DeleteAsync(organizationWithSso);
        await organizationRepository.DeleteAsync(organizationWithoutSso);
        await userRepository.DeleteAsync(user);
    }
}
