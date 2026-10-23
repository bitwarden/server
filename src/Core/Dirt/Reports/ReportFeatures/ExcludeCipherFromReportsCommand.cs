using Bit.Core.Billing.Premium.Queries;
using Bit.Core.Dirt.Reports.ReportFeatures.Interfaces;
using Bit.Core.Dirt.Repositories;
using Bit.Core.Exceptions;
using Bit.Core.Platform.Push;

namespace Bit.Core.Dirt.Reports.ReportFeatures;

public class ExcludeCipherFromReportsCommand : IExcludeCipherFromReportsCommand
{
    private readonly ICipherReportExclusionRepository _cipherReportExclusionRepository;
    private readonly IHasPremiumAccessQuery _hasPremiumAccessQuery;
    private readonly IPushNotificationService _pushService;

    public ExcludeCipherFromReportsCommand(
        ICipherReportExclusionRepository cipherReportExclusionRepository,
        IHasPremiumAccessQuery hasPremiumAccessQuery,
        IPushNotificationService pushService)
    {
        _cipherReportExclusionRepository = cipherReportExclusionRepository;
        _hasPremiumAccessQuery = hasPremiumAccessQuery;
        _pushService = pushService;
    }

    public async Task ExcludeAsync(Guid cipherId, Guid userId)
    {
        if (!await _hasPremiumAccessQuery.HasPremiumAccessAsync(userId))
        {
            throw new BadRequestException("Premium required to exclude items from reports.");
        }

        var excluded = await _cipherReportExclusionRepository.ExcludeAsync([cipherId], userId);
        if (excluded == 0)
        {
            throw new NotFoundException();
        }

        await _pushService.PushSyncCiphersAsync(userId, true);
    }
}
