using AutoMapper;
using Bit.Core.KeyManagement.Models.Data;
using Bit.Core.KeyManagement.Repositories;
using Bit.Infrastructure.EntityFramework.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Infrastructure.EntityFramework.KeyManagement.Repositories;

public class OrganizationUserKeyRepository : BaseEntityFrameworkRepository, IOrganizationUserKeyRepository
{
    public OrganizationUserKeyRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper)
        : base(serviceScopeFactory, mapper)
    {
    }

    public async Task<ICollection<OrganizationUserV2UpgradeDetails>> GetManyPendingV2UpgradesByOrganizationIdAsync(
        Guid organizationId)
    {
        await using var scope = ServiceScopeFactory.CreateAsyncScope();
        var dbContext = GetDatabaseContext(scope);

        // A row without a user key id cannot be upgraded. The server has nothing to validate the admin's
        // re-wrapped key against, so the row is not returned.
        return await (
            from organizationUser in dbContext.OrganizationUsers
            join user in dbContext.Users on organizationUser.UserId equals user.Id
            where organizationUser.OrganizationId == organizationId
                && organizationUser.V2UpgradeToken != null
                && organizationUser.ResetPasswordKey != null
                && user.UserKeyId != null
            select new OrganizationUserV2UpgradeDetails
            {
                OrganizationUserId = organizationUser.Id,
                UserKeyId = user.UserKeyId!,
                AccountRecoveryKey = organizationUser.ResetPasswordKey!,
                V2UpgradeToken = organizationUser.V2UpgradeToken!
            }).ToListAsync();
    }

    public async Task<int> UpdateManyV2UpgradedAccountRecoveryKeysAsync(Guid organizationId,
        IEnumerable<OrganizationUserAccountRecoveryKeyUpdate> updates)
    {
        var requested = updates.ToList();
        if (requested.Count == 0)
        {
            return 0;
        }

        // Two updates for one membership would make the outcome depend on write order. The Dapper procedure
        // reports this as a short row count, so report nothing written here as well.
        var distinctIdCount = requested.Select(update => update.OrganizationUserId).Distinct().Count();
        if (distinctIdCount != requested.Count)
        {
            return 0;
        }

        await using var scope = ServiceScopeFactory.CreateAsyncScope();
        var dbContext = GetDatabaseContext(scope);
        await using var transaction = await dbContext.Database.BeginTransactionAsync();

        var updatedCount = 0;
        foreach (var update in requested)
        {
            // The user key id is part of the update's WHERE clause, so the check and the write are one statement.
            // Reading the key id first and writing after would leave a window for a rotation between the two,
            // and the stale key would be installed.
            updatedCount += await dbContext.OrganizationUsers
                .Where(organizationUser => organizationUser.Id == update.OrganizationUserId
                    && organizationUser.OrganizationId == organizationId
                    && organizationUser.V2UpgradeToken != null
                    && dbContext.Users.Any(user =>
                        user.Id == organizationUser.UserId && user.UserKeyId == update.UserKeyId))
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(organizationUser => organizationUser.ResetPasswordKey, update.AccountRecoveryKey)
                    // The token is consumed, so the upgrade cannot be replayed.
                    .SetProperty(organizationUser => organizationUser.V2UpgradeToken, (string?)null));
        }

        // One stale row leaves every other row unchanged.
        if (updatedCount != requested.Count)
        {
            await transaction.RollbackAsync();
            return 0;
        }

        await transaction.CommitAsync();

        return updatedCount;
    }
}
