using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.PrivilegedControlsSeats;
using Bit.Core.Billing.Models.Mail.Mailer;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Platform.Mail.Mailer;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Bitwarden.Server.Sdk.Environment;
using Microsoft.Extensions.Logging;
using OrganizationEntity = Bit.Core.AdminConsole.Entities.Organization;

namespace Bit.Subscriptions.Organization.Commands;

internal sealed class ReservePrivilegedControlsSeatForJoiningMemberCommand(
    IReservePrivilegedControlsSeatsCommand reservePrivilegedControlsSeatsCommand,
    IOrganizationUserRepository organizationUserRepository,
    IMailer mailer,
    IGlobalSettings globalSettings,
    IBitwardenEnvironment environment,
    ILogger<ReservePrivilegedControlsSeatForJoiningMemberCommand> logger)
    : IReservePrivilegedControlsSeatForJoiningMemberCommand
{
    public async Task<JoiningMemberPrivilegedControlsAccess> RunAsync(OrganizationEntity organization, OrganizationUser organizationUser)
    {
        if (!organization.UsePam || !organizationUser.AccessPam)
        {
            return JoiningMemberPrivilegedControlsAccess.Kept;
        }

        PrivilegedControlsSeatReservation reservation;
        try
        {
            reservation = await reservePrivilegedControlsSeatsCommand.RunAsync(organization, 1);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Could not reserve a Privileged Controls seat for a member joining organization {OrganizationId}; the member joins without Privileged Controls access.",
                organization.Id);
            organizationUser.AccessPam = false;
            return JoiningMemberPrivilegedControlsAccess.RemovedAfterError;
        }

        if (reservation == PrivilegedControlsSeatReservation.Reserved)
        {
            return JoiningMemberPrivilegedControlsAccess.Kept;
        }

        organizationUser.AccessPam = false;
        return JoiningMemberPrivilegedControlsAccess.RemovedAtSeatLimit;
    }

    public async Task NotifyOwnersAsync(OrganizationEntity organization, string memberEmail)
    {
        if (environment.SelfHosted)
        {
            return;
        }

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
