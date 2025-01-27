using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Models.Business.Tokenables;
using Bit.Core.AdminConsole.OrganizationFeatures.Organizations.Interfaces;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Tokens;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Organizations;

public class OrganizationInitiateDeleteCommand : IOrganizationInitiateDeleteCommand
{
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IUserRepository _userRepository;
    private readonly IDataProtectorTokenFactory<OrgDeleteTokenable> _orgDeleteTokenDataFactory;
    private readonly IMailService _mailService;

    public const string OrganizationAdminNotFoundErrorMessage = "Org admin not found.";

    public OrganizationInitiateDeleteCommand(
        IOrganizationUserRepository organizationUserRepository,
        IUserRepository userRepository,
        IDataProtectorTokenFactory<OrgDeleteTokenable> orgDeleteTokenDataFactory,
        IMailService mailService)
    {
        _organizationUserRepository = organizationUserRepository;
        _userRepository = userRepository;
        _orgDeleteTokenDataFactory = orgDeleteTokenDataFactory;
        _mailService = mailService;
    }

    public async Task InitiateDeleteAsync(Organization organization, string orgAdminEmail)
    {
        var orgAdmin = await _userRepository.GetByEmailAsync(orgAdminEmail);
        if (orgAdmin == null)
        {
            throw new BadRequestException(OrganizationAdminNotFoundErrorMessage);
        }
        var orgAdminOrgUser = await _organizationUserRepository.GetDetailsByUserAsync(orgAdmin.Id, organization.Id);
        if (orgAdminOrgUser == null || orgAdminOrgUser.Status is not OrganizationUserStatusType.Confirmed ||
            (orgAdminOrgUser.Type is not OrganizationUserType.Admin and not OrganizationUserType.Owner))
        {
            throw new BadRequestException(OrganizationAdminNotFoundErrorMessage);
        }
        var token = _orgDeleteTokenDataFactory.Protect(new OrgDeleteTokenable(organization, 1));
        await _mailService.SendInitiateDeleteOrganzationEmailAsync(orgAdminEmail, organization, token);
    }
}
