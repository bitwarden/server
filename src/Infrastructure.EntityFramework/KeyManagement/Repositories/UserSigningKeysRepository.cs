#nullable enable
using AutoMapper;
using Bit.Core.KeyManagement.Models.Data;
using Bit.Core.KeyManagement.Repositories;
using Bit.Core.KeyManagement.UserKey;
using Bit.Infrastructure.EntityFramework.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Infrastructure.EntityFramework.KeyManagement.Repositories;

public class UserSignatureKeyPairRepository : Repository<Core.Entities.UserSignatureKeyPair, Models.UserSigningKeys, Guid>, IUserSignatureKeyPairRepository
{
    public UserSignatureKeyPairRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper) : base(serviceScopeFactory, mapper, context => context.UserSigningKeys)
    {
    }

    public async Task<SignatureKeyPairData?> GetByUserIdAsync(Guid userId)
    {
        await using var scope = ServiceScopeFactory.CreateAsyncScope();
        var dbContext = GetDatabaseContext(scope);
        var signingKeys = await dbContext.UserSigningKeys.FindAsync(userId);
        if (signingKeys == null)
        {
            return null;
        }

        return new SignatureKeyPairData
        {
            SignatureAlgorithm = signingKeys.SignatureAlgorithm,
            VerifyingKey = signingKeys.VerifyingKey,
            WrappedSigningKey = signingKeys.SigningKey,
        };
    }

    public UpdateEncryptedDataForKeyRotation SetUserSignatureKeyPair(Guid userId, SignatureKeyPairData signingKeys)
    {
        return async (_, _) =>
        {
            await using var scope = ServiceScopeFactory.CreateAsyncScope();
            var dbContext = GetDatabaseContext(scope);
            var entity = new Models.UserSigningKeys
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                SignatureAlgorithm = signingKeys.SignatureAlgorithm,
                VerifyingKey = signingKeys.VerifyingKey,
                SigningKey = signingKeys.WrappedSigningKey,
                CreationDate = DateTime.UtcNow,
                RevisionDate = DateTime.UtcNow,
            };
            await dbContext.UserSigningKeys.AddAsync(entity);
            await dbContext.SaveChangesAsync();
        };
    }

    public UpdateEncryptedDataForKeyRotation UpdateForKeyRotation(Guid grantorId, SignatureKeyPairData signingKeys)
    {
        return async (_, _) =>
        {
            await using var scope = ServiceScopeFactory.CreateAsyncScope();
            var dbContext = GetDatabaseContext(scope);
            var entity = await dbContext.UserSigningKeys.FirstOrDefaultAsync(x => x.UserId == grantorId);
            if (entity != null)
            {
                entity.SignatureAlgorithm = signingKeys.SignatureAlgorithm;
                entity.VerifyingKey = signingKeys.VerifyingKey;
                entity.SigningKey = signingKeys.WrappedSigningKey;
                entity.RevisionDate = DateTime.UtcNow;
                await dbContext.SaveChangesAsync();
            }
        };
    }
}
