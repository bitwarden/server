using Bit.Api.Tools.Models.Response;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Context;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Vault.Queries;
using Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Requests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Tools.Controllers;

[Route("reports")]
[Authorize("Application")]
public class ReportsController : Controller
{
    private readonly IOrganizationUserUserDetailsQuery _organizationUserUserDetailsQuery;
    private readonly IGroupRepository _groupRepository;
    private readonly ICollectionRepository _collectionRepository;
    private readonly ICurrentContext _currentContext;
    private readonly IOrganizationCiphersQuery _organizationCiphersQuery;
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
        _applicationCacheService = applicationCacheService;
    }

    [HttpGet("member-access/{orgId}")]
    public async Task<IEnumerable<MemberAccessReportModel>> GetMemberAccessReport(Guid orgId)
    {
        if (!await _currentContext.AccessReports(orgId))
        {
            throw new NotFoundException();
        }

        var orgUsers = await _organizationUserUserDetailsQuery.GetOrganizationUserUserDetails(
            new OrganizationUserUserDetailsQueryRequest
            {
                OrganizationId = orgId,
                IncludeCollections = true,
                IncludeGroups = true
            });

        // _collectionRepository.GetManyByOrganizationIdWithAccessAsync contains only the group id. 
        // Create a dictionary to lookup the group names later. 
        var orgGroups = await _groupRepository.GetManyByOrganizationIdAsync(orgId);
        var groupNameDictionary = orgGroups.ToDictionary(x => x.Id, x => x.Name);

        // Contains the collections for the organization and the groups/users and permissions
        var orgCollectionsWithAccess = await _collectionRepository.GetManyByOrganizationIdWithAccessAsync(orgId);
        var orgItems = await _organizationCiphersQuery.GetAllOrganizationCiphers(orgId);

        // Take the collections/groups and create the access details items
        var accessDetails = new List<MemberAccessReportAccessDetails>();
        foreach (var tCollect in orgCollectionsWithAccess)
        {
            // All collections assigned to groups and their permissions 
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
                var userCollections = accessDetails.Where(x => user.OrganizationUserUserDetails.Collections.Any(y => x.CollectionId == y.Id && x.UserGuid == user.OrganizationUserUserDetails.Id));
                userAccessDetails.AddRange(userCollections);
            }
            report.AccessDetails = userAccessDetails;

            // Distinct items only
            report.TotalItemCount = report.AccessDetails.SelectMany(x => x.CipherIds).Distinct().Count();

            report.CollectionsCount = report.AccessDetails.Select(x => x.CollectionId).Distinct().Count();
            report.GroupsCount = report.AccessDetails.Select(x => x.GroupId).Where(y => y.HasValue).Distinct().Count();
            memberAccessReport.Add(report);
        }
        return memberAccessReport;
    }
}
