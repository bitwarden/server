using Bit.Core.Auth.Sso;
using Bit.Core.Auth.UserFeatures.DeviceTrust;
using Bit.Core.Auth.UserFeatures.PremiumAccess;
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
        services.AddDeviceTrustCommands();
        services.AddUserPasswordCommands();
        services.AddUserRegistrationCommands();
        services.AddWebAuthnLoginCommands();
        services.AddTdeOffboardingPasswordCommands();
        services.AddPremiumAccessQueries();
        services.AddTwoFactorQueries();
        services.AddSsoQueries();
    }

    public static void AddDeviceTrustCommands(this IServiceCollection services)
    {
        services.AddScoped<IUntrustDevicesCommand, UntrustDevicesCommand>();
    }

    public static void AddUserKeyCommands(this IServiceCollection services, IGlobalSettings globalSettings)
    {
        services.AddScoped<IRotateUserAccountKeysCommand, RotateUserAccountKeysCommand>();
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

    private static void AddPremiumAccessQueries(this IServiceCollection services)
    {
        services.AddScoped<IPremiumAccessQuery, PremiumAccessQuery>();
    }

    private static void AddTwoFactorQueries(this IServiceCollection services)
    {
        services.AddScoped<ITwoFactorIsEnabledQuery, TwoFactorIsEnabledQuery>();
    }

    private static void AddSsoQueries(this IServiceCollection services)
    {
        services.AddScoped<IUserSsoOrganizationIdentifierQuery, UserSsoOrganizationIdentifierQuery>();
    }
}
