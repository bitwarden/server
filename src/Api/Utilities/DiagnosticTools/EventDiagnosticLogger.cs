using Bit.Api.Models.Public.Request;
using Bit.Api.Models.Public.Response;

namespace Bit.Api.Utilities.DiagnosticTools;

public static class EventDiagnosticLogger
{

    public static void LogAggregateData(this ILogger logger, Guid organizationId, PagedListResponseModel<EventResponseModel> data, EventFilterRequestModel request)
    {
        var recordCount = data.Data.Count();
        var oldestRecordDate = data.Data.FirstOrDefault()?.Date;
        var newestRecordDate = data.Data.LastOrDefault()?.Date;
        var hasMore = !string.IsNullOrEmpty(data.ContinuationToken);

        try
        {
            logger.LogInformation(
                "Events query for Organization {OrgId}. Returned {Count} events, oldest record {oldestRecord}, newest record {newestRecord} HasMore: {HasMore}, " +
                "Request Filters: Start={Start}, End={End}, ActingUserId={ActingUserId}, ItemId={ItemId},",
                organizationId,
                recordCount,
                oldestRecordDate,
                newestRecordDate,
                hasMore,
                request.Start,
                request.End,
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
        Guid organizationId,
        string continuationToken,
        IEnumerable<Bit.Api.Models.Response.EventResponseModel> data,
        DateTime? queryStart = null,
        DateTime? queryEnd = null)
    {
        var list = data.ToList();
        var recordCount = list.Count;
        var oldestRecordDate = list.FirstOrDefault()?.Date;
        var newestRecordDate = list.LastOrDefault()?.Date;
        var hasMore = !string.IsNullOrEmpty(continuationToken);

        try
        {
            logger.LogInformation(
                "Events query for Organization {OrgId}. Returned {Count} events, oldest record {oldestRecord}, newest record {newestRecord} HasMore: {HasMore}, " +
                "Request Filters: Start={queryStart}, End={End}",
                organizationId,
                recordCount,
                oldestRecordDate,
                newestRecordDate,
                hasMore,
                queryStart,
                queryEnd);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Unexpected exception from EventDiagnosticLogger.LogAggregateData");
        }
    }
}
