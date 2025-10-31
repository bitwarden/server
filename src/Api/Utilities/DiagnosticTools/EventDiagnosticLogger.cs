using Bit.Api.Models.Public.Request;
using Bit.Api.Models.Public.Response;
using Bit.Core;
using Bit.Core.Services;

namespace Bit.Api.Utilities.DiagnosticTools;

public static class EventDiagnosticLogger
{
    public static void LogAggregateData(
        this ILogger logger,
        IFeatureService featureService,
        Guid organizationId,
        PagedListResponseModel<EventResponseModel> data, EventFilterRequestModel request)
    {
        try
        {
            if (!featureService.IsEnabled(FeatureFlagKeys.EventDiagnosticLogging))
            {
                return;
            }

            var orderedRecords = data.Data.OrderBy(e => e.Date).ToList();
            var recordCount = orderedRecords.Count;
            var newestRecordDate = orderedRecords.LastOrDefault()?.Date.ToString("o");
            var oldestRecordDate = orderedRecords.FirstOrDefault()?.Date.ToString("o"); ;
            var hasMore = !string.IsNullOrEmpty(data.ContinuationToken);

            logger.LogInformation(
                "Events query for Organization:{OrgId}. Event count:{Count} newest record:{newestRecord}  oldest record:{oldestRecord} HasMore:{HasMore} " +
                "Request Filters Start:{QueryStart} End:{QueryEnd} ActingUserId:{ActingUserId} ItemId:{ItemId},",
                organizationId,
                recordCount,
                newestRecordDate,
                oldestRecordDate,
                hasMore,
                request.Start?.ToString("o"),
                request.End?.ToString("o"),
                request.ActingUserId,
                request.ItemId);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Unexpected exception from EventDiagnosticLogger.LogAggregateData");
        }
    }

    public static void LogAggregateData(
        this ILogger logger,
        IFeatureService featureService,
        Guid organizationId,
        string continuationToken,
        IEnumerable<Bit.Api.Models.Response.EventResponseModel> data,
        DateTime? queryStart = null,
        DateTime? queryEnd = null)
    {

        try
        {
            if (!featureService.IsEnabled(FeatureFlagKeys.EventDiagnosticLogging))
            {
                return;
            }

            var orderedRecords = data.OrderBy(e => e.Date).ToList();
            var recordCount = orderedRecords.Count;
            var newestRecordDate = orderedRecords.LastOrDefault()?.Date.ToString("o");
            var oldestRecordDate = orderedRecords.FirstOrDefault()?.Date.ToString("o"); ;
            var hasMore = !string.IsNullOrEmpty(continuationToken);

            logger.LogInformation(
                "Events query for Organization:{OrgId}. Event count:{Count} newest record:{newestRecord}  oldest record:{oldestRecord} HasMore:{HasMore} " +
                "Request Filters Start:{QueryStart} End:{QueryEnd}",
                organizationId,
                recordCount,
                newestRecordDate,
                oldestRecordDate,
                hasMore,
                queryStart?.ToString("o"),
                queryEnd?.ToString("o"));
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Unexpected exception from EventDiagnosticLogger.LogAggregateData");
        }
    }
}
