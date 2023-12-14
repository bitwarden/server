

using Bit.Core.Auth.UserFeatures.UserMasterPassword;
using Bit.Core.Auth.UserFeatures.UserMasterPassword.Interfaces;
using Bit.Core.Auth.UserFeatures.WebAuthnLogin;
using Bit.Core.Auth.UserFeatures.WebAuthnLogin.Implementations;
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
        services.AddWebAuthnLoginCommands();
    }

    private static void AddUserPasswordCommands(this IServiceCollection services)
    {
        services.AddScoped<ISetInitialMasterPasswordCommand, SetInitialMasterPasswordCommand>();
    }

    private static void AddWebAuthnLoginCommands(this IServiceCollection services)
    {
        services.AddScoped<IGetWebAuthnLoginCredentialCreateOptionsCommand, GetWebAuthnLoginCredentialCreateOptionsCommand>();
        services.AddScoped<ICreateWebAuthnLoginCredentialCommand, CreateWebAuthnLoginCredentialCommand>();
        services.AddScoped<IGetWebAuthnLoginCredentialAssertionOptionsCommand, GetWebAuthnLoginCredentialAssertionOptionsCommand>();
        services.AddScoped<IAssertWebAuthnLoginCredentialCommand, AssertWebAuthnLoginCredentialCommand>();
    }
}
