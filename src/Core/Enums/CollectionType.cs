namespace Bit.Core.Enums;

public enum CollectionType
{
    /// <summary>
    /// A standard collection used to share items within an organization.
    /// </summary>
    SharedCollection = 0,

    /// <summary>
    /// The default collection for an OrganizationUser, called a "My Items" collection in the product.
    /// This is used by an OrganizationUser to store their items within the organization and is not
    /// accessible by other members.
    /// It is only created if the Organization Data Ownership policy is enabled.
    /// </summary>
    DefaultUserCollection = 1,
}
