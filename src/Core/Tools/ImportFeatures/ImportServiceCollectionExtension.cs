using Bit.Core.Tools.ImportFeatures.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Core.Tools.ImportFeatures;

public static class ImportServiceCollectionExtension
{
    public static void AddImportServices(this IServiceCollection services)
    {
        services.AddScoped<IImportCiphersCommand, ImportCiphersCommand>();
    }
}
