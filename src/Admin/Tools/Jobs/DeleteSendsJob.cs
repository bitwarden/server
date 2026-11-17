using Bit.Admin.Auth.Jobs;
using Bit.Core;
using Bit.Core.Jobs;
using Bit.Core.Tools.Repositories;
using Bit.Core.Tools.SendFeatures.Commands.Interfaces;
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

        // Item Sends are kept while their vault item exists, so ones whose item was deleted are removed here.
        var itemSendsWithDeletedCipher = await _sendRepository.GetManyItemSendsWithDeletedCipherAsync();
        _logger.LogInformation(Constants.BypassFiltersEventId, "Deleting {0} item sends whose item was deleted.",
            itemSendsWithDeletedCipher.Count);

        var sendsToDelete = sends.Concat(itemSendsWithDeletedCipher).DistinctBy(s => s.Id).ToList();
        if (sendsToDelete.Count == 0)
        {
            return;
        }
        using (var scope = _serviceProvider.CreateScope())
        {
            var nonAnonymousSendCommand = scope.ServiceProvider.GetRequiredService<INonAnonymousSendCommand>();
            foreach (var send in sendsToDelete)
            {
                await nonAnonymousSendCommand.DeleteSendAsync(send);
            }
        }
    }
}
