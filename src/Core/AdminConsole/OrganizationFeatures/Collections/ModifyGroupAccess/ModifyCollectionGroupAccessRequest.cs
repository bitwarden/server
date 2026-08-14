using Bit.Core.Entities;
using Bit.Core.Models.Data;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Collections.ModifyGroupAccess;

public record CollectionGroupAccessTarget(Collection Collection, CollectionAccessDetails AccessDetails);

public record ModifyCollectionGroupAccessRequest(
    IReadOnlyCollection<CollectionGroupAccessTarget> Targets,
    IReadOnlyCollection<CollectionAccessSelection> Add,
    IReadOnlyCollection<CollectionAccessSelection> Update,
    IReadOnlyCollection<Guid> Remove,
    Guid? PerformingOrganizationUserId,
    bool AllowAdminAccessToAllCollectionItems);
