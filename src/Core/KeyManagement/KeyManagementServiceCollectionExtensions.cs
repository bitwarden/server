using Bit.Core.KeyManagement.Commands;
using Bit.Core.KeyManagement.Commands.Interfaces;
using Bit.Core.KeyManagement.Queries;
using Bit.Core.KeyManagement.Queries.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Core.KeyManagement;

public static class KeyManagementServiceCollectionExtensions
{
    public static void AddKeyManagementServices(this IServiceCollection services)
    {
        services.AddKeyManagementCommands();
        services.AddKeyManagementQueries();
    }

    private static void AddKeyManagementCommands(this IServiceCollection services)
    {
        services.AddScoped<IRegenerateUserAsymmetricKeysCommand, RegenerateUserAsymmetricKeysCommand>();
    }

    private static void AddKeyManagementQueries(this IServiceCollection services)
    {
        services.AddScoped<IUserAccountKeysQuery, UserAccountKeysQuery>();
    }
}
