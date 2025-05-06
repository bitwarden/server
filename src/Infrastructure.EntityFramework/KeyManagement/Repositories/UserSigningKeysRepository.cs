#nullable enable
using AutoMapper;
using Bit.Core.KeyManagement.Models.Data;
using Bit.Core.KeyManagement.Repositories;
using Bit.Core.KeyManagement.UserKey;
using Bit.Infrastructure.EntityFramework.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Infrastructure.EntityFramework.KeyManagement.Repositories;

public class UserSigningKeysRepository : Repository<Core.Entities.UserSigningKeys, Models.UserSigningKeys, Guid>, IUserSigningKeysRepository
{
    public UserSigningKeysRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper, Func<DatabaseContext, DbSet<Models.UserSigningKeys>> getDbSet) : base(serviceScopeFactory, mapper, getDbSet)
    {
    }

    public async Task<SigningKeyData?> GetByUserIdAsync(Guid userId)
    {
        await using var scope = ServiceScopeFactory.CreateAsyncScope();
        var dbContext = GetDatabaseContext(scope);
        var signingKeys = await dbContext.UserSigningKeys.FindAsync(userId);
        if (signingKeys == null)
        {
            return null;
        }

        return new SigningKeyData
        {
            KeyAlgorithm = signingKeys.KeyType,
            VerifyingKey = signingKeys.VerifyingKey,
            WrappedSigningKey = signingKeys.SigningKey,
        };
    }

    public UpdateEncryptedDataForKeyRotation SetUserSigningKeys(Guid userId, SigningKeyData signingKeys)
    {
        return async (_, _) =>
        {
            await using var scope = ServiceScopeFactory.CreateAsyncScope();
            var dbContext = GetDatabaseContext(scope);
            var entity = new Models.UserSigningKeys
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                KeyType = signingKeys.KeyAlgorithm,
                VerifyingKey = signingKeys.VerifyingKey,
                SigningKey = signingKeys.WrappedSigningKey,
                CreationDate = DateTime.UtcNow,
                RevisionDate = DateTime.UtcNow,
            };
            await dbContext.UserSigningKeys.AddAsync(entity);
            await dbContext.SaveChangesAsync();
        };
    }

    public UpdateEncryptedDataForKeyRotation UpdateForKeyRotation(Guid grantorId, SigningKeyData signingKeys)
    {
        return async (_, _) =>
        {
            await using var scope = ServiceScopeFactory.CreateAsyncScope();
            var dbContext = GetDatabaseContext(scope);
            var entity = await dbContext.UserSigningKeys.FirstOrDefaultAsync(x => x.UserId == grantorId);
            if (entity != null)
            {
                entity.KeyType = signingKeys.KeyAlgorithm;
                entity.VerifyingKey = signingKeys.VerifyingKey;
                entity.SigningKey = signingKeys.WrappedSigningKey;
                entity.RevisionDate = DateTime.UtcNow;
                await dbContext.SaveChangesAsync();
            }
        };
    }
}
