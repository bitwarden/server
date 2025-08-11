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
        services.AddScoped<IDropOrganizationReportCommand, DropOrganizationReportCommand>();
        services.AddScoped<IGetOrganizationReportQuery, GetOrganizationReportQuery>();
        services.AddScoped<IUpdateOrganizationReportCommand, UpdateOrganizationReportCommand>();
    }
}
