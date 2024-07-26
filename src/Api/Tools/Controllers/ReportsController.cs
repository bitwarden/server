using Bit.Api.Tools.Models.Response;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Context;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Vault.Queries;
using Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Requests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Tools.Controllers;

public class CipherCounts
{
    public Guid CollectionId { get; set; }
    public int ItemCount { get; set; }
}

public class ReportCollectionDetails
{
    public Guid CollectionId { get; set; }
    public Guid? UserId { get; set; }
    public Guid? GroupId { get; set; }
    public string GroupName { get; set; }
    public string CollectionName { get; set; }
    public bool ReadOnly { get; set; }
    public bool HidePasswords { get; set; }
    public bool Manage { get; set; }
    public IEnumerable<Guid> CipherIds { get; set; }
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
    private readonly IApplicationCacheService _applicationCacheService;

    public ReportsController(
        IOrganizationUserUserDetailsQuery organizationUserUserDetailsQuery,
        IGroupRepository groupRepository,
        ICollectionRepository collectionRepository,
        ICurrentContext currentContext,
        IOrganizationCiphersQuery organizationCiphersQuery,
        IOrganizationUserRepository organizationUserRepository,
        IApplicationCacheService applicationCacheService
    )
    {
        _organizationUserUserDetailsQuery = organizationUserUserDetailsQuery;
        _groupRepository = groupRepository;
        _collectionRepository = collectionRepository;
        _currentContext = currentContext;
        _organizationCiphersQuery = organizationCiphersQuery;
        _organizationUserRepository = organizationUserRepository;
        _applicationCacheService = applicationCacheService;
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

        var orgGroups = await _groupRepository.GetManyByOrganizationIdAsync(orgId);
        var groupNameDictionary = orgGroups.ToDictionary(x => x.Id, x => x.Name);
        var orgCollectionsWithAccess = await _collectionRepository.GetManyByOrganizationIdWithAccessAsync(orgId);
        var orgItems = await _organizationCiphersQuery.GetAllOrganizationCiphers(orgId);

        // Take the collections/groups and create the access details items
        var collectionAccessDetails = new List<MemberAccessReportAccessDetails>();
        var groupAccessDetails = new List<MemberAccessReportAccessDetails>();
        var accessDetails = new List<MemberAccessReportAccessDetails>();
        foreach (var tCollect in orgCollectionsWithAccess)
        {
            var collectionItems = orgItems.Where(x => x.CollectionIds.Contains(tCollect.Item1.Id)).Select(x => x.Id);
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
                        CipherIds = collectionItems.ToList(),
                        ItemCount = collectionItems.Count()
                    });
                accessDetails.AddRange(groupDetails);
            }

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
                        CipherIds = collectionItems.ToList(),
                        ItemCount = collectionItems.Count()
                    });
                accessDetails.AddRange(userCollectionDetails);
            }
        }

        // Loop through the org users and populate report and access data
        var orgAbility = await _applicationCacheService.GetOrganizationAbilityAsync(orgId);
        var memberAccessReport = new List<MemberAccessReportModel>();
        foreach (var user in orgUsers)
        {
            var report = new MemberAccessReportModel
            {
                UserName = user.OrganizationUserUserDetails.Name,
                Email = user.OrganizationUserUserDetails.Email,
                TwoFactorEnabled = user.TwoFactorEnabled,
                // Both the user's ResetPasswordKey must be set and the organization can UseResetPassword
                // var orgAbility = await _applicationCacheService.GetOrganizationAbilityAsync(orgId); 
                AccountRecoveryEnabled = !string.IsNullOrEmpty(user.OrganizationUserUserDetails.ResetPasswordKey) && orgAbility.UseResetPassword
            };

            var userAccessDetails = new List<MemberAccessReportAccessDetails>();
            if (user.OrganizationUserUserDetails.Groups.Any())
            {
                var userGroups = accessDetails.Where(x => user.OrganizationUserUserDetails.Groups.Contains(x.GroupId.GetValueOrDefault()));
                userAccessDetails.AddRange(userGroups);
            }

            if (user.OrganizationUserUserDetails.Collections.Any())
            {
                var userCollections = accessDetails.Where(x => user.OrganizationUserUserDetails.Collections.Any(y => x.CollectionId == y.Id));
                userAccessDetails.AddRange(userCollections);
            }
            report.AccessDetails = userAccessDetails;

            // Distinct items only
            report.TotalItemCount = report.AccessDetails.SelectMany(x => x.CipherIds).Distinct().Count();

            report.CollectionsCount = report.AccessDetails.Select(x => x.CollectionId).Distinct().Count();
            report.GroupCount = report.AccessDetails.Select(x => x.GroupId).Distinct().Count();
            memberAccessReport.Add(report);
        }
        return memberAccessReport;
    }
}
