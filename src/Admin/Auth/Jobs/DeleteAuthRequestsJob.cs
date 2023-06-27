using Bit.Core;
using Bit.Core.Jobs;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using Quartz;

namespace Bit.Admin.Auth.Jobs;

public class DeleteAuthRequestsJob : BaseJob
{
    private readonly IAuthRequestRepository _authRepo;
    private readonly IGlobalSettings _globalSettings;

    public DeleteAuthRequestsJob(
        IAuthRequestRepository authrepo,
        IGlobalSettings globalSettings,
        ILogger<DeleteAuthRequestsJob> logger)
        : base(logger)
    {
        _authRepo = authrepo;
        _globalSettings = globalSettings;
    }

    protected async override Task ExecuteJobAsync(IJobExecutionContext context)
    {
        _logger.LogInformation(Constants.BypassFiltersEventId, "Execute job task: DeleteAuthRequestsJob: Start");
        // TODO: Replace with global settings
        var count = await _authRepo.DeleteExpiredAsync(
            _globalSettings.PasswordlessAuth.UserRequestExpiration,
            _globalSettings.PasswordlessAuth.AdminRequestExpiration,
            _globalSettings.PasswordlessAuth.AfterAdminApprovalExpiration);
        _logger.LogInformation(Constants.BypassFiltersEventId, "{Count} records deleted from AuthRequests.", count);
        _logger.LogInformation(Constants.BypassFiltersEventId, "Execute job task: DeleteAuthRequestsJob: End");
    }
}
