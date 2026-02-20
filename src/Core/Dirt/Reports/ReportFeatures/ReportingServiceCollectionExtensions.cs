using Bit.Core.Dirt.Reports.ReportFeatures.Interfaces;
using Bit.Core.Dirt.Reports.ReportFeatures.OrganizationReportMembers.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Core.Dirt.Reports.ReportFeatures;

public static class ReportingServiceCollectionExtensions
{
    public static void AddReportingServices(this IServiceCollection services)
    {
        services.AddScoped<IRiskInsightsReportQuery, RiskInsightsReportQuery>();
        services.AddScoped<IMemberAccessReportQuery, MemberAccessReportQuery>();
        services.AddScoped<IAddPasswordHealthReportApplicationCommand, AddPasswordHealthReportApplicationCommand>();
        services.AddScoped<IGetPasswordHealthReportApplicationQuery, GetPasswordHealthReportApplicationQuery>();
        services.AddScoped<IDropPasswordHealthReportApplicationCommand, DropPasswordHealthReportApplicationCommand>();
        services.AddScoped<IAddOrganizationReportCommand, AddOrganizationReportCommand>();
        services.AddScoped<IGetOrganizationReportQuery, GetOrganizationReportQuery>();
        services.AddScoped<IUpdateOrganizationReportCommand, UpdateOrganizationReportCommand>();
        services.AddScoped<IUpdateOrganizationReportSummaryCommand, UpdateOrganizationReportSummaryCommand>();
        services.AddScoped<IGetOrganizationReportSummaryDataQuery, GetOrganizationReportSummaryDataQuery>();
        services.AddScoped<IGetOrganizationReportSummaryDataByDateRangeQuery, GetOrganizationReportSummaryDataByDateRangeQuery>();
        services.AddScoped<IGetOrganizationReportDataQuery, GetOrganizationReportDataQuery>();
        services.AddScoped<IUpdateOrganizationReportDataCommand, UpdateOrganizationReportDataCommand>();
        services.AddScoped<IGetOrganizationReportApplicationDataQuery, GetOrganizationReportApplicationDataQuery>();
        services.AddScoped<IUpdateOrganizationReportApplicationDataCommand, UpdateOrganizationReportApplicationDataCommand>();

        // v2 file storage commands
        services.AddScoped<ICreateOrganizationReportStorageCommand, CreateOrganizationReportStorageCommand>();
        services.AddScoped<IUpdateOrganizationReportDataFileStorageCommand, UpdateOrganizationReportDataFileStorageCommand>();

        // v2 file storage queries
        services.AddScoped<IGetOrganizationReportDataFileStorageQuery, GetOrganizationReportDataFileStorageQuery>();

        // v2 application data
        services.AddScoped<IGetOrganizationReportApplicationDataV2Query, GetOrganizationReportApplicationDataV2Query>();
        services.AddScoped<IUpdateOrganizationReportApplicationDataV2Command, UpdateOrganizationReportApplicationDataV2Command>();

        // v2 summary data
        services.AddScoped<IGetOrganizationReportSummaryDataByDateRangeV2Query, GetOrganizationReportSummaryDataByDateRangeV2Query>();
        services.AddScoped<IGetOrganizationReportSummaryDataV2Query, GetOrganizationReportSummaryDataV2Query>();
        services.AddScoped<IUpdateOrganizationReportSummaryV2Command, UpdateOrganizationReportSummaryV2Command>();
    }
}
