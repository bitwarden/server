using Bit.Core;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteReminders;
using Bit.Core.Jobs;
using Quartz;

namespace Bit.Admin.AdminConsole.Jobs;

/// <summary>
/// Sends due invite reminders. The command checks the feature flag and settings.
/// </summary>
public class SendInviteRemindersJob(
    ISendInviteRemindersCommand sendInviteRemindersCommand,
    ILogger<SendInviteRemindersJob> logger)
    : BaseJob(logger)
{
    protected override async Task ExecuteJobAsync(IJobExecutionContext context)
    {
        _logger.LogInformation(Constants.BypassFiltersEventId, "Execute job task: SendInviteRemindersJob: Start");
        var result = await sendInviteRemindersCommand.SendInviteRemindersAsync();
        _logger.LogInformation(Constants.BypassFiltersEventId,
            "Invite reminder job finished: {Claimed} claimed, {Sent} sent, {Failed} failed, sent by reminder number {SentByReminderNumber}",
            result.Claimed, result.Sent, result.Failed, result.SentByReminderNumber);
        _logger.LogInformation(Constants.BypassFiltersEventId, "Execute job task: SendInviteRemindersJob: End");
    }
}
