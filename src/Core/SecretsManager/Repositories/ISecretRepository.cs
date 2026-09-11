// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Core.Enums;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Models.Data;
using Bit.Core.SecretsManager.Models.Data.AccessPolicyUpdates;

namespace Bit.Core.SecretsManager.Repositories;

public interface ISecretRepository
{
    Task<IEnumerable<SecretPermissionDetails>> GetManyDetailsByOrganizationIdAsync(Guid organizationId, Guid userId, AccessClientType accessType);
    Task<IEnumerable<SecretPermissionDetails>> GetManyDetailsByOrganizationIdInTrashAsync(Guid organizationId);
    Task<IEnumerable<SecretPermissionDetails>> GetManyDetailsByProjectIdAsync(Guid projectId, Guid userId, AccessClientType accessType);
    Task<IEnumerable<Secret>> GetManyByOrganizationIdAsync(Guid organizationId, Guid userId, AccessClientType accessType);
    Task<IEnumerable<Secret>> GetManyByOrganizationIdInTrashByIdsAsync(Guid organizationId, IEnumerable<Guid> ids);
    Task<IEnumerable<Secret>> GetManyByIds(IEnumerable<Guid> ids);
    Task<IEnumerable<Secret>> GetManyTrashedSecretsByIds(IEnumerable<Guid> ids);
    Task<Secret> GetByIdAsync(Guid id);
    /// <summary>
    /// Creates a secret together with its initial version snapshot, in the same transaction, so a
    /// secret created here is never persisted without version history.
    /// </summary>
    /// <remarks>
    /// <see cref="ImportAsync"/> is the one create path that writes no version, which is what
    /// <see cref="UpdateAsync"/>'s backfill exists to recover from.
    /// </remarks>
    Task<Secret> CreateAsync(Secret secret, SecretAccessPoliciesUpdates accessPoliciesUpdates,
        SecretVersion initialVersion);

    /// <summary>
    /// Updates a secret, and when <paramref name="newVersion"/> is supplied writes it in the same
    /// transaction. Pass null when the value did not change and no snapshot is wanted.
    /// <para>
    /// When <paramref name="newVersion"/> is supplied and the secret has no version history yet,
    /// a snapshot of the pre-update value is written first, with no editor attributed. This keeps
    /// the overwritten value recoverable for secrets stored before versioning existed.
    /// </para>
    /// </summary>
    Task<Secret> UpdateAsync(Secret secret, SecretAccessPoliciesUpdates accessPoliciesUpdates = null,
        SecretVersion newVersion = null);
    Task SoftDeleteManyByIdAsync(IEnumerable<Guid> ids);
    Task HardDeleteManyByIdAsync(IEnumerable<Guid> ids);
    Task RestoreManyByIdAsync(IEnumerable<Guid> ids);
    Task<IEnumerable<Secret>> ImportAsync(IEnumerable<Secret> secrets);
    Task<(bool Read, bool Write)> AccessToSecretAsync(Guid id, Guid userId, AccessClientType accessType);
    Task<Dictionary<Guid, (bool Read, bool Write)>> AccessToSecretsAsync(IEnumerable<Guid> ids, Guid userId, AccessClientType accessType);
    Task EmptyTrash(DateTime nowTime, uint deleteAfterThisNumberOfDays);
    Task<int> GetSecretsCountByOrganizationIdAsync(Guid organizationId);
    Task<int> GetSecretsCountByOrganizationIdAsync(Guid organizationId, Guid userId, AccessClientType accessType);
}
