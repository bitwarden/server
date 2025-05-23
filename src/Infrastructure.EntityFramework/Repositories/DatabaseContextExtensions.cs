﻿#nullable enable

using System.Diagnostics;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.Auth.Enums;
using Bit.Core.Enums;
using Bit.Infrastructure.EntityFramework.Repositories.Queries;
using Microsoft.EntityFrameworkCore;

#nullable enable

namespace Bit.Infrastructure.EntityFramework.Repositories;

public static class DatabaseContextExtensions
{
    /// <summary>
    /// Bump the account revision date for the user.
    /// The caller is responsible for providing a valid UserId (not a null or default Guid) for a user that exists
    /// in the database.
    /// </summary>
    public static async Task UserBumpAccountRevisionDateAsync(this DatabaseContext context, Guid userId)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("Invalid UserId.");
        }

        var user = await context.Users.FindAsync(userId);
        Debug.Assert(user is not null, "The user id is expected to be validated as a true-in database user before making this call.");
        user.AccountRevisionDate = DateTime.UtcNow;
    }

    public static async Task UserBumpManyAccountRevisionDatesAsync(this DatabaseContext context, ICollection<Guid> userIds)
    {
        var users = context.Users.Where(u => userIds.Contains(u.Id));
        var currentTime = DateTime.UtcNow;
        await users.ForEachAsync(u =>
        {
            context.Attach(u);
            u.AccountRevisionDate = currentTime;
        });
    }

    public static async Task UserBumpAccountRevisionDateByOrganizationIdAsync(this DatabaseContext context, Guid organizationId)
    {
        var users = await (from u in context.Users
                           join ou in context.OrganizationUsers on u.Id equals ou.UserId
                           where ou.OrganizationId == organizationId && ou.Status == OrganizationUserStatusType.Confirmed
                           select u).ToListAsync();

        UpdateUserRevisionDate(users);
    }

    public static async Task UserBumpAccountRevisionDateByCipherIdAsync(this DatabaseContext context, Guid cipherId, Guid organizationId)
    {
        var query = new UserBumpAccountRevisionDateByCipherIdQuery(cipherId, organizationId);
        var users = await query.Run(context).ToListAsync();
        UpdateUserRevisionDate(users);
    }

    public static async Task UserBumpAccountRevisionDateByCollectionIdAsync(this DatabaseContext context, Guid collectionId, Guid organizationId)
    {
        var query = from u in context.Users
                    join ou in context.OrganizationUsers
                        on u.Id equals ou.UserId
                    join cu in context.CollectionUsers
                        on new { OrganizationUserId = ou.Id, CollectionId = collectionId } equals
                        new { cu.OrganizationUserId, cu.CollectionId } into cu_g
                    from cu in cu_g.DefaultIfEmpty()
                    join gu in context.GroupUsers
                        on new { CollectionId = (Guid?)cu.CollectionId, OrganizationUserId = ou.Id } equals
                        new { CollectionId = (Guid?)null, gu.OrganizationUserId } into gu_g
                    from gu in gu_g.DefaultIfEmpty()
                    join g in context.Groups
                        on gu.GroupId equals g.Id into g_g
                    from g in g_g.DefaultIfEmpty()
                    join cg in context.CollectionGroups
                        on new { gu.GroupId, CollectionId = collectionId } equals
                        new { cg.GroupId, cg.CollectionId } into cg_g
                    from cg in cg_g.DefaultIfEmpty()
                    where ou.OrganizationId == organizationId &&
                      ou.Status == OrganizationUserStatusType.Confirmed &&
                        ((cu == null ? (Guid?)null : cu.CollectionId) != null ||
                        (cg == null ? (Guid?)null : cg.CollectionId) != null)
                    select u;

        var users = await query.ToListAsync();
        UpdateUserRevisionDate(users);
    }

    public static async Task UserBumpAccountRevisionDateByCollectionIdsAsync(this DatabaseContext context, IEnumerable<Guid> collectionIds, Guid organizationId)
    {
        var query = from u in context.Users
                    from c in context.Collections
                    join ou in context.OrganizationUsers
                        on u.Id equals ou.UserId
                    join cu in context.CollectionUsers
                        on new { OrganizationUserId = ou.Id, CollectionId = c.Id } equals
                        new { cu.OrganizationUserId, cu.CollectionId } into cu_g
                    from cu in cu_g.DefaultIfEmpty()
                    join gu in context.GroupUsers
                        on new { CollectionId = (Guid?)cu.CollectionId, OrganizationUserId = ou.Id } equals
                        new { CollectionId = (Guid?)null, gu.OrganizationUserId } into gu_g
                    from gu in gu_g.DefaultIfEmpty()
                    join g in context.Groups
                        on gu.GroupId equals g.Id into g_g
                    from g in g_g.DefaultIfEmpty()
                    join cg in context.CollectionGroups
                        on new { gu.GroupId, CollectionId = c.Id } equals
                        new { cg.GroupId, cg.CollectionId } into cg_g
                    from cg in cg_g.DefaultIfEmpty()
                    where ou.OrganizationId == organizationId && collectionIds.Contains(c.Id) &&
                      ou.Status == OrganizationUserStatusType.Confirmed &&
                        ((cu == null ? (Guid?)null : cu.CollectionId) != null ||
                        (cg == null ? (Guid?)null : cg.CollectionId) != null)
                    select u;

        var users = await query.ToListAsync();
        UpdateUserRevisionDate(users);
    }

    public static async Task UserBumpAccountRevisionDateByOrganizationUserIdAsync(this DatabaseContext context, Guid organizationUserId)
    {
        var query = from u in context.Users
                    join ou in context.OrganizationUsers
                      on u.Id equals ou.UserId
                    where ou.Id == organizationUserId && ou.Status == OrganizationUserStatusType.Confirmed
                    select u;

        var users = await query.ToListAsync();
        UpdateUserRevisionDate(users);
    }

    public static async Task UserBumpAccountRevisionDateByOrganizationUserIdsAsync(this DatabaseContext context, IEnumerable<Guid> organizationUserIds)
    {
        foreach (var organizationUserId in organizationUserIds)
        {
            await context.UserBumpAccountRevisionDateByOrganizationUserIdAsync(organizationUserId);
        }
    }

    public static async Task UserBumpAccountRevisionDateByEmergencyAccessGranteeIdAsync(this DatabaseContext context, Guid emergencyAccessId)
    {
        var query = from u in context.Users
                    join ea in context.EmergencyAccesses on u.Id equals ea.GranteeId
                    where ea.Id == emergencyAccessId && ea.Status == EmergencyAccessStatusType.Confirmed
                    select u;

        var users = await query.ToListAsync();

        UpdateUserRevisionDate(users);
    }

    public static async Task UserBumpAccountRevisionDateByProviderIdAsync(this DatabaseContext context, Guid providerId)
    {
        var query = from u in context.Users
                    join pu in context.ProviderUsers on u.Id equals pu.UserId
                    where pu.ProviderId == providerId && pu.Status == ProviderUserStatusType.Confirmed
                    select u;

        var users = await query.ToListAsync();
        UpdateUserRevisionDate(users);
    }

    public static async Task UserBumpAccountRevisionDateByProviderUserIdAsync(this DatabaseContext context, Guid providerUserId)
    {
        var query = from u in context.Users
                    join pu in context.ProviderUsers on u.Id equals pu.UserId
                    where pu.ProviderId == providerUserId && pu.Status == ProviderUserStatusType.Confirmed
                    select u;

        var users = await query.ToListAsync();
        UpdateUserRevisionDate(users);
    }

    private static void UpdateUserRevisionDate(List<Models.User> users)
    {
        var time = DateTime.UtcNow;
        foreach (var user in users)
        {
            user.AccountRevisionDate = time;
        }
    }
}
