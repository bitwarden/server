#nullable enable
using AutoMapper;
using Bit.Core.KeyManagement.Models.Data;
using Bit.Core.KeyManagement.Repositories;
using Bit.Core.KeyManagement.UserKey;
using Bit.Infrastructure.EntityFramework.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Infrastructure.EntityFramework.KeyManagement.Repositories;

public class UserSignatureKeyPairRepository : Repository<Core.KeyManagement.Entities.UserSignatureKeyPair, Models.UserSignatureKeyPair, Guid>, IUserSignatureKeyPairRepository
{
    public UserSignatureKeyPairRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper) : base(serviceScopeFactory, mapper, context => context.UserSignatureKeyPair)
    {
    }

    public async Task<SignatureKeyPairData?> GetByUserIdAsync(Guid userId)
    {
        await using var scope = ServiceScopeFactory.CreateAsyncScope();
        var dbContext = GetDatabaseContext(scope);
        var signingKeys = await dbContext.UserSignatureKeyPair.FindAsync(userId);
        if (signingKeys == null)
        {
            return null;
        }

        return signingKeys.ToSignatureKeyPairData();
    }

    public UpdateEncryptedDataForKeyRotation SetUserSignatureKeyPair(Guid userId, SignatureKeyPairData signingKeys)
    {
        return async (_, _) =>
        {
            await using var scope = ServiceScopeFactory.CreateAsyncScope();
            var dbContext = GetDatabaseContext(scope);
            var entity = new Models.UserSignatureKeyPair
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                SignatureAlgorithm = signingKeys.SignatureAlgorithm,
                SigningKey = signingKeys.WrappedSigningKey,
                VerifyingKey = signingKeys.VerifyingKey,
                CreationDate = DateTime.UtcNow,
                RevisionDate = DateTime.UtcNow,
            };
            await dbContext.UserSignatureKeyPair.AddAsync(entity);
            await dbContext.SaveChangesAsync();
        };
    }

    public UpdateEncryptedDataForKeyRotation UpdateForKeyRotation(Guid grantorId, SignatureKeyPairData signingKeys)
    {
        return async (_, _) =>
        {
            await using var scope = ServiceScopeFactory.CreateAsyncScope();
            var dbContext = GetDatabaseContext(scope);
            var entity = await dbContext.UserSignatureKeyPair.FirstOrDefaultAsync(x => x.UserId == grantorId);
            if (entity != null)
            {
                entity.SignatureAlgorithm = signingKeys.SignatureAlgorithm;
                entity.SigningKey = signingKeys.WrappedSigningKey;
                entity.VerifyingKey = signingKeys.VerifyingKey;
                entity.RevisionDate = DateTime.UtcNow;
                await dbContext.SaveChangesAsync();
            }
        };
    }
}
