using AutoMapper;
using Bit.Core.Dirt.Reports.Models.Data;
using Bit.Core.Dirt.Reports.ReportFeatures;
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

        var access = await GetMemberCollectionAccessAsync(dbContext, organizationId);
        var collectionCiphers = await GetCollectionCipherLinksAsync(dbContext, organizationId);
        var details = await GetConfirmedMemberDetailsAsync(dbContext, organizationId);

        var sharedItemCounts = SharedItemCountCalculator.Calculate(access, collectionCiphers);

        foreach (var detail in details)
        {
            detail.SharedItemCount = sharedItemCounts.GetValueOrDefault(detail.OrganizationUserId);
        }

        return details;
    }

    /// <summary>
    /// Reads every member-to-collection edge in the organization, whether the grant is direct or
    /// inherited from a group.
    /// </summary>
    internal static Task<List<MemberCollectionAccess>> GetMemberCollectionAccessAsync(
        DatabaseContext dbContext,
        Guid organizationId)
    {
        var directAccess =
            from collectionUser in dbContext.CollectionUsers
            join collection in dbContext.Collections
                on collectionUser.CollectionId equals collection.Id
            where collection.OrganizationId == organizationId
            select new { collectionUser.OrganizationUserId, collectionUser.CollectionId };

        var groupAccess =
            from groupUser in dbContext.GroupUsers
            join collectionGroup in dbContext.CollectionGroups
                on groupUser.GroupId equals collectionGroup.GroupId
            join collection in dbContext.Collections
                on collectionGroup.CollectionId equals collection.Id
            where collection.OrganizationId == organizationId
            select new { groupUser.OrganizationUserId, collectionGroup.CollectionId };

        // Union, not Concat: a member who reaches one collection both directly and through one or more
        // groups is a single edge, and the database is the cheap place to collapse that. The projection
        // has to stay anonymous until after the set operation, because EF cannot translate a union whose
        // sides already project into MemberCollectionAccess.
        return directAccess
            .Union(groupAccess)
            .Select(edge => new MemberCollectionAccess(edge.OrganizationUserId, edge.CollectionId))
            .ToListAsync();
    }

    /// <summary>
    /// Reads every collection-to-cipher edge in the organization, restricted to organization-owned
    /// ciphers that are not in the trash.
    /// </summary>
    internal static Task<List<CollectionCipherLink>> GetCollectionCipherLinksAsync(
        DatabaseContext dbContext,
        Guid organizationId)
    {
        // CollectionCipher is keyed on (CollectionId, CipherId), so these edges are already distinct.
        return (
            from collectionCipher in dbContext.CollectionCiphers
            join collection in dbContext.Collections
                on collectionCipher.CollectionId equals collection.Id
            join cipher in dbContext.Ciphers
                on collectionCipher.CipherId equals cipher.Id
            where collection.OrganizationId == organizationId
                && cipher.OrganizationId == organizationId
                && cipher.DeletedDate == null
            select new CollectionCipherLink(collectionCipher.CollectionId, collectionCipher.CipherId))
            .ToListAsync();
    }

    /// <summary>
    /// Reads one row per confirmed member with everything the report needs except the shared item count.
    /// </summary>
    internal static Task<List<MemberAdoptionReportDetail>> GetConfirmedMemberDetailsAsync(
        DatabaseContext dbContext,
        Guid organizationId)
    {
        return (
            from organizationUser in dbContext.OrganizationUsers
            where organizationUser.OrganizationId == organizationId
                && organizationUser.Status == OrganizationUserStatusType.Confirmed
            join user in dbContext.Users
                on organizationUser.UserId equals (Guid?)user.Id into userJoin
            from user in userJoin.DefaultIfEmpty()
            // Sort on the coalesced email the report displays, so a member with no email either side
            // sorts as the empty string rather than as a null each provider places differently.
            orderby user.Email ?? organizationUser.Email ?? string.Empty, organizationUser.Id
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
                // A confirmed member can still have no linked user. The null check keeps that member
                // from matching every unowned cipher, which EF's null semantics would otherwise do.
                VaultItemCount = dbContext.Ciphers
                    .Count(cipher => cipher.UserId != null
                        && cipher.UserId == organizationUser.UserId
                        && cipher.OrganizationId == null
                        && cipher.DeletedDate == null),
                HasRedeemedSponsorship = dbContext.OrganizationSponsorships
                    .Any(sponsorship => sponsorship.SponsoringOrganizationUserId == organizationUser.Id
                        && sponsorship.SponsoredOrganizationId != null)
            })
            .ToListAsync();
    }
}
