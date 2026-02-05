// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Core.Enums;
using Bit.Core.NotificationCenter.Commands.Interfaces;
using Bit.Core.NotificationCenter.Entities;
using Bit.Core.NotificationCenter.Enums;
using Bit.Core.Platform.Push;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Vault.Commands.Interfaces;
using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Models.Data;
using Bit.Core.Vault.Queries;

public class CreateManyTaskNotificationsCommand : ICreateManyTaskNotificationsCommand
{
    private readonly IGetSecurityTasksNotificationDetailsQuery _getSecurityTasksNotificationDetailsQuery;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IMailService _mailService;
    private readonly ICreateNotificationCommand _createNotificationCommand;
    private readonly IPushNotificationService _pushNotificationService;
    private readonly IOrganizationUserRepository _organizationUserRepository;

    public CreateManyTaskNotificationsCommand(
        IGetSecurityTasksNotificationDetailsQuery getSecurityTasksNotificationDetailsQuery,
        IOrganizationRepository organizationRepository,
        IMailService mailService,
        ICreateNotificationCommand createNotificationCommand,
        IPushNotificationService pushNotificationService,
        IOrganizationUserRepository organizationUserRepository)
    {
        _getSecurityTasksNotificationDetailsQuery = getSecurityTasksNotificationDetailsQuery;
        _organizationRepository = organizationRepository;
        _mailService = mailService;
        _createNotificationCommand = createNotificationCommand;
        _pushNotificationService = pushNotificationService;
        _organizationUserRepository = organizationUserRepository;
    }

    public async Task CreateAsync(Guid orgId, IEnumerable<SecurityTask> securityTasks)
    {
        var securityTaskCiphers = await _getSecurityTasksNotificationDetailsQuery.GetNotificationDetailsByManyIds(orgId, securityTasks);

        // Get the number of tasks for each user
        var userTaskCount = securityTaskCiphers.GroupBy(x => x.UserId).Select(x => new UserSecurityTasksCount
        {
            UserId = x.Key,
            Email = x.First().Email,
            TaskCount = x.Count()
        }).ToList();

        var organization = await _organizationRepository.GetByIdAsync(orgId);
        var orgAdminEmails = (await _organizationUserRepository.GetManyDetailsByRoleAsync(orgId, OrganizationUserType.Admin))
            .Select(u => u.Email)
            .ToList();

        var orgOwnerEmails = (await _organizationUserRepository.GetManyDetailsByRoleAsync(orgId, OrganizationUserType.Owner))
            .Select(u => u.Email)
            .ToList();

        // Ensure proper deserialization of emails
        var orgAdminAndOwnerEmails = orgAdminEmails.Concat(orgOwnerEmails).Distinct().ToList();

        await _mailService.SendBulkSecurityTaskNotificationsAsync(organization, userTaskCount, orgAdminAndOwnerEmails);

        // Break securityTaskCiphers into separate lists by user Id
        var securityTaskCiphersByUser = securityTaskCiphers.GroupBy(x => x.UserId)
                                   .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var userId in securityTaskCiphersByUser.Keys)
        {
            // Get the security tasks by the user Id
            var userSecurityTaskCiphers = securityTaskCiphersByUser[userId];

            // Process each user's security task ciphers
            for (int i = 0; i < userSecurityTaskCiphers.Count; i++)
            {
                var userSecurityTaskCipher = userSecurityTaskCiphers[i];

                // Create a notification for the user with the associated task
                var notification = new Notification
                {
                    UserId = userSecurityTaskCipher.UserId,
                    OrganizationId = orgId,
                    Priority = Priority.Informational,
                    ClientType = ClientType.Browser,
                    TaskId = userSecurityTaskCipher.TaskId
                };

                await _createNotificationCommand.CreateAsync(notification, false);
            }

            // Notify the user that they have pending security tasks
            await _pushNotificationService.PushRefreshSecurityTasksAsync(userId);
        }
    }
}
