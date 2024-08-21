using Bit.Commercial.Infrastructure.EntityFramework.ActionableInsights.Repositories;
using Bit.Core.ActionableInsights.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Commercial.Infrastructure.EntityFramework.ActionableInsights;

public static class ActionableInsightsEfServiceCollectionExtensions
{
    public static void AddActionableInsightsEfRepositories(this IServiceCollection services)
    {
        services.AddSingleton<IReportRepository, ReportRepository>();
    }
}
