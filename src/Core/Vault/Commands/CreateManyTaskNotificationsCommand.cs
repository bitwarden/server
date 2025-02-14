using Bit.Core.NotificationCenter.Commands.Interfaces;
using Bit.Core.NotificationCenter.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Vault.Commands.Interfaces;
using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Models.Data;
using Bit.Core.Vault.Queries;
using Bit.Core.NotificationCenter.Enums;

public class CreateManyTaskNotificationsCommand : ICreateManyTaskNotificationsCommand
{
    private readonly IGetSecurityTasksNotificationDetailsQuery _getSecurityTasksNotificationDetailsQuery;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IMailService _mailService;
    private readonly ICreateNotificationCommand _createNotificationCommand;

    public CreateManyTaskNotificationsCommand(
        IGetSecurityTasksNotificationDetailsQuery getSecurityTasksNotificationDetailsQuery,
        IOrganizationRepository organizationRepository,
        IMailService mailService,
        ICreateNotificationCommand createNotificationCommand)
    {
        _getSecurityTasksNotificationDetailsQuery = getSecurityTasksNotificationDetailsQuery;
        _organizationRepository = organizationRepository;
        _mailService = mailService;
        _createNotificationCommand = createNotificationCommand;
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

        await _mailService.SendBulkSecurityTaskNotificationsAsync(organization.Name, userTaskCount);

        foreach (var userSecurityTaskCipher in securityTaskCiphers)
        {
            // Create a notification for the user with the associated task
            var notification = new Notification
            {
                UserId = userSecurityTaskCipher.UserId,
                OrganizationId = orgId,
                Priority = Priority.Informational,
                ClientType = ClientType.Browser,
                TaskId = userSecurityTaskCipher.TaskId
            };
            await _createNotificationCommand.CreateAsync(notification);
        }
    }
}
