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
    /// A no-op unless the user exists, has no salt stored, and has a master password. Users without
    /// a master password (Key Connector, TDE) have no salt to prefill.
    /// </remarks>
    /// <param name="userId">The user to backfill.</param>
    Task UpdateAsync(Guid userId);
}
