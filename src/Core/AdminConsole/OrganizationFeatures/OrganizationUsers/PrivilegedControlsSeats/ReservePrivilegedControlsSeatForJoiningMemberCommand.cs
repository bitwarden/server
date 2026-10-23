using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Models.Mail.Mailer;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Platform.Mail.Mailer;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Microsoft.Extensions.Logging;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.PrivilegedControlsSeats;

public class ReservePrivilegedControlsSeatForJoiningMemberCommand(
    IReservePrivilegedControlsSeatsCommand reservePrivilegedControlsSeatsCommand,
    IOrganizationUserRepository organizationUserRepository,
    IMailer mailer,
    IGlobalSettings globalSettings,
    ILogger<ReservePrivilegedControlsSeatForJoiningMemberCommand> logger)
    : IReservePrivilegedControlsSeatForJoiningMemberCommand
{
    public async Task<bool> RunAsync(Organization organization, OrganizationUser organizationUser, string memberEmail)
    {
        if (!organization.UsePam || !organizationUser.AccessPam)
        {
            return true;
        }

        var reservation = await reservePrivilegedControlsSeatsCommand.RunAsync(organization, 1);

        if (reservation == PrivilegedControlsSeatReservation.Reserved)
        {
            return true;
        }

        organizationUser.AccessPam = false;
        await NotifyOwnersAsync(organization, memberEmail);
        return false;
    }

    private async Task NotifyOwnersAsync(Organization organization, string memberEmail)
    {
        try
        {
            var ownerEmails = (await organizationUserRepository.GetManyByMinimumRoleAsync(
                    organization.Id, OrganizationUserType.Owner))
                .Select(u => u.Email)
                .Distinct();

            var organizationName = organization.DisplayName();

            await mailer.SendEmail(new PrivilegedControlsSeatNeededMail
            {
                ToEmails = ownerEmails,
                Subject = $"{organizationName} needs more Privileged Controls seats",
                View = new PrivilegedControlsSeatNeededView(globalSettings)
                {
                    OrganizationId = organization.Id,
                    OrganizationName = CoreHelpers.PreventEmailAutoLinking(organizationName),
                    MemberEmail = CoreHelpers.PreventEmailAutoLinking(memberEmail)
                }
            });
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error encountered notifying organization owners that a member needs a Privileged Controls seat.");
        }
    }
}
