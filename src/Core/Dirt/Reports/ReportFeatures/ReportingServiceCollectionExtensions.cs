using Bit.Core.Dirt.Reports.ReportFeatures.Interfaces;
using Bit.Core.Dirt.Reports.ReportFeatures.OrganizationReportMembers.Interfaces;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Core.Dirt.Reports.ReportFeatures;

public static class ReportingServiceCollectionExtensions
{
    public static void AddReportingServices(this IServiceCollection services, IGlobalSettings globalSettings)
    {
        services.AddExtendedCache(OrganizationReportCacheConstants.CacheName, (GlobalSettings)globalSettings);

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
        services.AddScoped<IGetOrganizationReportApplicationDataQuery, GetOrganizationReportApplicationDataQuery>();
        services.AddScoped<IUpdateOrganizationReportApplicationDataCommand, UpdateOrganizationReportApplicationDataCommand>();

        // v2 file storage commands
        services.AddScoped<ICreateOrganizationReportCommand, CreateOrganizationReportCommand>();
        services.AddScoped<IUpdateOrganizationReportV2Command, UpdateOrganizationReportV2Command>();
        services.AddScoped<IValidateOrganizationReportFileCommand, ValidateOrganizationReportFileCommand>();
    }
}
