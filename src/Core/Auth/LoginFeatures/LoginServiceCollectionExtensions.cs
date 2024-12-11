using Bit.Core.Auth.LoginFeatures.PasswordlessLogin;
using Bit.Core.Auth.LoginFeatures.PasswordlessLogin.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Core.Auth.LoginFeatures;

public static class LoginServiceCollectionExtensions
{
    public static void AddLoginServices(this IServiceCollection services)
    {
        services.AddScoped<IVerifyAuthRequestCommand, VerifyAuthRequestCommand>();
    }
}
