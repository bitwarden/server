using Bit.Core.Exceptions;
using Bit.Core.KeyManagement.Commands.Interfaces;
using Bit.Core.KeyManagement.Models.Data;
using Bit.Core.Repositories;

namespace Bit.Core.KeyManagement.Commands;

public class SetOrganizationKeyIdCommand : ISetOrganizationKeyIdCommand
{
    private readonly IOrganizationRepository _organizationRepository;

    public SetOrganizationKeyIdCommand(IOrganizationRepository organizationRepository)
    {
        _organizationRepository = organizationRepository;
    }

    /// <inheritdoc />
    public async Task SetOrganizationKeyIdAsync(Guid organizationId, KeyId organizationKeyId)
    {
        if (await _organizationRepository.SetOrganizationKeyIdAsync(organizationId, organizationKeyId))
        {
            return;
        }

        // The repository only sets a key id that is not set yet, so re-read to tell a matching report from a conflict.
        var current = await _organizationRepository.GetByIdAsync(organizationId)
            ?? throw new NotFoundException();
        if (!organizationKeyId.Equals(current.GetOrganizationKeyId()))
        {
            throw new BadRequestException("Organization key id is already set.");
        }
    }
}
