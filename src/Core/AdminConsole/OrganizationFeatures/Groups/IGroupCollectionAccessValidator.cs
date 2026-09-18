using Bit.Core.Models.Data;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Groups;

public interface IGroupCollectionAccessValidator
{
    /// <summary>
    /// Checks that each collection exists, is in the organization, and is not a default user collection.
    /// Throws instead of returning a ValidationResult, which is what the calling commands expect.
    /// </summary>
    Task ValidateAsync(Guid organizationId, ICollection<CollectionAccessSelection> collectionAccess);
}
