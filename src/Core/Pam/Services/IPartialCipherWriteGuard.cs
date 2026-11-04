using Bit.Core.Vault.Entities;

namespace Bit.Core.Pam.Services;

/// <summary>
/// Refuses a cipher update that looks built from a partial (leasing-gated) copy, so a client that holds only the
/// partial cannot blank the withheld fields.
/// </summary>
public interface IPartialCipherWriteGuard
{
    /// <summary>Throws a <see cref="Exceptions.BadRequestException"/> if the update would blank withheld content.</summary>
    Task EnsureNotPartialShapedAsync(Cipher cipher);
}
