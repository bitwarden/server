
using AutoMapper;
using Bit.Core.KeyManagement.Models.Data;
using Bit.Core.KeyManagement.Repositories;
using Bit.Core.Repositories;
using Bit.Core.Utilities;
using Bit.Infrastructure.EntityFramework.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Infrastructure.EntityFramework.KeyManagement.Repositories;

public class UserSignatureKeyPairRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper) : Repository<Core.KeyManagement.Entities.UserSignatureKeyPair, Models.UserSignatureKeyPair, Guid>(serviceScopeFactory, mapper, context => context.UserSignatureKeyPairs), IUserSignatureKeyPairRepository
{
    public async Task<SignatureKeyPairData?> GetByUserIdAsync(Guid userId)
    {
        await using var scope = ServiceScopeFactory.CreateAsyncScope();
        var dbContext = GetDatabaseContext(scope);
        var signingKeys = await dbContext.UserSignatureKeyPairs.FirstOrDefaultAsync(x => x.UserId == userId);
        if (signingKeys == null)
        {
            return null;
        }

        return signingKeys.ToSignatureKeyPairData();
    }

    public DatabaseTransactionAction SetUserSignatureKeyPair(Guid userId, SignatureKeyPairData signingKeys)
    {
        return async (connection, transaction) =>
        {
            await using var scope = ServiceScopeFactory.CreateAsyncScope();
            var dbContext = GetTransactionalDatabaseContext(scope, connection, transaction);
            var entity = new Models.UserSignatureKeyPair
            {
                Id = CoreHelpers.GenerateComb(),
                UserId = userId,
                SignatureAlgorithm = signingKeys.SignatureAlgorithm,
                SigningKey = signingKeys.WrappedSigningKey,
                VerifyingKey = signingKeys.VerifyingKey,
                CreationDate = DateTime.UtcNow,
                RevisionDate = DateTime.UtcNow,
            };
            await dbContext.UserSignatureKeyPairs.AddAsync(entity);
            await dbContext.SaveChangesAsync();
        };
    }

    public DatabaseTransactionAction UpdateForKeyRotation(Guid grantorId, SignatureKeyPairData signingKeys)
    {
        return async (connection, transaction) =>
        {
            await using var scope = ServiceScopeFactory.CreateAsyncScope();
            var dbContext = GetTransactionalDatabaseContext(scope, connection, transaction);
            var entity = await dbContext.UserSignatureKeyPairs.FirstOrDefaultAsync(x => x.UserId == grantorId);
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
