using Bit.Core.Billing.Premium.Queries;
using Bit.Core.Dirt.Reports.ReportFeatures.Interfaces;
using Bit.Core.Dirt.Repositories;
using Bit.Core.Exceptions;
using Bit.Core.Platform.Push;

namespace Bit.Core.Dirt.Reports.ReportFeatures;

public class ExcludeCipherFromBrowserHealthCommand : IExcludeCipherFromBrowserHealthCommand
{
    private readonly ICipherBrowserHealthExclusionRepository _cipherBrowserHealthExclusionRepository;
    private readonly IHasPremiumAccessQuery _hasPremiumAccessQuery;
    private readonly IPushNotificationService _pushService;

    public ExcludeCipherFromBrowserHealthCommand(
        ICipherBrowserHealthExclusionRepository cipherBrowserHealthExclusionRepository,
        IHasPremiumAccessQuery hasPremiumAccessQuery,
        IPushNotificationService pushService)
    {
        _cipherBrowserHealthExclusionRepository = cipherBrowserHealthExclusionRepository;
        _hasPremiumAccessQuery = hasPremiumAccessQuery;
        _pushService = pushService;
    }

    public async Task ExcludeAsync(Guid cipherId, Guid userId)
    {
        if (!await _hasPremiumAccessQuery.HasPremiumAccessAsync(userId))
        {
            throw new BadRequestException("Premium required to exclude items from browser health.");
        }

        var excluded = await _cipherBrowserHealthExclusionRepository.ExcludeAsync([cipherId], userId);
        if (excluded == 0)
        {
            throw new NotFoundException();
        }

        await _pushService.PushSyncCiphersAsync(userId, true);
    }
}
