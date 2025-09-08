using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Models.Data.EventIntegrations;
using Bit.Core.Enums;

namespace Bit.Api.AdminConsole.Models.Request.Organizations;

public class OrganizationIntegrationRequestModel : IValidatableObject
{
    public string? Configuration { get; set; }

    public IntegrationType Type { get; set; }

    public OrganizationIntegration ToOrganizationIntegration(Guid organizationId)
    {
        return new OrganizationIntegration()
        {
            OrganizationId = organizationId,
            Configuration = Configuration,
            Type = Type,
        };
    }

    public OrganizationIntegration ToOrganizationIntegration(OrganizationIntegration currentIntegration)
    {
        currentIntegration.Configuration = Configuration;
        return currentIntegration;
    }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        switch (Type)
        {
            case IntegrationType.CloudBillingSync or IntegrationType.Scim:
                yield return new ValidationResult($"{nameof(Type)} integrations are not yet supported.", new[] { nameof(Type) });
                break;
            case IntegrationType.Slack:
                yield return new ValidationResult($"{nameof(Type)} integrations cannot be created directly.", new[] { nameof(Type) });
                break;
            case IntegrationType.Webhook:
                if (string.IsNullOrWhiteSpace(Configuration))
                {
                    break;
                }
                if (!IsIntegrationValid<WebhookIntegration>())
                {
                    yield return new ValidationResult(
                        "Webhook integrations must include valid configuration.",
                        new[] { nameof(Configuration) });
                }
                break;
            case IntegrationType.Hec:
                if (!IsIntegrationValid<HecIntegration>())
                {
                    yield return new ValidationResult(
                        "HEC integrations must include valid configuration.",
                        new[] { nameof(Configuration) });
                }
                break;
            case IntegrationType.Datadog:
                if (!IsIntegrationValid<DatadogIntegration>())
                {
                    yield return new ValidationResult(
                        "Datadog integrations must include valid configuration.",
                        new[] { nameof(Configuration) });
                }
                break;
            default:
                yield return new ValidationResult(
                    $"Integration type '{Type}' is not recognized.",
                    new[] { nameof(Type) });
                break;
        }
    }

    private bool IsIntegrationValid<T>()
    {
        if (string.IsNullOrWhiteSpace(Configuration))
        {
            return false;
        }

        try
        {
            var config = JsonSerializer.Deserialize<T>(Configuration);
            return config is not null;
        }
        catch
        {
            return false;
        }
    }
}
