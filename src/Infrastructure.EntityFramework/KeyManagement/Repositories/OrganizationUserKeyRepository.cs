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

    public async Task<ICollection<Guid>> UpdateManyV2UpgradedAccountRecoveryKeysAsync(Guid organizationId,
        IEnumerable<OrganizationUserAccountRecoveryKeyUpdate> updates, DateTime revisionDate)
    {
        await using var scope = ServiceScopeFactory.CreateAsyncScope();
        var dbContext = GetDatabaseContext(scope);

        var updatedIds = new List<Guid>();
        foreach (var update in updates)
        {
            // The key id and the enrollment are checked in the WHERE clause, so a rotation or a withdrawal cannot
            // slip in between the check and the write. A row that no longer matches is skipped.
            var updatedCount = await dbContext.OrganizationUsers
                .Where(organizationUser => organizationUser.Id == update.OrganizationUserId
                    && organizationUser.OrganizationId == organizationId
                    && organizationUser.V2UpgradeToken != null
                    && organizationUser.ResetPasswordKey != null
                    && dbContext.Users.Any(user =>
                        user.Id == organizationUser.UserId && user.UserKeyId == update.UserKeyId))
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(organizationUser => organizationUser.ResetPasswordKey, update.AccountRecoveryKey)
                    // The token is consumed, so the upgrade cannot be replayed.
                    .SetProperty(organizationUser => organizationUser.V2UpgradeToken, (string?)null)
                    .SetProperty(organizationUser => organizationUser.RevisionDate, revisionDate));

            if (updatedCount > 0)
            {
                updatedIds.Add(update.OrganizationUserId);
            }
        }

        // Bump the account revision date of the members whose row was updated.
        await dbContext.Users
            .Where(user => dbContext.OrganizationUsers
                .Any(organizationUser => updatedIds.Contains(organizationUser.Id)
                    && organizationUser.UserId == user.Id))
            .ExecuteUpdateAsync(setters =>
                setters.SetProperty(user => user.AccountRevisionDate, revisionDate));

        return updatedIds;
    }
}
