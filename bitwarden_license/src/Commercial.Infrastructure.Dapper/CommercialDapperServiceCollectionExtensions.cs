using Bit.Commercial.Infrastructure.Dapper.ActionableInsights.Repositories;
using Bit.Core.ActionableInsights.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Commercial.Infrastructure.Dapper;

public static class CommercialDapperServiceCollectionExtensions
{
    public static void AddCommercialDapperRepositories(this IServiceCollection services)
    {
        services.AddSingleton<IReportRepository, ReportRepository>();
    }
}
