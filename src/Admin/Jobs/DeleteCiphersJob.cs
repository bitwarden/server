using Bit.Core;
using Bit.Core.Jobs;
using Bit.Core.Repositories;
using Microsoft.Extensions.Options;
using Quartz;

namespace Bit.Admin.Jobs;

public class DeleteCiphersJob : BaseJob
{
    private readonly ICipherRepository _cipherRepository;
    private readonly AdminSettings _adminSettings;

    public DeleteCiphersJob(
        ICipherRepository cipherRepository,
        IOptions<AdminSettings> adminSettings,
        ILogger<DeleteCiphersJob> logger)
        : base(logger)
    {
        _cipherRepository = cipherRepository;
        _adminSettings = adminSettings?.Value;
    }

    protected async override Task ExecuteJobAsync(IJobExecutionContext context)
    {
        _logger.LogInformation(Constants.BypassFiltersEventId, "Execute job task: DeleteDeletedAsync");
        var deleteDate = DateTime.UtcNow.AddDays(-30);
        var daysAgoSetting = (_adminSettings?.DeleteTrashDaysAgo).GetValueOrDefault();
        if (daysAgoSetting > 0)
        {
            deleteDate = DateTime.UtcNow.AddDays(-1 * daysAgoSetting);
        }
        await _cipherRepository.DeleteDeletedAsync(deleteDate);
        _logger.LogInformation(Constants.BypassFiltersEventId, "Finished job task: DeleteDeletedAsync");
    }
}
