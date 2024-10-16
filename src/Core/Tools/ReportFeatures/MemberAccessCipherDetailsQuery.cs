using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Auth.UserFeatures.TwoFactorAuth.Interfaces;
using Bit.Core.Entities;
using Bit.Core.Models.Data;
using Bit.Core.Models.Data.Organizations;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Tools.Models.Data;
using Bit.Core.Tools.ReportFeatures.OrganizationReportMembers.Interfaces;
using Bit.Core.Tools.ReportFeatures.Requests;
using Bit.Core.Vault.Models.Data;
using Bit.Core.Vault.Queries;
using Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Requests;

namespace Bit.Core.Tools.ReportFeatures;

public class MemberAccessCipherDetailsQuery : IMemberAccessCipherDetailsQuery
{
    private readonly IOrganizationUserUserDetailsQuery _organizationUserUserDetailsQuery;
    private readonly IGroupRepository _groupRepository;
    private readonly ICollectionRepository _collectionRepository;
    private readonly IOrganizationCiphersQuery _organizationCiphersQuery;
    private readonly IApplicationCacheService _applicationCacheService;
    private readonly ITwoFactorIsEnabledQuery _twoFactorIsEnabledQuery;

    public MemberAccessCipherDetailsQuery(
        IOrganizationUserUserDetailsQuery organizationUserUserDetailsQuery,
        IGroupRepository groupRepository,
        ICollectionRepository collectionRepository,
        IOrganizationCiphersQuery organizationCiphersQuery,
        IApplicationCacheService applicationCacheService,
        ITwoFactorIsEnabledQuery twoFactorIsEnabledQuery
    )
    {
        _organizationUserUserDetailsQuery = organizationUserUserDetailsQuery;
        _groupRepository = groupRepository;
        _collectionRepository = collectionRepository;
        _organizationCiphersQuery = organizationCiphersQuery;
        _applicationCacheService = applicationCacheService;
        _twoFactorIsEnabledQuery = twoFactorIsEnabledQuery;
    }

    public async Task<IEnumerable<MemberAccessCipherDetails>> GetMemberAccessCipherDetails(MemberAccessCipherDetailsRequest request)
    {
        var orgUsers = await _organizationUserUserDetailsQuery.GetOrganizationUserUserDetails(
            new OrganizationUserUserDetailsQueryRequest
            {
                OrganizationId = request.OrganizationId,
                IncludeCollections = true,
                IncludeGroups = true
            });

        var orgGroups = await _groupRepository.GetManyByOrganizationIdAsync(request.OrganizationId);
        var orgAbility = await _applicationCacheService.GetOrganizationAbilityAsync(request.OrganizationId);
        var orgCollectionsWithAccess = await _collectionRepository.GetManyByOrganizationIdWithAccessAsync(request.OrganizationId);
        var orgItems = await _organizationCiphersQuery.GetAllOrganizationCiphers(request.OrganizationId);
        var organizationUsersTwoFactorEnabled = await _twoFactorIsEnabledQuery.TwoFactorIsEnabledAsync(orgUsers);

        var memberAccessCipherDetails = GenerateAccessData(
            orgGroups,
            orgCollectionsWithAccess,
            orgItems,
            organizationUsersTwoFactorEnabled,
            orgAbility
        );

        return memberAccessCipherDetails;
    }

    /// <summary>
    /// Generates a report for all members of an organization. Containing summary information
    /// such as item, collection, and group counts. Including the cipherIds a member is assigned. 
    /// Child collection includes detailed information on the  user and group collections along
    /// with their permissions. 
    /// </summary>
    /// <param name="orgGroups">Organization groups collection</param>
    /// <param name="orgCollectionsWithAccess">Collections for the organization and the groups/users and permissions</param>
    /// <param name="orgItems">Cipher items for the organization with the collections associated with them</param>
    /// <param name="organizationUsersTwoFactorEnabled">Organization users and two factor status</param>
    /// <param name="orgAbility">Organization ability for account recovery status</param>
    /// <returns>List of the MemberAccessCipherDetailsModel</returns>;
    private IEnumerable<MemberAccessCipherDetails> GenerateAccessData(
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
                (cipher, collectionId) => new { Cipher = cipher, CollectionId = collectionId })
            .GroupBy(y => y.CollectionId,
                (key, ciphers) => new { CollectionId = key, Ciphers = ciphers });
        var itemLookup = collectionItems.ToDictionary(x => x.CollectionId.ToString(), x => x.Ciphers.Select(c => c.Cipher.Id.ToString()));

