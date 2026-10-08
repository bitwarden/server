using Bit.AgentFill.Commands;
using Bit.Core;
using Bit.Core.Jobs;
using Quartz;

namespace Bit.Admin.AgentFill.Jobs;

public class DeleteExpiredAgentFillApprovalRequestsJob : BaseJob
{
    private readonly IDeleteExpiredAgentFillApprovalRequestsCommand _deleteExpiredCommand;

    public DeleteExpiredAgentFillApprovalRequestsJob(
        IDeleteExpiredAgentFillApprovalRequestsCommand deleteExpiredCommand,
        ILogger<DeleteExpiredAgentFillApprovalRequestsJob> logger)
        : base(logger)
    {
        _deleteExpiredCommand = deleteExpiredCommand;
    }

    protected override async Task ExecuteJobAsync(IJobExecutionContext context)
    {
        _logger.LogInformation(Constants.BypassFiltersEventId,
            "Execute job task: DeleteExpiredAgentFillApprovalRequestsJob: Start");
        var count = await _deleteExpiredCommand.RunAsync();
        _logger.LogInformation(Constants.BypassFiltersEventId,
            "{Count} records deleted from AgentFillApprovalRequest.", count);
        _logger.LogInformation(Constants.BypassFiltersEventId,
            "Execute job task: DeleteExpiredAgentFillApprovalRequestsJob: End");
    }
}
