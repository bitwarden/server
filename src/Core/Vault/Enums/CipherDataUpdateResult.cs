namespace Bit.Core.Vault.Enums;

/// <summary>
/// The result of a revision-checked write of a cipher's data. The values match the outcome codes of
/// <c>Cipher_UpdateData</c>.
/// </summary>
public enum CipherDataUpdateResult
{
    /// <summary>The cipher's data and revision date were replaced.</summary>
    Updated = 1,

    /// <summary>The cipher no longer exists, so a retry can't succeed.</summary>
    NotFound = 0,

    /// <summary>Someone else changed the cipher since the last known revision date; nothing was written.</summary>
    RevisionMismatch = -1,
}
