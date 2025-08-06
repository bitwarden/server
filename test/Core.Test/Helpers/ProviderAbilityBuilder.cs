using System.Collections.Concurrent;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Models.Data.Provider;

namespace Bit.Core.Test.Helpers;

public static class ProviderAbilityBuilder
{
    public static ConcurrentDictionary<Guid, ProviderAbility> BuildConcurrentDictionary(ProviderUser providerUser) =>
        new(
            new[]
            {
                new KeyValuePair<Guid, ProviderAbility>(
                    providerUser.ProviderId,
                    new ProviderAbility
                    {
                        Id = providerUser.ProviderId,
                        UseEvents = true,
                        Enabled = true
                    })
            });


    public static ConcurrentDictionary<Guid, ProviderAbility> BuildConcurrentDictionary(Provider provider) =>
        new(
            new[]
            {
                new KeyValuePair<Guid, ProviderAbility>(
                    provider.Id,
                    new ProviderAbility
                    {
                        Id = provider.Id,
                        UseEvents = true,
                        Enabled = true
                    })
            });
}
