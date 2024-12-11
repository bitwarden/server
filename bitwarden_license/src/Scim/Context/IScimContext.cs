using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.OrganizationConnectionConfigs;
using Bit.Core.Repositories;
using Bit.Core.Settings;

namespace Bit.Scim.Context;

public interface IScimContext
{
    ScimProviderType RequestScimProvider { get; set; }
    ScimConfig ScimConfiguration { get; set; }
    Guid? OrganizationId { get; set; }
    Organization Organization { get; set; }
    Task BuildAsync(
        HttpContext httpContext,
        GlobalSettings globalSettings,
        IOrganizationRepository organizationRepository,
        IOrganizationConnectionRepository organizationConnectionRepository
    );
}
