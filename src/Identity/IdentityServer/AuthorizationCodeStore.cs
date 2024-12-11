using Duende.IdentityServer;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Stores;
using Duende.IdentityServer.Stores.Serialization;

namespace Bit.Identity.IdentityServer;

public class AuthorizationCodeStore : DefaultGrantStore<AuthorizationCode>, IAuthorizationCodeStore
{
    public AuthorizationCodeStore(
        IPersistedGrantStore store,
        IPersistentGrantSerializer serializer,
        IHandleGenerationService handleGenerationService,
        ILogger<DefaultAuthorizationCodeStore> logger
    )
        : base(
            IdentityServerConstants.PersistedGrantTypes.AuthorizationCode,
            store,
            serializer,
            handleGenerationService,
            logger
        ) { }

    public Task<string> StoreAuthorizationCodeAsync(AuthorizationCode code)
    {
        return CreateItemAsync(
            code,
            code.ClientId,
            code.Subject.GetSubjectId(),
            code.SessionId,
            code.Description,
            code.CreationTime,
            code.Lifetime
        );
    }

    public Task<AuthorizationCode> GetAuthorizationCodeAsync(string code)
    {
        return GetItemAsync(code);
    }

    public Task RemoveAuthorizationCodeAsync(string code)
    {
        // return RemoveItemAsync(code);

        // We don't want to delete authorization codes during validation.
        // We'll rely on the authorization code lifecycle for short term validation and the
        // DatabaseExpiredGrantsJob to clean up old authorization codes.
        return Task.FromResult(0);
    }
}
