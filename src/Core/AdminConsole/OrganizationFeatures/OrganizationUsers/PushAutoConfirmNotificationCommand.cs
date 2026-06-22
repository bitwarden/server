using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.Enums;
using Bit.Core.Models;
using Bit.Core.Platform.Push;
using Bit.Core.Repositories;
using Bit.Core.Services;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers;

public class PushAutoConfirmNotificationCommand : IPushAutoConfirmNotificationCommand
{
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IPushNotificationService _pushNotificationService;
    private readonly IApplicationCacheService _applicationCacheService;
    private readonly IPolicyQuery _policyQuery;

    public PushAutoConfirmNotificationCommand(
        IOrganizationUserRepository organizationUserRepository,
        IPushNotificationService pushNotificationService,
        IApplicationCacheService applicationCacheService,
        IPolicyQuery policyQuery)
    {
        _organizationUserRepository = organizationUserRepository;
        _pushNotificationService = pushNotificationService;
        _applicationCacheService = applicationCacheService;
        _policyQuery = policyQuery;
    }

    public async Task PushAsync(Guid userId, Guid organizationId)
    {
        var orgAbility = await _applicationCacheService.GetOrganizationAbilityAsync(organizationId);
        if (orgAbility is not { UseAutomaticUserConfirmation: true })
        {
            return;
        }

        var policy = await _policyQuery.RunAsync(organizationId, PolicyType.AutomaticUserConfirmation);
        if (!policy.Enabled)
        {
            return;
        }

        var organizationUser = await _organizationUserRepository.GetByOrganizationAsync(organizationId, userId);
        if (organizationUser == null)
        {
            throw new Exception("Organization user not found");
        }

        if (organizationUser.Type != OrganizationUserType.User)
        {
            return;
        }

        var admins = await _organizationUserRepository.GetManyByMinimumRoleAsync(
            organizationId,
            OrganizationUserType.Admin);

        var customUsersWithManagePermission = (await _organizationUserRepository.GetManyDetailsByRoleAsync(
                organizationId,
                OrganizationUserType.Custom))
            .Where(c => c.GetPermissions()?.ManageUsers == true)
            .Select(c => c.UserId);

        var userIds = admins
            .Select(a => a.UserId)
            .Concat(customUsersWithManagePermission)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct();

        foreach (var adminUserId in userIds)
        {
            await _pushNotificationService.PushAsync(
                new PushNotification<AutoConfirmPushNotification>
                {
                    Target = NotificationTarget.User,
                    TargetId = adminUserId,
                    Type = PushType.AutoConfirm,
                    Payload = new AutoConfirmPushNotification
                    {
                        UserId = adminUserId,
                        OrganizationId = organizationId,
                        TargetUserId = userId,
                        TargetOrganizationUserId = organizationUser.Id
                    },
                    ExcludeCurrentContext = false,
                });
        }
    }
}
