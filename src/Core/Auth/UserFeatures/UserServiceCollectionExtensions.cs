

using Bit.Core.Auth.UserFeatures.Registration;
using Bit.Core.Auth.UserFeatures.Registration.Implementations;
using Bit.Core.Auth.UserFeatures.TdeOffboardingPassword.Interfaces;
using Bit.Core.Auth.UserFeatures.TwoFactorAuth;
using Bit.Core.Auth.UserFeatures.TwoFactorAuth.Interfaces;
using Bit.Core.Auth.UserFeatures.UserMasterPassword;
using Bit.Core.Auth.UserFeatures.UserMasterPassword.Interfaces;
using Bit.Core.Auth.UserFeatures.WebAuthnLogin;
using Bit.Core.Auth.UserFeatures.WebAuthnLogin.Implementations;
using Bit.Core.KeyManagement.UserKey;
using Bit.Core.KeyManagement.UserKey.Implementations;
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
        services.AddUserRegistrationCommands();
        services.AddWebAuthnLoginCommands();
        services.AddTdeOffboardingPasswordCommands();
        services.AddTwoFactorQueries();
    }

    public static void AddUserKeyCommands(this IServiceCollection services, IGlobalSettings globalSettings)
    {
        services.AddScoped<IRotateUserKeyCommand, RotateUserKeyCommand>();
    }

    private static void AddUserPasswordCommands(this IServiceCollection services)
    {
        services.AddScoped<ISetInitialMasterPasswordCommand, SetInitialMasterPasswordCommand>();
    }

    private static void AddTdeOffboardingPasswordCommands(this IServiceCollection services)
    {
        services.AddScoped<ITdeOffboardingPasswordCommand, TdeOffboardingPasswordCommand>();
    }

    private static void AddUserRegistrationCommands(this IServiceCollection services)
    {
        services.AddScoped<ISendVerificationEmailForRegistrationCommand, SendVerificationEmailForRegistrationCommand>();
        services.AddScoped<IRegisterUserCommand, RegisterUserCommand>();
    }

    private static void AddWebAuthnLoginCommands(this IServiceCollection services)
    {
        services.AddScoped<IGetWebAuthnLoginCredentialCreateOptionsCommand, GetWebAuthnLoginCredentialCreateOptionsCommand>();
        services.AddScoped<ICreateWebAuthnLoginCredentialCommand, CreateWebAuthnLoginCredentialCommand>();
        services.AddScoped<IGetWebAuthnLoginCredentialAssertionOptionsCommand, GetWebAuthnLoginCredentialAssertionOptionsCommand>();
        services.AddScoped<IAssertWebAuthnLoginCredentialCommand, AssertWebAuthnLoginCredentialCommand>();
    }

    private static void AddTwoFactorQueries(this IServiceCollection services)
    {
        services.AddScoped<ITwoFactorIsEnabledQuery, TwoFactorIsEnabledQuery>();
    }
}
