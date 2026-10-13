using System.Text.Json;
using Bit.Core.Dirt.Entities;
using Bit.Core.Dirt.Enums;
using Bit.Core.Dirt.Models.Data.EventIntegrations;
using Bit.Core.Dirt.Services;
using Bit.Core.Exceptions;

namespace Bit.Core.Dirt.EventIntegrations.OrganizationIntegrationConfigurations;

/// <summary>
/// Ensures a Teams event configuration only targets a standard channel of the team the Bitwarden app is installed in.
/// Private and shared channels are rejected because the bot can't post to them unless the app is added to that
/// specific channel.
/// </summary>
internal static class TeamsChannelValidation
{
    /// <remarks>Call after <see cref="IOrganizationIntegrationConfigurationValidator"/> has accepted the
    /// configuration. Does nothing for other integration types, when no channel is set (the install channel is
    /// used), or when the channel is unchanged from <paramref name="existingConfiguration"/> (a channel's type can't
    /// change, so it was already checked).</remarks>
    public static async Task EnsureStandardChannelAsync(
        ITeamsService teamsService,
        OrganizationIntegration integration,
        OrganizationIntegrationConfiguration configuration,
        OrganizationIntegrationConfiguration? existingConfiguration = null)
    {
        if (integration.Type != IntegrationType.Teams || configuration.Configuration is null)
        {
            return;
        }

        var channelId = GetChannelId(configuration);
        if (existingConfiguration is not null && GetChannelId(existingConfiguration) == channelId)
        {
            return;
        }

        // The install conversation ID is the team's ID for the Bot Framework.
        var teamsIntegration = integration.Configuration is null
            ? null
            : JsonSerializer.Deserialize<TeamsIntegration>(integration.Configuration);
        if (teamsIntegration is not { ChannelId: { } teamId, ServiceUrl: { } serviceUrl })
        {
            throw new BadRequestException("The Bitwarden app has not been added to a team yet.");
        }

        var channels = await teamsService.GetStandardChannelsAsync(serviceUrl, teamId)
            ?? throw new BadRequestException("Unable to retrieve the channels for the connected team. Please try again.");
        if (!channels.Any(channel => channel.Id == channelId))
        {
            throw new BadRequestException("The selected channel is not a standard channel in the connected team.");
        }
    }

    private static string? GetChannelId(OrganizationIntegrationConfiguration configuration) =>
        configuration.Configuration is null
            ? null
            : JsonSerializer.Deserialize<TeamsIntegrationConfiguration>(configuration.Configuration)?.ChannelId;
}
