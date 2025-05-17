#nullable enable

using Bit.Identity.IdentityServer.ClientProviders;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;

namespace Bit.Identity.IdentityServer;

public interface IClientProvider
{
    Task<Client?> GetAsync(string identifier);
}

internal class DynamicClientStore : IClientStore
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IClientProvider _apiKeyClientProvider;
    private readonly StaticClientStore _staticClientStore;

    public DynamicClientStore(
      IServiceProvider serviceProvider,
      [FromKeyedServices(SecretsManagerApiKeyProvider.ApiKeyPrefix)] IClientProvider apiKeyClientProvider,
      StaticClientStore staticClientStore
    )
    {
        _serviceProvider = serviceProvider;
        _apiKeyClientProvider = apiKeyClientProvider;
        _staticClientStore = staticClientStore;
    }

    public Task<Client?> FindClientByIdAsync(string clientId)
    {
        var clientIdSpan = clientId.AsSpan();

        var firstPeriod = clientIdSpan.IndexOf('.');

        if (firstPeriod == -1)
        {
            // No splitter, attempt but don't fail for a static client
            if (_staticClientStore.ApiClients.TryGetValue(clientId, out var client))
            {
                return Task.FromResult<Client?>(client);
            }
        }
        else
        {
            // Increment past the period
            var identifierName = clientIdSpan[..firstPeriod++];

            var identifier = clientIdSpan[firstPeriod..];

            // The identifier is required to be non-empty
            if (identifier.IsEmpty || identifier.IsWhiteSpace())
            {
                return Task.FromResult<Client?>(null);
            }

            // Once identifierName is proven valid, materialize the string
            var clientBuilder = _serviceProvider.GetKeyedService<IClientProvider>(identifierName.ToString());

            if (clientBuilder == null)
            {
                // No client registered by this identifier
                return Task.FromResult<Client?>(null);
            }

            return clientBuilder.GetAsync(identifier.ToString());
        }

        // It could be an ApiKey, give them the full thing to try,
        // this is a special case for legacy reasons, no other client should
        // be allowed without a prefixing identifier.
        return _apiKeyClientProvider.GetAsync(clientId);
    }
}
