// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Core;
using Bit.Core.Jobs;
using Bit.Core.Tools.Services;
using Bit.Core.Vault.Repositories;
using Microsoft.Extensions.Options;
using Quartz;

namespace Bit.Admin.Jobs;

public class DeleteCiphersJob : BaseJob
{
    private readonly ICipherRepository _cipherRepository;
    private readonly IItemSendCleanupService _itemSendCleanupService;
    private readonly AdminSettings _adminSettings;

    public DeleteCiphersJob(
        ICipherRepository cipherRepository,
        IItemSendCleanupService itemSendCleanupService,
        IOptions<AdminSettings> adminSettings,
        ILogger<DeleteCiphersJob> logger)
        : base(logger)
    {
        _cipherRepository = cipherRepository;
        _itemSendCleanupService = itemSendCleanupService;
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
        // Item Sends must go with the trashed ciphers they reference. The IDs are read before the purge,
        // so a cipher restored in the gap between the read and the delete loses its item Sends.
        var expiredCipherIds = await _cipherRepository.GetManyDeletedIdsBeforeAsync(deleteDate);
        await _itemSendCleanupService.DeleteItemSendsForCiphersAsync(expiredCipherIds);
        await _cipherRepository.DeleteDeletedAsync(deleteDate);
        _logger.LogInformation(Constants.BypassFiltersEventId, "Finished job task: DeleteDeletedAsync");
    }
}
