using Bit.Core.Exceptions;
using Bit.Core.KeyManagement.Models.Data;
using Bit.Core.Repositories;

namespace Bit.OrganizationKeys.Commands;

internal sealed class SetOrganizationKeyIdCommand(IOrganizationRepository organizationRepository)
    : ISetOrganizationKeyIdCommand
{
    /// <inheritdoc />
    public async Task SetOrganizationKeyIdAsync(Guid organizationId, KeyId organizationKeyId)
    {
        if (await organizationRepository.SetOrganizationKeyIdAsync(organizationId, organizationKeyId))
        {
            return;
        }

        // The repository only sets a key id that is not set yet, so re-read to tell a matching report from a conflict.
        var current = await organizationRepository.GetByIdAsync(organizationId);
        if (!organizationKeyId.Equals(current!.GetOrganizationKeyId()))
        {
            throw new ConflictException("Organization key id is already set.");
        }
    }
}
