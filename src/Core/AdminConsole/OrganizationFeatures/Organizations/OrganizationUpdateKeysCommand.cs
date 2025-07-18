// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Core.AdminConsole.Entities;
using Bit.Core.Context;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;

public class OrganizationUpdateKeysCommand : IOrganizationUpdateKeysCommand
{
    private readonly ICurrentContext _currentContext;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IOrganizationService _organizationService;

    public const string OrganizationKeysAlreadyExistErrorMessage = "Organization Keys already exist.";

    public OrganizationUpdateKeysCommand(
        ICurrentContext currentContext,
        IOrganizationRepository organizationRepository,
        IOrganizationService organizationService)
    {
        _currentContext = currentContext;
        _organizationRepository = organizationRepository;
        _organizationService = organizationService;
    }

    public async Task<Organization> UpdateOrganizationKeysAsync(Guid organizationId, string publicKey, string privateKey)
    {
        if (!await _currentContext.ManageResetPassword(organizationId))
        {
            throw new UnauthorizedAccessException();
        }

        // If the keys already exist, error out
        var organization = await _organizationRepository.GetByIdAsync(organizationId);
        if (organization.PublicKey != null && organization.PrivateKey != null)
        {
            throw new BadRequestException(OrganizationKeysAlreadyExistErrorMessage);
        }

        // Update org with generated public/private key
        organization.PublicKey = publicKey;
        organization.PrivateKey = privateKey;

        await _organizationService.UpdateAsync(organization);

        return organization;
    }
}
