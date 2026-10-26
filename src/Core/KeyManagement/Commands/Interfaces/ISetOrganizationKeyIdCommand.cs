using Bit.Core.KeyManagement.Models.Data;

namespace Bit.Core.KeyManagement.Commands.Interfaces;

public interface ISetOrganizationKeyIdCommand
{
    /// <summary>
    /// Stores the key id of an organization's current organization key.
    /// </summary>
    /// <remarks>
    /// This is a backfill primitive for organizations that predate the key id. It only sets a key id that is not set
    /// yet. Reporting the key id that is already set succeeds, so multiple admin clients can report it at the same time.
    /// <para>
    /// This command does not check that the organization key exists. The caller must confirm it first, for example
    /// by checking that the acting member holds the organization key.
    /// </para>
    /// </remarks>
    /// <param name="organizationId">The organization whose key id is being recorded.</param>
    /// <param name="organizationKeyId">Key id of the organization's current organization key.</param>
    /// <exception cref="Bit.Core.Exceptions.NotFoundException">Thrown when the organization does not exist.</exception>
    /// <exception cref="Bit.Core.Exceptions.BadRequestException">
    /// Thrown when the organization already has a different key id.
    /// </exception>
    Task SetOrganizationKeyIdAsync(Guid organizationId, KeyId organizationKeyId);
}
