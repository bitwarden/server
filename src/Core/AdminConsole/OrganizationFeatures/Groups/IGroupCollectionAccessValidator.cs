using Bit.Core.Models.Data;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Groups;

public interface IGroupCollectionAccessValidator
{
    /// <summary>
    /// Checks that every collection a group is being given access to exists, belongs to the
    /// organization, and is not a default user collection.
    /// </summary>
    /// <exception cref="Bit.Core.Exceptions.NotFoundException">
    /// A collection does not exist, or belongs to another organization.
    /// </exception>
    /// <exception cref="Bit.Core.Exceptions.BadRequestException">
    /// A collection is a <see cref="Bit.Core.Enums.CollectionType.DefaultUserCollection"/>.
    /// </exception>
    Task ValidateAsync(Guid organizationId, ICollection<CollectionAccessSelection> collectionAccess);
}
