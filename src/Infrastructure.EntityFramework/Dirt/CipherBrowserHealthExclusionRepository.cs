using AutoMapper;
using Bit.Core.Dirt.Repositories;
using Bit.Core.Utilities;
using Bit.Core.Vault.Enums;
using Bit.Infrastructure.EntityFramework.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Infrastructure.EntityFramework.Dirt;

public class CipherBrowserHealthExclusionRepository : BaseEntityFrameworkRepository, ICipherBrowserHealthExclusionRepository
{
    public CipherBrowserHealthExclusionRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper)
        : base(serviceScopeFactory, mapper)
    {
    }

    public async Task<int> ExcludeAsync(IEnumerable<Guid> cipherIds, Guid userId)
    {
        var ids = cipherIds.ToList();
        if (ids.Count == 0)
        {
            return 0;
        }

        await using var scope = ServiceScopeFactory.CreateAsyncScope();
        var dbContext = GetDatabaseContext(scope);
        var ciphers = await dbContext.Ciphers
            .Where(c => ids.Contains(c.Id) && c.UserId == userId && c.OrganizationId == null
                && c.Type == CipherType.Login && c.DeletedDate == null)
            .ToListAsync();

        foreach (var cipher in ciphers)
        {
            var exclusions = string.IsNullOrWhiteSpace(cipher.BrowserHealthExclusions)
                ? new Dictionary<Guid, bool>()
                : CoreHelpers.LoadClassFromJsonData<Dictionary<Guid, bool>>(cipher.BrowserHealthExclusions)
                  ?? new Dictionary<Guid, bool>();
            exclusions[userId] = true;
            cipher.BrowserHealthExclusions = CoreHelpers.ClassToJsonData(exclusions);
        }

        if (ciphers.Count > 0)
        {
            await dbContext.UserBumpAccountRevisionDateAsync(userId);
        }
        await dbContext.SaveChangesAsync();
        return ciphers.Count;
    }
}
