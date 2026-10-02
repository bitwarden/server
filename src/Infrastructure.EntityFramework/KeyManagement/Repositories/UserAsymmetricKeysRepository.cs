#nullable enable
using AutoMapper;
using Bit.Core.KeyManagement.Models.Data;
using Bit.Core.KeyManagement.Repositories;
using Bit.Core.Repositories;
using Bit.Infrastructure.EntityFramework.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Infrastructure.EntityFramework.KeyManagement.Repositories;

public class UserAsymmetricKeysRepository : BaseEntityFrameworkRepository, IUserAsymmetricKeysRepository
{
    public UserAsymmetricKeysRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper) : base(
        serviceScopeFactory,
        mapper)
    {
    }

    public async Task RegenerateUserAsymmetricKeysAsync(UserAsymmetricKeys userAsymmetricKeys,
        IEnumerable<DatabaseTransactionAction> updateDataActions)
    {
        await using var scope = ServiceScopeFactory.CreateAsyncScope();
        var dbContext = GetDatabaseContext(scope);

        var connection = dbContext.Database.GetDbConnection();
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await dbContext.Database.UseTransactionAsync(transaction);

        var entity = await dbContext.Users.FindAsync(userAsymmetricKeys.UserId);
        if (entity != null)
        {
            var utcNow = DateTime.UtcNow;
            entity.PublicKey = userAsymmetricKeys.PublicKey;
            entity.PrivateKey = userAsymmetricKeys.UserKeyEncryptedPrivateKey;
            entity.RevisionDate = utcNow;
            entity.AccountRevisionDate = utcNow;
            await dbContext.SaveChangesAsync();
        }

        foreach (var action in updateDataActions)
        {
            await action(connection, transaction);
        }

        await transaction.CommitAsync();
    }
}
