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

        // If present and the configuration is null, OAuth has been initiated, and we are
        // waiting on the return OAuth call. If Configuration is not null and IsCompleted is true,
        // then we've received the app install bot callback, and it's Completed. Otherwise,
        // it is In Progress while we await the app install bot callback.
        IntegrationType.Teams => string.IsNullOrWhiteSpace(Configuration)
            ? OrganizationIntegrationStatus.Initiated
            : (JsonSerializer.Deserialize<TeamsIntegration>(Configuration)?.IsCompleted ?? false)
                ? OrganizationIntegrationStatus.Completed
                : OrganizationIntegrationStatus.InProgress,

        // HEC and Datadog should only be allowed to be created non-null.
        // If they are null, they are Invalid
        IntegrationType.Hec => string.IsNullOrWhiteSpace(Configuration)
            ? OrganizationIntegrationStatus.Invalid
            : OrganizationIntegrationStatus.Completed,
        IntegrationType.Datadog => string.IsNullOrWhiteSpace(Configuration)
            ? OrganizationIntegrationStatus.Invalid
            : OrganizationIntegrationStatus.Completed,
    };
}
