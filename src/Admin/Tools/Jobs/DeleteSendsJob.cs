using Bit.Admin.Auth.Jobs;
using Bit.Core;
using Bit.Core.Jobs;
using Bit.Core.Tools.Repositories;
using Bit.Core.Tools.Services;
using Quartz;

namespace Bit.Admin.Tools.Jobs;

public class DeleteSendsJob : BaseJob
{
    private readonly ISendRepository _sendRepository;
    private readonly IServiceProvider _serviceProvider;

    public DeleteSendsJob(
        ISendRepository sendRepository,
        IServiceProvider serviceProvider,
        ILogger<DatabaseExpiredGrantsJob> logger)
        : base(logger)
    {
        _sendRepository = sendRepository;
        _serviceProvider = serviceProvider;
    }

    protected async override Task ExecuteJobAsync(IJobExecutionContext context)
    {
        var sends = await _sendRepository.GetManyByDeletionDateAsync(DateTime.UtcNow);
        _logger.LogInformation(Constants.BypassFiltersEventId, "Deleting {0} sends.", sends.Count);
        if (!sends.Any())
        {
            return;
        }
        using (var scope = _serviceProvider.CreateScope())
        {
            var sendService = scope.ServiceProvider.GetRequiredService<ISendService>();
            foreach (var send in sends)
            {
                await sendService.DeleteSendAsync(send);
            }
        }
    }
}
