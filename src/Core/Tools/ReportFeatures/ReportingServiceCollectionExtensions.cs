using Bit.Core.Tools.ReportFeatures.Interfaces;
using Bit.Core.Tools.ReportFeatures.OrganizationReportMembers.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Core.Tools.ReportFeatures;

public static class ReportingServiceCollectionExtensions
{
    public static void AddReportingServices(this IServiceCollection services)
    {
        services.AddScoped<IMemberAccessCipherDetailsQuery, MemberAccessCipherDetailsQuery>();
        services.AddScoped<IAddPasswordHealthReportApplicationCommand, AddPasswordHealthReportApplicationCommand>();
        services.AddScoped<IGetPasswordHealthReportApplicationQuery, GetPasswordHealthReportApplicationQuery>();
        services.AddScoped<IDropPasswordHealthReportApplicationCommand, DropPasswordHealthReportApplicationCommand>();
    }
}
