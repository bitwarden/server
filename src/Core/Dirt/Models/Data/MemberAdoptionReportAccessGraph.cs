namespace Bit.Core.Dirt.Reports.Models.Data;

/// <summary>
/// A member's access to one collection, whether granted directly or through a group.
/// </summary>
public readonly record struct MemberCollectionAccess(Guid OrganizationUserId, Guid CollectionId);

/// <summary>
/// An organization-owned, non-deleted cipher's membership of one collection.
/// </summary>
public readonly record struct CollectionCipherLink(Guid CollectionId, Guid CipherId);
