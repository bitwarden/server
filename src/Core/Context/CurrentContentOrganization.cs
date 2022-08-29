using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Utilities;

namespace Bit.Core.Context;

public class CurrentContentOrganization
{
    public CurrentContentOrganization() { }

    public CurrentContentOrganization(OrganizationUser orgUser)
    {
        Id = orgUser.OrganizationId;
        Type = orgUser.Type;
        Permissions = CoreHelpers.LoadClassFromJsonData<Permissions>(orgUser.Permissions);
    }

    public Guid Id { get; set; }
    public OrganizationUserType Type { get; set; }
    public Permissions Permissions { get; set; }
}
