using Bit.Core;
using Bit.Core.Jobs;
using Bit.Core.Repositories;
using Quartz;

namespace Bit.Admin.Jobs;

public class DeleteAuthRequestsJob : BaseJob
{
    private readonly IAuthRequestRepository _authRepo;

    public DeleteAuthRequestsJob(
        IAuthRequestRepository authrepo,
        ILogger<DeleteAuthRequestsJob> logger)
        : base(logger)
    {
        _authRepo = authrepo;
    }

    protected async override Task ExecuteJobAsync(IJobExecutionContext context)
    {
        _logger.LogInformation(Constants.BypassFiltersEventId, "Execute job task: DeleteAuthRequestsJob: Start");
        var count = await _authRepo.DeleteExpiredAsync();
        _logger.LogInformation(Constants.BypassFiltersEventId, $"{count} records deleted from AuthRequests.");
        _logger.LogInformation(Constants.BypassFiltersEventId, "Execute job task: DeleteAuthRequestsJob: End");
    }
}
