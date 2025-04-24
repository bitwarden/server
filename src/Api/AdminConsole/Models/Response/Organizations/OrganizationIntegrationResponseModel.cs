using Bit.Core.AdminConsole.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Api;

#nullable enable

namespace Bit.Api.AdminConsole.Models.Response.Organizations;

public class OrganizationIntegrationResponseModel : ResponseModel
{
    public OrganizationIntegrationResponseModel(OrganizationIntegration organizationIntegration, string obj = "organizationIntegration")
        : base(obj)
    {
        ArgumentNullException.ThrowIfNull(organizationIntegration);

        Id = organizationIntegration.Id;
        Type = organizationIntegration.Type;
    }

    public Guid Id { get; set; }
    public IntegrationType Type { get; set; }
}
