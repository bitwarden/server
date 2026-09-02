using System.Text.Json;
using Bit.Core.Dirt.Entities;
using Bit.Core.Dirt.Enums;
using Bit.Core.Dirt.Models.Data.EventIntegrations;
using Bit.Core.Models.Api;

namespace Bit.Api.Dirt.Models.Response;

public class OrganizationIntegrationResponseModel : ResponseModel
{
    public OrganizationIntegrationResponseModel(OrganizationIntegration organizationIntegration, string obj = "organizationIntegration")
        : base(obj)
    {
        ArgumentNullException.ThrowIfNull(organizationIntegration);

        Id = organizationIntegration.Id;
        Type = organizationIntegration.Type;
        Configuration = organizationIntegration.Configuration;
    }

    public Guid Id { get; set; }
    public IntegrationType Type { get; set; }
    public string? Configuration { get; set; }

    public OrganizationIntegrationStatus Status => Type switch
    {
        // Not yet implemented, shouldn't be present, NotApplicable
        IntegrationType.CloudBillingSync => OrganizationIntegrationStatus.NotApplicable,
        IntegrationType.Scim => OrganizationIntegrationStatus.NotApplicable,

        // Webhook is allowed to be null. If it's present, it's Completed
        IntegrationType.Webhook => OrganizationIntegrationStatus.Completed,

        // If present and the configuration is null, OAuth has been initiated, and we are
        // waiting on the return call
        IntegrationType.Slack => string.IsNullOrWhiteSpace(Configuration)
            ? OrganizationIntegrationStatus.Initiated
            : OrganizationIntegrationStatus.Completed,

        IntegrationType.Teams => TeamsStatus(Configuration),

        // HEC and Datadog should only be allowed to be created non-null.
        // If they are null, they are Invalid
        IntegrationType.Hec => string.IsNullOrWhiteSpace(Configuration)
            ? OrganizationIntegrationStatus.Invalid
            : OrganizationIntegrationStatus.Completed,
        IntegrationType.Datadog => string.IsNullOrWhiteSpace(Configuration)
            ? OrganizationIntegrationStatus.Invalid
            : OrganizationIntegrationStatus.Completed,
    };

    /// <summary>
    /// Teams is configured over two round trips — an OAuth flow followed by an app install callback — and can
    /// later lose its channel if the app is removed, so it carries more states than the other integrations.
    /// </summary>
    private static OrganizationIntegrationStatus TeamsStatus(string? configuration)
    {
        // OAuth has been initiated and we are waiting on the return OAuth call.
        if (string.IsNullOrWhiteSpace(configuration))
        {
            return OrganizationIntegrationStatus.Initiated;
        }

        var teamsIntegration = TeamsIntegration.FromConfiguration(configuration);
        if (teamsIntegration is null)
        {
            return OrganizationIntegrationStatus.Invalid;
        }

        // Checked ahead of IsCompleted so a disconnected integration can never report as healthy.
        if (teamsIntegration.NeedsReconnection)
        {
            return OrganizationIntegrationStatus.NeedsReconnection;
        }

        // Completed once the app install callback has supplied a channel; In Progress until then.
        return teamsIntegration.IsCompleted
            ? OrganizationIntegrationStatus.Completed
            : OrganizationIntegrationStatus.InProgress;
    }
}
