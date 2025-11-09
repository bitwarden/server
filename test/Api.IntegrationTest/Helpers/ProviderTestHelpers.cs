using Bit.Api.IntegrationTest.Factories;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.AdminConsole.Repositories;

namespace Bit.Api.IntegrationTest.Helpers;

public static class ProviderTestHelpers
{
    /// <summary>
    /// Creates a provider and links it to an organization.
    /// This does NOT create any provider users.
    /// </summary>
    /// <param name="factory">The API application factory</param>
    /// <param name="organizationId">The organization ID to link to the provider</param>
    /// <param name="providerType">The type of provider to create</param>
    /// <param name="providerStatus">The provider status (defaults to Created)</param>
    /// <returns>The created provider</returns>
    public static async Task<Provider> CreateProviderAndLinkToOrganizationAsync(
        ApiApplicationFactory factory,
        Guid organizationId,
        ProviderType providerType,
        ProviderStatusType providerStatus = ProviderStatusType.Created)
    {
        var providerRepository = factory.GetService<IProviderRepository>();
        var providerOrganizationRepository = factory.GetService<IProviderOrganizationRepository>();

        // Create the provider
        var provider = await providerRepository.CreateAsync(new Provider
        {
            Name = $"Test {providerType} Provider",
            BusinessName = $"Test {providerType} Provider Business",
            BillingEmail = $"provider-{providerType.ToString().ToLower()}@example.com",
            Type = providerType,
            Status = providerStatus,
            Enabled = true
        });

        // Link the provider to the organization
        await providerOrganizationRepository.CreateAsync(new ProviderOrganization
        {
            ProviderId = provider.Id,
            OrganizationId = organizationId,
            Key = "test-provider-key"
        });

        return provider;
    }
}
