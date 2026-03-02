using Bit.Core.Dirt.Models.Data;
using Bit.Core.Dirt.Reports.ReportFeatures.Interfaces;
using Bit.Core.Dirt.Repositories;
using Bit.Core.Exceptions;
using Bit.Core.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion;

namespace Bit.Core.Dirt.Reports.ReportFeatures;

public class GetOrganizationReportSummaryDataByDateRangeQuery : IGetOrganizationReportSummaryDataByDateRangeQuery
{
    private const int MaxRecordsForWidget = 6;
    private readonly IOrganizationReportRepository _organizationReportRepo;
    private readonly ILogger<GetOrganizationReportSummaryDataByDateRangeQuery> _logger;
    private readonly IFusionCache _cache;

    public GetOrganizationReportSummaryDataByDateRangeQuery(
        IOrganizationReportRepository organizationReportRepo,
        [FromKeyedServices(OrganizationReportCacheConstants.CacheName)] IFusionCache cache,
        ILogger<GetOrganizationReportSummaryDataByDateRangeQuery> logger)
    {
        _organizationReportRepo = organizationReportRepo;
        _cache = cache;
        _logger = logger;
    }

    public async Task<IEnumerable<OrganizationReportSummaryDataResponse>> GetOrganizationReportSummaryDataByDateRangeAsync(Guid organizationId, DateTime startDate, DateTime endDate)
    {
        try
        {
            _logger.LogInformation(Constants.BypassFiltersEventId, "Fetching organization report summary data by date range for organization {OrganizationId}, from {StartDate} to {EndDate}",
                organizationId, startDate, endDate);

            var (isValid, errorMessage) = ValidateRequest(organizationId, startDate, endDate);
            if (!isValid)
            {
                _logger.LogWarning(Constants.BypassFiltersEventId, "GetOrganizationReportSummaryDataByDateRangeAsync validation failed: {errorMessage}", errorMessage);
                throw new BadRequestException(errorMessage);
            }

            // update start and end date to include the entire day
            startDate = startDate.Date;
            endDate = endDate.Date.AddDays(1).AddTicks(-1);

            // cache key and tag
            var cacheKey = OrganizationReportCacheConstants.BuildCacheKeyForSummaryDataByDateRange(organizationId, startDate, endDate);
            var cacheTag = OrganizationReportCacheConstants.BuildCacheTagForOrganizationReports(organizationId);

            var summaryDataList = await _cache.GetOrSetAsync(
                key: cacheKey,
                factory: async _ =>
                    {
                        var data = await _organizationReportRepo.GetSummaryDataByDateRangeAsync(organizationId, startDate, endDate);
                        return GetMostRecentEntries(data);
                    },
                options: new FusionCacheEntryOptions(duration: OrganizationReportCacheConstants.DurationForSummaryData),
                tags: [cacheTag]
            );

            var resultList = summaryDataList?.ToList() ?? Enumerable.Empty<OrganizationReportSummaryDataResponse>().ToList();

            _logger.LogInformation(Constants.BypassFiltersEventId, "Fetched {Count} organization report summary data entries for organization {OrganizationId}, from {StartDate} to {EndDate}",
                resultList.Count, organizationId, startDate, endDate);

            return resultList;
        }
        catch (Exception ex) when (!(ex is BadRequestException))
        {
            _logger.LogError(ex, "Error fetching organization report summary data by date range for organization {OrganizationId}, from {StartDate} to {EndDate}",
                organizationId, startDate, endDate);
            throw;
        }
    }

    private static (bool IsValid, string errorMessage) ValidateRequest(Guid organizationId, DateTime startDate, DateTime endDate)
    {
        if (organizationId == Guid.Empty)
        {
            return (false, "OrganizationId is required");
        }

        if (startDate == default)
        {
            return (false, "StartDate is required");
        }

        if (endDate == default)
        {
            return (false, "EndDate is required");
        }

        if (startDate > endDate)
        {
            return (false, "StartDate must be earlier than or equal to EndDate");
        }

        return (true, string.Empty);
    }

    private static IEnumerable<OrganizationReportSummaryDataResponse> GetMostRecentEntries(IEnumerable<OrganizationReportSummaryDataResponse> data, int maxEntries = MaxRecordsForWidget)
    {
        if (data.Count() <= maxEntries)
        {
            return data;
        }

        // here we need to take 10 records, evenly spaced by RevisionDate, 
        // to cover the entire date range, 
        // and ensure we include the most recent record as well
        var sortedData = data.OrderByDescending(d => d.RevisionDate).ToList();
        var totalRecords = sortedData.Count;
        var interval = (double)(totalRecords - 1) / (maxEntries - 1); // -1 the most recent record will be included by default
        var result = new List<OrganizationReportSummaryDataResponse>();

        for (int i = 0; i < maxEntries - 1; i++)
        {
            result.Add(sortedData[(int)Math.Round(i * interval)]);
        }

        return result;
    }
}
