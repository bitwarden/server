using Bit.Core.KeyManagement.Commands;
using Bit.Core.KeyManagement.Commands.Interfaces;
using Bit.Core.KeyManagement.Kdf;
using Bit.Core.KeyManagement.Kdf.Implementations;
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
        services.AddSendPasswordServices();
    }

    private static void AddKeyManagementCommands(this IServiceCollection services)
    {
        services.AddScoped<IRegenerateUserAsymmetricKeysCommand, RegenerateUserAsymmetricKeysCommand>();
        services.AddScoped<IChangeKdfCommand, ChangeKdfCommand>();
    }

    private static void AddKeyManagementQueries(this IServiceCollection services)
    {
        services.AddScoped<IUserAccountKeysQuery, UserAccountKeysQuery>();
        services.AddScoped<IGetMinimumClientVersionForUserQuery, GetMinimumClientVersionForUserQuery>();
    }
}
