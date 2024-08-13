﻿using Bit.Core.AdminConsole.Entities;
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

    // internal to not expose 
    internal Guid? UserGuid { get; set; }
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
    /// <param name="orgId">Id of the organization to generate the report</param>
    /// <returns>List of MemberAccessReportResponseModel</returns>
    public static IEnumerable<MemberAccessReportResponseModel> CreateReport(
        IEnumerable<OrganizationUserUserDetails> orgUsers,
        ICollection<Group> orgGroups,
        ICollection<Tuple<Collection, CollectionAccessDetails>> orgCollectionsWithAccess,
        IEnumerable<CipherOrganizationDetailsWithCollections> orgItems,
        IEnumerable<(OrganizationUserUserDetails user, bool twoFactorIsEnabled)> organizationUsersTwoFactorEnabled,
        OrganizationAbility orgAbility)
    {
        // Create a dictionary to lookup the group names later. 
        var groupNameDictionary = orgGroups.ToDictionary(x => x.Id, x => x.Name);

        // Get collection counts into a dictionary
        var groupCollectionItems = orgItems.SelectMany(x => x.CollectionIds, (x, b) => new { CipherId = x.Id, CollectionId = b }).GroupBy(y => y.CollectionId, (key, g) => new { CollectionId = key, ItemCount = g.Count() });
        var collectionCountDictionary = groupCollectionItems.ToDictionary(x => x.CollectionId, x => x.ItemCount);

        // Take the collections/groups and create the access details items
        var accessDetails = new List<MemberAccessReportAccessDetails>();
        foreach (var tCollect in orgCollectionsWithAccess)
        {
            var itemCounts = collectionCountDictionary.TryGetValue(tCollect.Item1.Id, out var itemCount) ? itemCount : 0;
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
                accessDetails.AddRange(groupDetails);
            }

            // All collections assigned to users and their permissions
            if (tCollect.Item2.Users.Count() > 0)
            {
                var userCollectionDetails = tCollect.Item2.Users.Select(x =>
                    new MemberAccessReportAccessDetails
                    {
                        CollectionId = tCollect.Item1.Id,
                        CollectionName = tCollect.Item1.Name,
                        UserGuid = x.Id,
                        ReadOnly = x.ReadOnly,
                        HidePasswords = x.HidePasswords,
                        Manage = x.Manage,
                        ItemCount = itemCounts,
                    });
                accessDetails.AddRange(userCollectionDetails);
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
                var userGroups = accessDetails.Where(x => user.Groups.Contains(x.GroupId.GetValueOrDefault()));
                userAccessDetails.AddRange(userGroups);
            }

            if (user.Collections.Any())
            {
                var userCollections = accessDetails.Where(x => user.Collections.Any(y => x.CollectionId == y.Id && x.UserGuid == user.Id));
                userAccessDetails.AddRange(userCollections);
            }
            report.AccessDetails = userAccessDetails;

            // Distinct items only            
            var distinctItems = report.AccessDetails.Select(x => x.CollectionId).Distinct();
            report.TotalItemCount = distinctItems.Select(x => collectionCountDictionary.TryGetValue(x, out var count) ? count : 0).Sum();
            report.CollectionsCount = distinctItems.Count();
            report.GroupsCount = report.AccessDetails.Select(x => x.GroupId).Where(y => y.HasValue).Distinct().Count();
            memberAccessReport.Add(report);
        }
        return memberAccessReport;
    }

}
