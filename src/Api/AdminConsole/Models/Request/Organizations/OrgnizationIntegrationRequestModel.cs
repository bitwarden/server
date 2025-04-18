using System.ComponentModel.DataAnnotations;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Enums;

#nullable enable

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
                if (Configuration is not null)
                {
                    yield return new ValidationResult(
                        "Webhook integrations must not include configuration.",
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
}
