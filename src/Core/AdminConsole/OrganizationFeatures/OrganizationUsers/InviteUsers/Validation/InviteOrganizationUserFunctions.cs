using Bit.Core.Models.Data;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation;

public static class InviteOrganizationUserFunctions
{
    /// <summary>
    /// This
    /// </summary>
    public static Func<CollectionAccessSelection, bool> ValidateCollectionConfiguration => collectionAccessSelection =>
        collectionAccessSelection.Manage && (collectionAccessSelection.ReadOnly || collectionAccessSelection.HidePasswords);
}
