using Bit.Core.AdminConsole.Utilities.v2;
using Bit.Core.Models.Data;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Groups;

public interface IGroupCollectionAccessValidator
{
    /// <summary>
    /// Checks that each collection exists, is in the organization, and is not a default user collection.
    /// </summary>
    /// <returns><c>null</c> when valid, otherwise the <see cref="Error"/> describing why it is not.</returns>
    Task<Error?> ValidateAsync(Guid organizationId, ICollection<CollectionAccessSelection> collectionAccess);
}
