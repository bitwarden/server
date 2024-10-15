#nullable enable
using AutoMapper;
using Bit.Core.KeyManagement.Models.Data;
using Bit.Core.KeyManagement.Repositories;
using Bit.Infrastructure.EntityFramework.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Infrastructure.EntityFramework.KeyManagement.Repositories;

public class UserAsymmetricKeysRepository : BaseEntityFrameworkRepository, IUserAsymmetricKeysRepository
{
    public UserAsymmetricKeysRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper) : base(
        serviceScopeFactory,
        mapper)
    {
    }

    public async Task RegenerateUserAsymmetricKeysAsync(UserAsymmetricKeys userAsymmetricKeys)
    {
        await using var scope = ServiceScopeFactory.CreateAsyncScope();
        var dbContext = GetDatabaseContext(scope);

        var entity = await dbContext.Users.FindAsync(userAsymmetricKeys.UserId);
        if (entity != null)
        {
            entity.PublicKey = userAsymmetricKeys.PublicKey;
            entity.PrivateKey = userAsymmetricKeys.UserKeyEncryptedPrivateKey;
            await dbContext.SaveChangesAsync();
        }
    }
}
