using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Utilities;

namespace Bit.Core.Context;

public class CurrentContentOrganization
{
    public CurrentContentOrganization() { }

    public CurrentContentOrganization(OrganizationUserOrganizationDetails orgUser)
    {
        Id = orgUser.OrganizationId;
        Type = orgUser.Type;
        Permissions = CoreHelpers.LoadClassFromJsonData<Permissions>(orgUser.Permissions);
        AccessSecretsManager = orgUser.AccessSecretsManager && orgUser.UseSecretsManager;
    }

    public Guid Id { get; set; }
    public OrganizationUserType Type { get; set; }
    public Permissions Permissions { get; set; } = new Permissions();
    public bool AccessSecretsManager { get; set; }
}
