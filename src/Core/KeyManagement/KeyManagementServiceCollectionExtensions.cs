using Bit.Core.KeyManagement.Authorization;
using Bit.Core.KeyManagement.Commands;
using Bit.Core.KeyManagement.Commands.Interfaces;
using Bit.Core.KeyManagement.Kdf;
using Bit.Core.KeyManagement.Kdf.Implementations;
using Bit.Core.KeyManagement.MasterPassword;
using Bit.Core.KeyManagement.MasterPassword.Interfaces;
using Bit.Core.KeyManagement.Queries;
using Bit.Core.KeyManagement.Queries.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Core.KeyManagement;

public static class KeyManagementServiceCollectionExtensions
{
    public static void AddKeyManagementServices(this IServiceCollection services)
    {
        services.AddKeyManagementAuthorizationHandlers();
        services.AddKeyManagementCommands();
        services.AddKeyManagementQueries();
        services.AddSendPasswordServices();
    }

    private static void AddKeyManagementAuthorizationHandlers(this IServiceCollection services)
    {
        services.AddScoped<IAuthorizationHandler, KeyConnectorAuthorizationHandler>();
    }

    private static void AddKeyManagementCommands(this IServiceCollection services)
    {
        services.AddScoped<IRegenerateUserAsymmetricKeysCommand, RegenerateUserAsymmetricKeysCommand>();
        services.AddScoped<IChangeKdfCommand, ChangeKdfCommand>();
        services.AddScoped<ISetKeyConnectorKeyCommand, SetKeyConnectorKeyCommand>();
        services.AddScoped<ISetInitialMasterPasswordCommand, SetInitialMasterPasswordCommand>();
        services.AddScoped<IUpdateMasterPasswordCommand, UpdateMasterPasswordCommand>();
    }

    private static void AddKeyManagementQueries(this IServiceCollection services)
    {
        services.AddScoped<IUserAccountKeysQuery, UserAccountKeysQuery>();
        services.AddScoped<IKeyConnectorConfirmationDetailsQuery, KeyConnectorConfirmationDetailsQuery>();
        services.AddScoped<ISetInitialMasterPasswordQuery, SetInitialMasterPasswordQuery>();
        services.AddScoped<IUpdateMasterPasswordQuery, UpdateMasterPasswordQuery>();
    }
}
