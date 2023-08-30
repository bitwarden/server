

using Bit.Core.Auth.UserFeatures.UserMasterPassword;
using Bit.Core.Auth.UserFeatures.UserMasterPassword.Interfaces;
using Bit.Core.Services;
using Bit.Core.Settings;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Core.Auth.UserFeatures;

public static class UserServiceCollectionExtensions
{
    public static void AddUserServices(this IServiceCollection services, IGlobalSettings globalSettings)
    {
        services.AddScoped<IUserService, UserService>();
        services.AddUserPasswordCommands();
    }

    private static void AddUserPasswordCommands(this IServiceCollection services)
    {
        services.AddScoped<ISetInitialMasterPasswordCommand, SetInitialMasterPasswordCommand>();
    }

}
