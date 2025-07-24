using Bit.Core.KeyManagement.Commands;
using Bit.Core.KeyManagement.Commands.Interfaces;
using Bit.Core.KeyManagement.Kdf;
using Bit.Core.KeyManagement.UserKey.Implementations;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Core.KeyManagement;

public static class KeyManagementServiceCollectionExtensions
{
    public static void AddKeyManagementServices(this IServiceCollection services)
    {
        services.AddKeyManagementCommands();
    }

    private static void AddKeyManagementCommands(this IServiceCollection services)
    {
        services.AddScoped<IRegenerateUserAsymmetricKeysCommand, RegenerateUserAsymmetricKeysCommand>();
        services.AddScoped<IChangeKdfCommand, ChangeKdfCommand>();
    }
}
