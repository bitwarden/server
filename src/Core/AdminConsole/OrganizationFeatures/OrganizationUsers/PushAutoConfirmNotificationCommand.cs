using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.Enums;
using Bit.Core.Models;
using Bit.Core.Platform.Push;
using Bit.Core.Repositories;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers;

public class PushAutoConfirmNotificationCommand : IPushAutoConfirmNotificationCommand
{
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IPushNotificationService _pushNotificationService;

    public PushAutoConfirmNotificationCommand(
        IOrganizationUserRepository organizationUserRepository,
        IPushNotificationService pushNotificationService)
    {
        _organizationUserRepository = organizationUserRepository;
        _pushNotificationService = pushNotificationService;
    }

    public async Task PushAsync(Guid userId, Guid organizationId)
    {
        var organizationUser = await _organizationUserRepository.GetByOrganizationAsync(organizationId, userId);
        if (organizationUser == null)
        {
            throw new Exception("Organization user not found");
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
                        TargetUserId = organizationUser.Id
                    },
                    ExcludeCurrentContext = false,
                });
        }
    }
}
