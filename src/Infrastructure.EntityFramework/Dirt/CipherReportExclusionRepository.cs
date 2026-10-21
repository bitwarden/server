using AutoMapper;
using Bit.Core.Dirt.Repositories;
using Bit.Core.Utilities;
using Bit.Core.Vault.Enums;
using Bit.Infrastructure.EntityFramework.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Infrastructure.EntityFramework.Dirt;

public class CipherReportExclusionRepository : BaseEntityFrameworkRepository, ICipherReportExclusionRepository
{
    public CipherReportExclusionRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper)
        : base(serviceScopeFactory, mapper)
    {
    }

    public async Task<int> ExcludeAsync(IEnumerable<Guid> cipherIds, Guid userId)
    {
        await using var scope = ServiceScopeFactory.CreateAsyncScope();
        var dbContext = GetDatabaseContext(scope);
        var ids = cipherIds.ToList();
        var ciphers = await dbContext.Ciphers
            .Where(c => ids.Contains(c.Id) && c.UserId == userId && c.OrganizationId == null
                && c.Type == CipherType.Login && c.DeletedDate == null)
            .ToListAsync();

        var utcNow = DateTime.UtcNow;
        foreach (var cipher in ciphers)
        {
            var exclusions = string.IsNullOrWhiteSpace(cipher.ReportExclusions)
                ? new Dictionary<Guid, DateTime>()
                : CoreHelpers.LoadClassFromJsonData<Dictionary<Guid, DateTime>>(cipher.ReportExclusions)
                  ?? new Dictionary<Guid, DateTime>();
            exclusions[userId] = utcNow;
            cipher.ReportExclusions = CoreHelpers.ClassToJsonData(exclusions);
        }

        if (ciphers.Count > 0)
        {
            await dbContext.UserBumpAccountRevisionDateAsync(userId);
        }
        await dbContext.SaveChangesAsync();
        return ciphers.Count;
    }
}
