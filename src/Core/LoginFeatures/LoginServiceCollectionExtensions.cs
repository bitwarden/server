using Bit.Core.LoginFeatures.PasswordlessLogin;
using Bit.Core.LoginFeatures.PasswordlessLogin.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Core.LoginFeatures;

public static class LoginServiceCollectionExtensions
{
    public static void AddLoginServices(this IServiceCollection services)
    {
        services.AddScoped<IVerifyAuthRequestCommand, VerifyAuthRequestCommand>();
    }
}

