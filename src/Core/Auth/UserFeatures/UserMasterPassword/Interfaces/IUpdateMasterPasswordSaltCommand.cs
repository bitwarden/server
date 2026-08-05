using Bit.Core.Entities;

namespace Bit.Core.Auth.UserFeatures.UserMasterPassword.Interfaces;

/// <summary>
/// <para>Backfills <see cref="User.MasterPasswordSalt"/> for users who predate the column, so that
/// the salt becomes a stored value rather than one derived from the email at read time by
/// <see cref="User.GetMasterPasswordSalt"/>.</para>
/// <para>The salt written is the user's normalized email — the same value clients already derive —
/// so the backfill is not observable by clients and does not bump the user's revision dates.</para>
/// </summary>
public interface IUpdateMasterPasswordSaltCommand
{
    /// <summary>
    /// Writes the user's normalized email to <see cref="User.MasterPasswordSalt"/>.
    /// </summary>
    /// <remarks>
    /// <para>The passed entity is used only to decide whether a write is worth attempting and to
    /// derive the salt; it is never mutated or persisted. The authoritative guards run in the
    /// database as part of a single conditional UPDATE
    /// (<see cref="Bit.Core.Repositories.IUserRepository.SetMasterPasswordSaltIfNullAsync"/>), so a
    /// stale entity cannot overwrite a salt that another request has since written.</para>
    /// <para>A no-op unless the user has no salt stored and has a master password. Users without a
    /// master password (Key Connector, TDE) have no salt to prefill.</para>
    /// </remarks>
    /// <param name="user">
    /// The already-resolved user to backfill. Resolution is the caller's responsibility — this
    /// command does not read the row back, so callers that already hold the entity (on the
    /// token-refresh path, the one the legacy-user check left on <c>ICurrentContext.User</c>) avoid
    /// a second read of the same row.
    /// </param>
    Task UpdateAsync(User user);
}
