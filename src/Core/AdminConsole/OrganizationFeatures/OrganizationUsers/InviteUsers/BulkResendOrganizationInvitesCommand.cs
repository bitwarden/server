using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Models;
using Bit.Core.AdminConsole.Utilities.DebuggingInstruments;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Microsoft.Extensions.Logging;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers;

public class BulkResendOrganizationInvitesCommand : IBulkResendOrganizationInvitesCommand
{
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly ISendOrganizationInvitesCommand _sendOrganizationInvitesCommand;
    private readonly ILogger<BulkResendOrganizationInvitesCommand> _logger;

    public BulkResendOrganizationInvitesCommand(
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationRepository organizationRepository,
        ISendOrganizationInvitesCommand sendOrganizationInvitesCommand,
        ILogger<BulkResendOrganizationInvitesCommand> logger)
    {
        _organizationUserRepository = organizationUserRepository;
        _organizationRepository = organizationRepository;
        _sendOrganizationInvitesCommand = sendOrganizationInvitesCommand;
        _logger = logger;
    }

    public async Task<IEnumerable<Tuple<OrganizationUser, string>>> BulkResendInvitesAsync(
        Guid organizationId,
        Guid? invitingUserId,
        IEnumerable<Guid> organizationUsersId)
    {
        var orgUsers = await _organizationUserRepository.GetManyAsync(organizationUsersId);
        _logger.LogUserInviteStateDiagnostics(orgUsers);

        var org = await _organizationRepository.GetByIdAsync(organizationId);
        if (org == null)
        {
            throw new NotFoundException();
        }

        var validUsers = new List<OrganizationUser>();
        var result = new List<Tuple<OrganizationUser, string>>();

        foreach (var orgUser in orgUsers)
        {
            if (orgUser.Status != OrganizationUserStatusType.Invited || orgUser.OrganizationId != organizationId)
            {
                result.Add(Tuple.Create(orgUser, "User invalid."));
            }
            else
            {
                validUsers.Add(orgUser);
            }
        }

        if (validUsers.Any())
        {
            await _sendOrganizationInvitesCommand.SendInvitesAsync(
                new SendInvitesRequest(validUsers, org));

            result.AddRange(validUsers.Select(u => Tuple.Create(u, "")));
        }

        return result;
    }
}
