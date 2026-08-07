using Bit.Core.Entities;
using Bit.Core.Models.Data;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Collections.ModifyUserAccess;

public record CollectionUserAccessTarget(Collection Collection, CollectionAccessDetails AccessDetails);

public record ModifyCollectionUserAccessRequest(
    IReadOnlyCollection<CollectionUserAccessTarget> Targets,
    IReadOnlyCollection<CollectionAccessSelection> Add,
    IReadOnlyCollection<CollectionAccessSelection> Update,
    IReadOnlyCollection<Guid> Remove,
    Guid? PerformingOrganizationUserId,
    bool AllowAdminAccessToAllCollectionItems);