        // Loop through the org users and populate report and access data
        var memberAccessCipherDetails = new List<MemberAccessCipherDetails>();
        foreach (var user in orgUsers)
        {
            var groupAccessDetails = new List<MemberAccessDetails>();
            var userCollectionAccessDetails = new List<MemberAccessDetails>();
            foreach (var tCollect in orgCollectionsWithAccess)
            {
                var hasItems = itemLookup.TryGetValue(tCollect.Item1.Id.ToString(), out var items);
                var collectionCiphers = hasItems ? items.Select(x => x) : null;

                var itemCounts = hasItems ? collectionCiphers.Count() : 0;
                if (tCollect.Item2.Groups.Count() > 0)
                {

                    var groupDetails = tCollect.Item2.Groups.Where((tCollectGroups) => user.Groups.Contains(tCollectGroups.Id)).Select(x =>
                        new MemberAccessDetails
                        {
                            CollectionId = tCollect.Item1.Id,
                            CollectionName = tCollect.Item1.Name,
                            GroupId = x.Id,
                            GroupName = groupNameDictionary[x.Id],
                            ReadOnly = x.ReadOnly,
                            HidePasswords = x.HidePasswords,
                            Manage = x.Manage,
                            ItemCount = itemCounts,
                            CollectionCipherIds = items
                        });

                    groupAccessDetails.AddRange(groupDetails);
                }

                // All collections assigned to users and their permissions
                if (tCollect.Item2.Users.Count() > 0)
                {
                    var userCollectionDetails = tCollect.Item2.Users.Where((tCollectUser) => tCollectUser.Id == user.Id).Select(x =>
                        new MemberAccessDetails
                        {
                            CollectionId = tCollect.Item1.Id,
                            CollectionName = tCollect.Item1.Name,
                            ReadOnly = x.ReadOnly,
                            HidePasswords = x.HidePasswords,
                            Manage = x.Manage,
                            ItemCount = itemCounts,
                            CollectionCipherIds = items
                        });
                    userCollectionAccessDetails.AddRange(userCollectionDetails);
                }
            }

            var report = new MemberAccessCipherDetails
            {
                UserName = user.Name,
                Email = user.Email,
                TwoFactorEnabled = organizationUsersTwoFactorEnabled.FirstOrDefault(u => u.user.Id == user.Id).twoFactorIsEnabled,
                // Both the user's ResetPasswordKey must be set and the organization can UseResetPassword
                AccountRecoveryEnabled = !string.IsNullOrEmpty(user.ResetPasswordKey) && orgAbility.UseResetPassword,
                UserGuid = user.Id,
                UsesKeyConnector = user.UsesKeyConnector
            };

            var userAccessDetails = new List<MemberAccessDetails>();
            if (user.Groups.Any())
            {
                var userGroups = groupAccessDetails.Where(x => user.Groups.Contains(x.GroupId.GetValueOrDefault()));
                userAccessDetails.AddRange(userGroups);
            }

            // There can be edge cases where groups don't have a collection
            var groupsWithoutCollections = user.Groups.Where(x => !userAccessDetails.Any(y => x == y.GroupId));
            if (groupsWithoutCollections.Count() > 0)
            {
                var emptyGroups = groupsWithoutCollections.Select(x => new MemberAccessDetails
                {
                    GroupId = x,
                    GroupName = groupNameDictionary[x],
                    ItemCount = 0
                });
                userAccessDetails.AddRange(emptyGroups);
            }

            if (user.Collections.Any())
            {
                var userCollections = userCollectionAccessDetails.Where(x => user.Collections.Any(y => x.CollectionId == y.Id));
                userAccessDetails.AddRange(userCollections);
            }
            report.AccessDetails = userAccessDetails;

            var userCiphers =
                report.AccessDetails
                    .Where(x => x.ItemCount > 0)
                    .SelectMany(y => y.CollectionCipherIds)
                    .Distinct();
            report.CipherIds = userCiphers;
            report.TotalItemCount = userCiphers.Count();

            // Distinct items only            
            var distinctItems = report.AccessDetails.Where(x => x.CollectionId.HasValue).Select(x => x.CollectionId).Distinct();
            report.CollectionsCount = distinctItems.Count();
            report.GroupsCount = report.AccessDetails.Select(x => x.GroupId).Where(y => y.HasValue).Distinct().Count();
            memberAccessCipherDetails.Add(report);
        }
        return memberAccessCipherDetails;
    }
}
