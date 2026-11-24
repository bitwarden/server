using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.AdminConsole.Repositories;
using Bit.IntegrationTestCommon.Factories;

#nullable enable

namespace Bit.Identity.IntegrationTest.Helpers;

/// <summary>
/// Arrange helpers for tests that need a user to belong to a provider.
/// </summary>
public static class ProviderTestHelpers
{
    /// <summary>
    /// Creates a new provider and makes the user a confirmed member of it.
    /// </summary>
    public static async Task<ProviderUser> CreateProviderWithConfirmedMemberAsync(
        IdentityApplicationFactory factory,
        Guid userId,
        ProviderUserType providerUserType)
    {
        var provider = await factory.Services.GetRequiredService<IProviderRepository>().CreateAsync(new Provider
        {
            Name = $"Provider Name | {Guid.NewGuid()}",
            Type = ProviderType.Msp,
            Status = ProviderStatusType.Created,
            Enabled = true,
        });

        return await factory.Services.GetRequiredService<IProviderUserRepository>().CreateAsync(new ProviderUser
        {
            ProviderId = provider.Id,
            UserId = userId,
            Status = ProviderUserStatusType.Confirmed,
            Type = providerUserType,
        });
    }
}
