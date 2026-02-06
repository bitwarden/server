using Bit.Core.Tools.SendFeatures.Commands;
using Bit.Core.Tools.SendFeatures.Commands.Interfaces;
using Bit.Core.Tools.SendFeatures.Queries;
using Bit.Core.Tools.SendFeatures.Queries.Interfaces;
using Bit.Core.Tools.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Core.Tools.SendFeatures;

public static class SendServiceCollectionExtension
{
    public static void AddSendServices(this IServiceCollection services)
    {
        services.AddScoped<INonAnonymousSendCommand, NonAnonymousSendCommand>();
        services.AddScoped<IAnonymousSendCommand, AnonymousSendCommand>();
        services.AddScoped<ISendAuthorizationService, SendAuthorizationService>();
        services.AddScoped<ISendValidationService, SendValidationService>();
        services.AddScoped<ISendCoreHelperService, SendCoreHelperService>();
        services.AddScoped<ISendAuthenticationQuery, SendAuthenticationQuery>();
        services.AddScoped<ISendOwnerQuery, SendOwnerQuery>();
    }
}
