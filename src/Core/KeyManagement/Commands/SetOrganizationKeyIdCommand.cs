using Bit.Core.Enums;
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
        var organization = await _organizationRepository.GetByIdAsync(organizationId)
            ?? throw new NotFoundException();

        // A pending organization has no organization key yet. Its key id is set when the organization is initialized.
        if (organization.Status == OrganizationStatusType.Pending)
        {
            throw new BadRequestException("Organization is not initialized.");
        }

        if (await _organizationRepository.SetOrganizationKeyIdAsync(organizationId, organizationKeyId))
        {
            return;
        }

        // The repository only sets a key id that is not set yet, so re-read to tell a matching report from a conflict.
        var current = await _organizationRepository.GetByIdAsync(organizationId);
        if (!organizationKeyId.Equals(current?.GetOrganizationKeyId()))
        {
            throw new BadRequestException("Organization key id is already set.");
        }
    }
}
