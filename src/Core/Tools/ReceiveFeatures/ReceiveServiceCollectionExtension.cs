using Bit.Core.Tools.ReceiveFeatures.Commands;
using Bit.Core.Tools.ReceiveFeatures.Commands.Interfaces;
using Bit.Core.Tools.Services;
using Microsoft.Extensions.DependencyInjection;


namespace Bit.Core.Tools.ReceiveFeatures;

public static class ReceiveServiceCollectionExtension
{
    public static void AddReceiveServices(this IServiceCollection services)
    {
        services.AddScoped<ICreateReceiveCommand, CreateReceiveCommand>();
        services.AddScoped<IUploadReceiveFileCommand, UploadReceiveFileCommand>();
        services.AddScoped<IReceiveAuthorizationService, ReceiveAuthorizationService>();
        services.AddScoped<IReceiveValidationService, ReceiveValidationService>();
    }
}
