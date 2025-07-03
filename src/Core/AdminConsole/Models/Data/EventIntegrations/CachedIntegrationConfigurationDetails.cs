#nullable enable

namespace Bit.Core.AdminConsole.Models.Data.EventIntegrations;

public class CachedIntegrationConfigurationDetails<T>
{
    public IntegrationFilterGroup? FilterGroup { get; init; }
    public required T Configuration { get; init; }
    public required string Template { get; init; }
}
