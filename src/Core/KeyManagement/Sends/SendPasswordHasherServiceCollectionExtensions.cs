using Bit.Core.Auth.UserFeatures.PasswordValidation;
using Bit.Core.KeyManagement.Sends;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

using Microsoft.Extensions.Options;

public static class SendPasswordHasherServiceCollectionExtensions
{
    public static void AddSendPasswordServices(this IServiceCollection services)
    {
        const string sendPasswordHasherMarkerName = "SendPasswordHasherMarker";

        services.AddOptions<PasswordHasherOptions>(sendPasswordHasherMarkerName)
            .Configure(options => options.IterationCount = PasswordValidationConstants.PasswordHasherKdfIterations);

        services.TryAddScoped<IPasswordHasher<SendPasswordHasherMarker>>(sp =>
            {
                var opts = sp
                    .GetRequiredService<IOptionsMonitor<PasswordHasherOptions>>()
                    .Get(sendPasswordHasherMarkerName);

                var optionsAccessor = Options.Create(opts);

                return new PasswordHasher<SendPasswordHasherMarker>(optionsAccessor);
            });
        services.TryAddScoped<ISendPasswordHasher, SendPasswordHasher>();
    }
}
