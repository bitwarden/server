using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Utilities;

namespace Bit.Core.Context;

public class CurrentContextOrganization
{
    public CurrentContextOrganization() { }

    public CurrentContextOrganization(OrganizationUserOrganizationDetails orgUser)
    {
        Id = orgUser.OrganizationId;
        Type = orgUser.Type;
        Permissions = CoreHelpers.LoadClassFromJsonData<Permissions>(orgUser.Permissions);
        AccessSecretsManager = orgUser.AccessSecretsManager && orgUser.UseSecretsManager;
        LimitCollectionCdOwnerAdmin = orgUser.LimitCollectionCdOwnerAdmin;
    }

    public Guid Id { get; set; }
    public OrganizationUserType Type { get; set; }
    public Permissions Permissions { get; set; }
    public bool AccessSecretsManager { get; set; }
    public bool LimitCollectionCdOwnerAdmin { get; set; }
}
