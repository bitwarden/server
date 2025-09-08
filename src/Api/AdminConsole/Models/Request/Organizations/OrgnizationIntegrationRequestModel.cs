using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Models.Data.EventIntegrations;
using Bit.Core.Enums;

namespace Bit.Api.AdminConsole.Models.Request.Organizations;

public class OrganizationIntegrationRequestModel : IValidatableObject
{
    public string? Configuration { get; init; }

    public IntegrationType Type { get; init; }

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
                yield return new ValidationResult($"{nameof(Type)} integrations are not yet supported.", [nameof(Type)]);
                break;
            case IntegrationType.Slack:
                yield return new ValidationResult($"{nameof(Type)} integrations cannot be created directly.", [nameof(Type)
                ]);
                break;
            case IntegrationType.Webhook:
                foreach (var r in ValidateConfiguration<WebhookIntegration>(allowNullOrEmpty: true))
                    yield return r;
                break;
            case IntegrationType.Hec:
                foreach (var r in ValidateConfiguration<HecIntegration>(allowNullOrEmpty: false))
                    yield return r;
                break;
            case IntegrationType.Datadog:
                foreach (var r in ValidateConfiguration<DatadogIntegration>(allowNullOrEmpty: false))
                    yield return r;
                break;
            default:
                yield return new ValidationResult(
                    $"Integration type '{Type}' is not recognized.",
                    [nameof(Type)]);
                break;
        }
    }

    private List<ValidationResult> ValidateConfiguration<T>(bool allowNullOrEmpty)
    {
        var results = new List<ValidationResult>();

        if (string.IsNullOrWhiteSpace(Configuration))
        {
            if (!allowNullOrEmpty)
                results.Add(InvalidConfig<T>());
            return results;
        }

        try
        {
            if (JsonSerializer.Deserialize<T>(Configuration) is null)
                results.Add(InvalidConfig<T>());
        }
        catch
        {
            results.Add(InvalidConfig<T>());
        }

        return results;
    }

    private static ValidationResult InvalidConfig<T>() =>
        new(errorMessage: $"Must include valid {typeof(T).Name} configuration.", memberNames: [nameof(Configuration)]);
}
