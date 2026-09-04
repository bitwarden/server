using AutoMapper;
using Bit.Core.Dirt.Reports.Models.Data;
using Bit.Core.Dirt.Reports.Repositories;
using Bit.Core.Enums;
using Bit.Core.Utilities;
using Bit.Infrastructure.EntityFramework.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Infrastructure.EntityFramework.Dirt.Repositories;

/// <summary>
/// EF translation of [dbo].[MemberAdoptionReport_ReadByOrganizationId] for MySQL, PostgreSQL and SQLite.
/// </summary>
public class MemberAdoptionReportRepository : BaseEntityFrameworkRepository, IMemberAdoptionReportRepository
{
    private const int ReportCommandTimeoutSeconds = 120;

    public MemberAdoptionReportRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper)
        : base(serviceScopeFactory, mapper)
    {
    }

    public async Task<IReadOnlyList<MemberAdoptionReportDetail>> GetMemberAdoptionDetailsByOrganizationIdAsync(
        Guid organizationId)
    {
        await using var scope = ServiceScopeFactory.CreateAsyncScope();
        var dbContext = GetDatabaseContext(scope);
        dbContext.Database.SetCommandTimeout(ReportCommandTimeoutSeconds);

        var sharedItemCounts = await GetSharedItemCountsByOrganizationUserIdAsync(dbContext, organizationId);

        var details = await (
            from organizationUser in dbContext.OrganizationUsers
            where organizationUser.OrganizationId == organizationId
                && organizationUser.Status == OrganizationUserStatusType.Confirmed
            join user in dbContext.Users
                on organizationUser.UserId equals (Guid?)user.Id into userJoin
            from user in userJoin.DefaultIfEmpty()
            orderby user.Email ?? organizationUser.Email, organizationUser.Id
            select new MemberAdoptionReportDetail
            {
                OrganizationUserId = organizationUser.Id,
                UserId = organizationUser.UserId,
                Name = user.Name,
                Email = user.Email ?? organizationUser.Email ?? string.Empty,
                LastActivityDate = dbContext.Devices
                    .Where(device => device.UserId == organizationUser.UserId)
                    .Max(device => device.LastActivityDate),
                HasExtensionInstalled = dbContext.Devices
                    .Any(device => device.UserId == organizationUser.UserId
                        && DeviceTypes.BrowserExtensionTypes.Contains(device.Type)),
                VaultItemCount = dbContext.Ciphers
                    .Count(cipher => cipher.UserId == organizationUser.UserId
                        && cipher.OrganizationId == null
                        && cipher.DeletedDate == null),
                HasRedeemedSponsorship = dbContext.OrganizationSponsorships
                    .Any(sponsorship => sponsorship.SponsoringOrganizationUserId == organizationUser.Id
                        && sponsorship.SponsoredOrganizationId != null)
            })
            .ToListAsync();

        foreach (var detail in details)
        {
            detail.SharedItemCount = sharedItemCounts.GetValueOrDefault(detail.OrganizationUserId);
        }

        return details;
    }

    /// <summary>
    /// Counts the distinct organization-owned ciphers each member can reach, directly or through their groups.
    /// </summary>
    private static async Task<Dictionary<Guid, int>> GetSharedItemCountsByOrganizationUserIdAsync(
        DatabaseContext dbContext,
        Guid organizationId)
    {
        var directCollectionAccess =
            from collectionUser in dbContext.CollectionUsers
            join collection in dbContext.Collections
                on collectionUser.CollectionId equals collection.Id
            where collection.OrganizationId == organizationId
            select new { collectionUser.OrganizationUserId, collectionUser.CollectionId };

        var groupCollectionAccess =
            from groupUser in dbContext.GroupUsers
            join collectionGroup in dbContext.CollectionGroups
                on groupUser.GroupId equals collectionGroup.GroupId
            join collection in dbContext.Collections
                on collectionGroup.CollectionId equals collection.Id
            where collection.OrganizationId == organizationId
            select new { groupUser.OrganizationUserId, collectionGroup.CollectionId };

        return await (
            from access in directCollectionAccess.Union(groupCollectionAccess)
            join collectionCipher in dbContext.CollectionCiphers
                on access.CollectionId equals collectionCipher.CollectionId
            join cipher in dbContext.Ciphers
                on collectionCipher.CipherId equals cipher.Id
            where cipher.OrganizationId == organizationId && cipher.DeletedDate == null
            select new { access.OrganizationUserId, cipher.Id })
            .Distinct()
            .GroupBy(reachableCipher => reachableCipher.OrganizationUserId)
            .Select(grouping => new { OrganizationUserId = grouping.Key, Count = grouping.Count() })
            .ToDictionaryAsync(result => result.OrganizationUserId, result => result.Count);
    }
}
