using Bit.Core.Entities;

namespace Bit.Api.Vault.AuthorizationHandlers.Collections;

public record CollectionUserAccessResource(
    ICollection<Collection> Collections,
    Guid? TargetUserId);
