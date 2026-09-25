namespace Bit.Core.Enums;

/// <summary>
/// Represents the type of a <see cref="Bit.Core.Entities.Collection"/>, indicating how it was created and how it behaves.
/// </summary>
public enum CollectionType
{
    /// <summary>
    /// A standard collection shared among organization members.
    /// </summary>
    SharedCollection = 0,
    /// <summary>
    /// A personal "My Items" collection created for a specific organization member when the
    /// Organization Data Ownership policy is enabled.
    /// </summary>
    DefaultUserCollection = 1,
}
