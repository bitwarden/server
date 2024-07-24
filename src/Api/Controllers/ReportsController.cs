using Api.Models.Response.Organizations;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Context;
using Bit.Core.Repositories;
using Bit.Core.Vault.Queries;
using Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Requests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

public class CipherCounts
{
    public Guid CollectionId { get; set; }
    public int ItemCount { get; set; }
}

[Route("reports")]
[Authorize("Application")]
public class ReportsController : Controller
{
    private readonly IOrganizationUserUserDetailsQuery _organizationUserUserDetailsQuery;
    private readonly IGroupRepository _groupRepository;
    private readonly ICollectionRepository _collectionRepository;
    private readonly ICurrentContext _currentContext;
    private readonly IOrganizationCiphersQuery _organizationCiphersQuery;
    private readonly IOrganizationUserRepository _organizationUserRepository;

    public ReportsController(
        IOrganizationUserUserDetailsQuery organizationUserUserDetailsQuery,
        IGroupRepository groupRepository,
        ICollectionRepository collectionRepository,
        ICurrentContext currentContext,
        IOrganizationCiphersQuery organizationCiphersQuery,
        IOrganizationUserRepository organizationUserRepository
    )
    {
        _organizationUserUserDetailsQuery = organizationUserUserDetailsQuery;
        _groupRepository = groupRepository;
        _collectionRepository = collectionRepository;
        _currentContext = currentContext;
        _organizationCiphersQuery = organizationCiphersQuery;
        _organizationUserRepository = organizationUserRepository;
    }

    [HttpGet("member-access/{orgId}")]
    public async Task<IEnumerable<MemberAccessReportModel>> GetMemberAccessReport(Guid orgId)
    {
        var orgUsers = await _organizationUserUserDetailsQuery.GetOrganizationUserUserDetails(
            new OrganizationUserUserDetailsQueryRequest
            {
                OrganizationId = orgId,
                IncludeCollections = true,
                IncludeGroups = true
            });

        var orgGroups = await _groupRepository.GetManyWithCollectionsByOrganizationIdAsync(orgId);
        var orgCollectionsWithAccess = await _collectionRepository.GetManyByOrganizationIdWithAccessAsync(orgId);
        var orgCollectionsWithPermissions = await _collectionRepository.GetManyByOrganizationIdWithPermissionsAsync(orgId, _currentContext.UserId.Value, true);
        var baseCollections = await _collectionRepository.GetManyByOrganizationIdAsync(orgId);
        var orgItems = await _organizationCiphersQuery.GetAllOrganizationCiphers(orgId);

        // Get recovery status for all org users. NOTE: Use the OrganizationUserId (id) not the UserId
        var userIds = orgUsers.Select(y => y.OrganizationUserUserDetails.Id);
        var usersRecoveryStatus = await _organizationUserRepository.GetManyAccountRecoveryDetailsByOrganizationUserAsync(orgId, userIds);

        // Get cipher counts 
        var cipherCounts = orgItems.SelectMany(x => x.CollectionIds).GroupBy(y => y).Select(n => new CipherCounts { CollectionId = n.Key, ItemCount = n.Count() });

        var memberAccess = new List<MemberAccessReportModel>();
        foreach (var user in orgUsers)
        {
            var report = new MemberAccessReportModel
            {
                UserName = user.OrganizationUserUserDetails.Name,
                Email = user.OrganizationUserUserDetails.Email,
                TwoFactorEnabled = user.TwoFactorEnabled
            };

            var userAccountRecovery = usersRecoveryStatus.Where(x => x.OrganizationUserId == user.OrganizationUserUserDetails.Id);
            // If the EncryptedPrivateKey is populated account recovery is enabled
            report.AccountRecoveryEnabled = !string.IsNullOrEmpty(userAccountRecovery.FirstOrDefault().EncryptedPrivateKey);

            var groups = new List<MemberAccessGroupModel>();
            foreach (var group in user.OrganizationUserUserDetails.Groups)
            {
                var reportGroup = new MemberAccessGroupModel();
                var orgGroup = orgGroups.Select(x => x.Item1).Where(y => y.Id == group).FirstOrDefault();
                var grpObj = orgGroups.Where(x => x.Item1.Id == group).FirstOrDefault();
                reportGroup.Id = orgGroup.Id;
                reportGroup.Name = orgGroup.Name;

                var collections = new List<MemberAccessCollectionModel>();

                foreach (var orgCol in grpObj.Item2)
                {
                    var collection = orgCollectionsWithPermissions.Where(x => x.Id == orgCol.Id).FirstOrDefault();
                    var itemCount = cipherCounts.Where(x => x.CollectionId == collection.Id).Select(y => y.ItemCount).FirstOrDefault();
                    var reportCollection = new MemberAccessCollectionModel(
                        collection.Id,
                        collection.Name,
                        itemCount,
                        collection.ReadOnly,
                        collection.HidePasswords,
                        collection.Manage);
                    collections.Add(reportCollection);
                }
                reportGroup.Collections = collections;
                groups.Add(reportGroup);
            }
            report.Groups = groups;

            var directAssign = new List<MemberAccessCollectionModel>();
            foreach (var userCollection in user.OrganizationUserUserDetails.Collections)
            {
                var orgCollections = orgCollectionsWithPermissions.Where(x => x.Id == userCollection.Id).FirstOrDefault();
                var itemCount = cipherCounts.Where(x => x.CollectionId == userCollection.Id).Select(y => y.ItemCount).FirstOrDefault();
                var directAssignCollection = new MemberAccessCollectionModel(
                    orgCollections.Id,
                    orgCollections.Name,
                    itemCount,
                    orgCollections.ReadOnly,
                    orgCollections.HidePasswords,
                    orgCollections.Manage);
                directAssign.Add(directAssignCollection);
            }

            report.Collections = directAssign;

            // Group counts for summary
            report.GroupCount = report.Groups.Count();

            // Distinct assigned user collections and the collections assigned through a group
            var distinctUserCollections = report.Collections.Union(report.Groups.SelectMany(y => y.Collections)).GroupBy(y => y.Id).Select(g => g.FirstOrDefault());
            report.CollectionsCount = distinctUserCollections.Count();

            // Total item counts from the distinct user collections to avoid duplicate counting
            var distinctCollectionIds = distinctUserCollections.Select(x => x.Id);
            var userOrgItems = orgItems.Where(x => distinctCollectionIds.Any(y => x.CollectionIds.Contains(y)));
            report.TotalItemCount = userOrgItems.Count();

            memberAccess.Add(report);
        }

        return memberAccess;
    }
}
