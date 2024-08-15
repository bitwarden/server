using Bit.Core.AdminConsole.Entities;
using Bit.Core.Entities;
using Bit.Core.Models.Data;
using Bit.Core.Models.Data.Organizations;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Vault.Models.Data;

namespace Bit.Api.Tools.Models.Response;

/// <summary>
/// Member access details. The individual item for the detailed member access
/// report. A collection can be assigned directly to a user without a group or
/// the user can be assigned to a collection through a group. Group level permissions
/// can override collection level permissions.  
/// </summary>
public class MemberAccessReportAccessDetails
{
    public Guid CollectionId { get; set; }
    public Guid? GroupId { get; set; }
    public string GroupName { get; set; }
    public string CollectionName { get; set; }
    public int ItemCount { get; set; }
    public bool ReadOnly { get; set; }
    public bool HidePasswords { get; set; }
    public bool Manage { get; set; }
}

/// <summary>
/// Contains the collections and group collections a user has access to including
/// the permission level for the collection and group collection. 
/// </summary>
public class MemberAccessReportResponseModel
{
    public string UserName { get; set; }
    public string Email { get; set; }
    public bool TwoFactorEnabled { get; set; }
    public bool AccountRecoveryEnabled { get; set; }
    public int GroupsCount { get; set; }
    public int CollectionsCount { get; set; }
    public int TotalItemCount { get; set; }
    public IEnumerable<MemberAccessReportAccessDetails> AccessDetails { get; set; }

    /// <summary>
    /// Generates a report for all members of an organization. Containing summary information
    /// such as item, collection, and group counts. As well as detailed information on the
    /// user and group collections along with their permissions
    /// </summary>
    /// <param name="orgUsers">Organization user details collection</param>
    /// <param name="orgGroups">Organization groups collection</param>
    /// <param name="orgCollectionsWithAccess">Collections for the organization and the groups/users and permissions</param>
    /// <param name="orgItems">Cipher items for the organization with the collections associated with them</param>
    /// <param name="organizationUsersTwoFactorEnabled">Organization users two factor status</param>
    /// <param name="orgAbility">Organization ability for account recovery status</param>
    /// <returns>List of the MemberAccessReportResponseModel</returns>;
    public static IEnumerable<MemberAccessReportResponseModel> CreateReport(
        ICollection<Group> orgGroups,
        ICollection<Tuple<Collection, CollectionAccessDetails>> orgCollectionsWithAccess,
        IEnumerable<CipherOrganizationDetailsWithCollections> orgItems,
        IEnumerable<(OrganizationUserUserDetails user, bool twoFactorIsEnabled)> organizationUsersTwoFactorEnabled,
        OrganizationAbility orgAbility)
    {
        var orgUsers = organizationUsersTwoFactorEnabled.Select(x => x.user);
        // Create a dictionary to lookup the group names later. 
        var groupNameDictionary = orgGroups.ToDictionary(x => x.Id, x => x.Name);

        // Get collections grouped and into a dictionary for counts
        var collectionItems = orgItems
            .SelectMany(x => x.CollectionIds,
                (x, b) => new { CipherId = x.Id, CollectionId = b })
            .GroupBy(y => y.CollectionId,
                (key, g) => new { CollectionId = key, Ciphers = g });
        var collectionItemCounts = collectionItems.ToDictionary(x => x.CollectionId, x => x.Ciphers.Count());

        // Take the collections/groups and create the access details items
        var groupAccessDetails = new List<MemberAccessReportAccessDetails>();
        var userCollectionAccessDetails = new List<MemberAccessReportAccessDetails>();
        foreach (var tCollect in orgCollectionsWithAccess)
        {
            var itemCounts = collectionItemCounts.TryGetValue(tCollect.Item1.Id, out var itemCount) ? itemCount : 0;
            if (tCollect.Item2.Groups.Count() > 0)
            {
                var groupDetails = tCollect.Item2.Groups.Select(x =>
                    new MemberAccessReportAccessDetails
                    {
                        CollectionId = tCollect.Item1.Id,
                        CollectionName = tCollect.Item1.Name,
                        GroupId = x.Id,
                        GroupName = groupNameDictionary[x.Id],
                        ReadOnly = x.ReadOnly,
                        HidePasswords = x.HidePasswords,
                        Manage = x.Manage,
                        ItemCount = itemCounts,
                    });
                groupAccessDetails.AddRange(groupDetails);
            }

            // All collections assigned to users and their permissions
            if (tCollect.Item2.Users.Count() > 0)
            {
                var userCollectionDetails = tCollect.Item2.Users.Select(x =>
                    new MemberAccessReportAccessDetails
                    {
                        CollectionId = tCollect.Item1.Id,
                        CollectionName = tCollect.Item1.Name,
                        ReadOnly = x.ReadOnly,
                        HidePasswords = x.HidePasswords,
                        Manage = x.Manage,
                        ItemCount = itemCounts,
                    });
                userCollectionAccessDetails.AddRange(userCollectionDetails);
            }
        }

        // Loop through the org users and populate report and access data
        var memberAccessReport = new List<MemberAccessReportResponseModel>();
        foreach (var user in orgUsers)
        {
            var report = new MemberAccessReportResponseModel
            {
                UserName = user.Name,
                Email = user.Email,
                TwoFactorEnabled = organizationUsersTwoFactorEnabled.FirstOrDefault(u => u.user.Id == user.Id).twoFactorIsEnabled,
                // Both the user's ResetPasswordKey must be set and the organization can UseResetPassword
                AccountRecoveryEnabled = !string.IsNullOrEmpty(user.ResetPasswordKey) && orgAbility.UseResetPassword
            };

            var userAccessDetails = new List<MemberAccessReportAccessDetails>();
            if (user.Groups.Any())
            {
                var userGroups = groupAccessDetails.Where(x => user.Groups.Contains(x.GroupId.GetValueOrDefault()));
                userAccessDetails.AddRange(userGroups);
            }

            if (user.Collections.Any())
            {
                var userCollections = userCollectionAccessDetails.Where(x => user.Collections.Any(y => x.CollectionId == y.Id));
                userAccessDetails.AddRange(userCollections);
            }
            report.AccessDetails = userAccessDetails;

            report.TotalItemCount = collectionItems
                .Where(x => report.AccessDetails.Any(y => x.CollectionId == y.CollectionId))
                .SelectMany(x => x.Ciphers)
                .GroupBy(g => g.CipherId).Select(grp => grp.FirstOrDefault())
                .Count();

            // Distinct items only            
            var distinctItems = report.AccessDetails.Select(x => x.CollectionId).Distinct();
            report.CollectionsCount = distinctItems.Count();
            report.GroupsCount = report.AccessDetails.Select(x => x.GroupId).Where(y => y.HasValue).Distinct().Count();
            memberAccessReport.Add(report);
        }
        return memberAccessReport;
    }

}
