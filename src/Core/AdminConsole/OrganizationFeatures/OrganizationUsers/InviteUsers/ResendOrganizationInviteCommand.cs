using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Models;
using Bit.Core.AdminConsole.Utilities.DebuggingInstruments;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Microsoft.Extensions.Logging;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers;

public class ResendOrganizationInviteCommand : IResendOrganizationInviteCommand
{
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly ISendOrganizationInvitesCommand _sendOrganizationInvitesCommand;
    private readonly ILogger<ResendOrganizationInviteCommand> _logger;

    public ResendOrganizationInviteCommand(
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationRepository organizationRepository,
        ISendOrganizationInvitesCommand sendOrganizationInvitesCommand,
        ILogger<ResendOrganizationInviteCommand> logger)
    {
        _organizationUserRepository = organizationUserRepository;
        _organizationRepository = organizationRepository;
        _sendOrganizationInvitesCommand = sendOrganizationInvitesCommand;
        _logger = logger;
    }

    public async Task ResendInviteAsync(Guid organizationId, Guid? invitingUserId, Guid organizationUserId,
        bool initOrganization = false)
    {
        var organizationUser = await _organizationUserRepository.GetByIdAsync(organizationUserId);
        if (organizationUser == null || organizationUser.OrganizationId != organizationId ||
            organizationUser.Status != OrganizationUserStatusType.Invited)
        {
            throw new BadRequestException("User invalid.");
        }

        _logger.LogUserInviteStateDiagnostics(organizationUser);

        var organization = await _organizationRepository.GetByIdAsync(organizationUser.OrganizationId);
        if (organization == null)
        {
            throw new BadRequestException("Organization invalid.");
        }
        await SendInviteAsync(organizationUser, organization, initOrganization);
    }

    private async Task SendInviteAsync(OrganizationUser organizationUser, Organization organization, bool initOrganization) =>
        await _sendOrganizationInvitesCommand.SendInvitesAsync(new SendInvitesRequest(
            users: [organizationUser],
            organization: organization,
            initOrganization: initOrganization));
}
