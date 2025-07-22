using Bit.Core.Auth.PasswordValidation;
using Bit.Core.Entities;
using Bit.Core.KeyManagement.Sends;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

public static class SendPasswordServiceCollectionExtensions
{
    public static void AddSendPasswordServices(this IServiceCollection services)
    {
        services.TryAddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
        services.Configure<PasswordHasherOptions>(options => options.IterationCount = PasswordValidationConstants.PasswordHasherKdfIterations);
        services.TryAddScoped<ISendPasswordHasher, SendPasswordHasher>();
    }
}
