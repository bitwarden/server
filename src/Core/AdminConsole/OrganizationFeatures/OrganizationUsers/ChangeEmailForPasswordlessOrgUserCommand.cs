using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.Auth.UserFeatures.UserEmail.Interfaces;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Utilities;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers;

public class ChangeEmailForPasswordlessOrgUserCommand : IChangeEmailForPasswordlessOrgUserCommand
{
    private readonly IUserRepository _userRepository;
    private readonly IOrganizationDomainRepository _organizationDomainRepository;
    private readonly IChangeEmailCommand _changeEmailCommand;

    public ChangeEmailForPasswordlessOrgUserCommand(
        IUserRepository userRepository,
        IOrganizationDomainRepository organizationDomainRepository,
        IChangeEmailCommand changeEmailCommand)
    {
        _userRepository = userRepository;
        _organizationDomainRepository = organizationDomainRepository;
        _changeEmailCommand = changeEmailCommand;
    }

    public async Task ChangeOrganizationUserEmailAsync(Guid organizationId, OrganizationUser organizationUser, string newEmail)
    {
        var user = await _userRepository.GetByIdAsync(organizationUser.UserId!.Value)
            ?? throw new NotFoundException();

        if (user.HasMasterPassword())
        {
            throw new BadRequestException("User has a master password.");
        }

        var newDomain = CoreHelpers.GetEmailDomain(newEmail);
        var claimedDomain = await _organizationDomainRepository
            .GetDomainByOrgIdAndDomainNameAsync(organizationId, newDomain!);

        if (claimedDomain?.VerifiedDate == null)
        {
            throw new BadRequestException("The email domain is not claimed by the organization.");
        }

        await _changeEmailCommand.ChangeEmailAsync(user, newEmail, logOutUser: false);
    }
}
