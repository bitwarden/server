using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Bit.Core.Vault.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bit.Seeder.Pipeline;

/// <summary>
/// Shared attachment-storage backend selection for the seeder entry points, mirroring the main app's
/// selection in <c>SharedWeb</c>: Azure when a connection string is configured, local disk when a base
/// directory is configured, otherwise a no-op.
/// </summary>
public static class AttachmentStorageServiceCollectionExtensions
{
    public static IServiceCollection AddAttachmentStorageService(this IServiceCollection services, GlobalSettings globalSettings)
    {
        if (CoreHelpers.SettingHasValue(globalSettings.Attachment.ConnectionString))
        {
            services.TryAddSingleton<IAttachmentStorageService, AzureAttachmentStorageService>();
        }
        else if (CoreHelpers.SettingHasValue(globalSettings.Attachment.BaseDirectory))
        {
            services.TryAddSingleton<IAttachmentStorageService, LocalAttachmentStorageService>();
        }
        else
        {
            services.TryAddSingleton<IAttachmentStorageService, NoopAttachmentStorageService>();
        }

        return services;
    }
}
