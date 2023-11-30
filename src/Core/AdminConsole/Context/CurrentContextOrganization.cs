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
        var customPermissions = CoreHelpers.LoadClassFromJsonData<Permissions>(orgUser.Permissions);

        Id = orgUser.OrganizationId;
        Type = orgUser.Type;
        Permissions = customPermissions;
        AccessSecretsManager = orgUser.AccessSecretsManager && orgUser.UseSecretsManager && orgUser.Enabled;
        LimitCollectionCreationDeletion = orgUser.LimitCollectionCreationDeletion;
        AllowAdminAccessToAllCollectionItems = orgUser.AllowAdminAccessToAllCollectionItems;

        // Permissions that depend on collection management settings
        // Usually we calculate specific permissions from the orgUser's role, however collection management settings
        // fundamentally change what different roles can do, so we include them here to avoid db lookups
        CreateNewCollections = !orgUser.LimitCollectionCreationDeletion ||
                               customPermissions.CreateNewCollections ||
                               orgUser.Type is OrganizationUserType.Admin or OrganizationUserType.Owner;
    }

    public Guid Id { get; set; }
    public OrganizationUserType Type { get; set; }
    public Permissions Permissions { get; set; } = new();
    public bool AccessSecretsManager { get; set; }
    public bool LimitCollectionCreationDeletion { get; set; }   // this would be deleted
    public bool AllowAdminAccessToAllCollectionItems { get; set; }   // this would be deleted

    /// <summary>
    /// Whether the user can create new collections for the organization, taking into account the collection
    /// management settings introduced with Flexible Collections.
    /// </summary>
    public bool CreateNewCollections { get; set; }
    // TODO: add DeleteManagedCollections (mvp) and AccessAllItems (v1)
}
