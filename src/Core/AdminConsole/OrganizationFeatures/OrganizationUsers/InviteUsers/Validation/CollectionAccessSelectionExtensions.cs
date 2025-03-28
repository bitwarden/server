using Bit.Core.Models.Data;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation;

public static class CollectionAccessSelectionExtensions
{
    /// <summary>
    /// This validates the permissions on the given assigned collection
    /// </summary>
    public static bool IsValidCollectionAccessConfiguration(this CollectionAccessSelection collectionAccessSelection) =>
        collectionAccessSelection.Manage && (collectionAccessSelection.ReadOnly || collectionAccessSelection.HidePasswords);
}
