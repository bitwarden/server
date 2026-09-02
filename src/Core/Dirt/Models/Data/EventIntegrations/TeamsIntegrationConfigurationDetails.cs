namespace Bit.Core.Dirt.Models.Data.EventIntegrations;

/// <summary>
/// The per-send Teams configuration, projected from the merged integration configuration. Both values are
/// nullable because they are absent until the app install callback arrives, and are cleared again if the app
/// is later removed.
/// </summary>
public record TeamsIntegrationConfigurationDetails(string? ChannelId, Uri? ServiceUrl);
