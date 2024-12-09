using AutoMapper;
using Bit.Core.Auth.Models.Data;
using Bit.Core.Auth.Repositories;
using Bit.Core.KeyManagement.UserKey;
using Bit.Infrastructure.EntityFramework.Auth.Models;
using Bit.Infrastructure.EntityFramework.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

#nullable enable

namespace Bit.Infrastructure.EntityFramework.Auth.Repositories;

public class WebAuthnCredentialRepository : Repository<Core.Auth.Entities.WebAuthnCredential, WebAuthnCredential, Guid>, IWebAuthnCredentialRepository
{
    public WebAuthnCredentialRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper)
        : base(serviceScopeFactory, mapper, (context) => context.WebAuthnCredentials)
    { }

    public async Task<Core.Auth.Entities.WebAuthnCredential?> GetByIdAsync(Guid id, Guid userId)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var query = dbContext.WebAuthnCredentials.Where(d => d.Id == id && d.UserId == userId);
            var cred = await query.FirstOrDefaultAsync();
            return Mapper.Map<Core.Auth.Entities.WebAuthnCredential>(cred);
        }
    }

    public async Task<ICollection<Core.Auth.Entities.WebAuthnCredential>> GetManyByUserIdAsync(Guid userId)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var query = dbContext.WebAuthnCredentials.Where(d => d.UserId == userId);
            var creds = await query.ToListAsync();
            return Mapper.Map<List<Core.Auth.Entities.WebAuthnCredential>>(creds);
        }
    }

    public async Task<bool> UpdateAsync(Core.Auth.Entities.WebAuthnCredential credential)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var cred = await dbContext.WebAuthnCredentials
                                .FirstOrDefaultAsync(d => d.Id == credential.Id &&
                                                          d.UserId == credential.UserId);
            if (cred == null)
            {
                return false;
            }

            cred.EncryptedPrivateKey = credential.EncryptedPrivateKey;
            cred.EncryptedPublicKey = credential.EncryptedPublicKey;
            cred.EncryptedUserKey = credential.EncryptedUserKey;

            await dbContext.SaveChangesAsync();
            return true;
        }
    }

    public UpdateEncryptedDataForKeyRotation UpdateKeysForRotationAsync(Guid userId, IEnumerable<WebAuthnLoginRotateKeyData> credentials)
    {
        return async (_, _) =>
        {
            var newCreds = credentials.ToList();
            using var scope = ServiceScopeFactory.CreateScope();
            var dbContext = GetDatabaseContext(scope);
            var userWebauthnCredentials = await GetDbSet(dbContext)
                .Where(wc => wc.Id == wc.Id)
                .ToListAsync();
            var validUserWebauthnCredentials = userWebauthnCredentials
                .Where(wc => newCreds.Any(nwc => nwc.Id == wc.Id))
                .Where(wc => wc.UserId == userId);

            foreach (var wc in validUserWebauthnCredentials)
            {
                var nwc = newCreds.First(eak => eak.Id == wc.Id);
                wc.EncryptedPublicKey = nwc.EncryptedPublicKey;
                wc.EncryptedUserKey = nwc.EncryptedUserKey;
            }

            await dbContext.SaveChangesAsync();
        };
    }

}
