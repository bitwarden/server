using Bit.Core.AdminConsole.OrganizationFeatures.Organizations.Interfaces;
using Bit.Core.Billing.Models.Mail.Mailer;
using Bit.Core.Billing.Organizations.Commands;
using Bit.Core.Billing.Organizations.Helpers;
using Bit.Core.Billing.Organizations.Models;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Platform.Mail.Mailer;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Bit.Subscriptions.Organization.Queries;
using Bitwarden.Server.Sdk.Environment;
using Microsoft.Extensions.Logging;
using OrganizationEntity = Bit.Core.AdminConsole.Entities.Organization;

namespace Bit.Subscriptions.Organization.Commands.PrivilegedControls;

internal sealed class AutoscalePrivilegedControlsSeatsCommand(
    IPrivilegedControlsSeatChangeSetFactory seatChangeSetFactory,
    IUpdateOrganizationSubscriptionCommand updateOrganizationSubscriptionCommand,
    IReplaceOrganizationAndUpdateCacheCommand replaceOrganizationAndUpdateCacheCommand,
    IOrganizationUserRepository organizationUserRepository,
    IIsOnPrivilegedControlsSalesInitiatedTrialQuery isOnTrialQuery,
    IMailer mailer,
    IGlobalSettings globalSettings,
    IBitwardenEnvironment environment,
    ILogger<AutoscalePrivilegedControlsSeatsCommand> logger) : IAutoscalePrivilegedControlsSeatsCommand
{
    public async Task<AutoscalePrivilegedControlsSeatsResult> RunAsync(OrganizationEntity organization, int seatsRequested)
    {
        if (environment.SelfHosted)
        {
            throw new BadRequestException("Cannot update subscription on a self-hosted instance.");
        }

        if (seatsRequested < 1)
        {
            throw new BadRequestException("At least one Privileged Controls seat must be added.");
        }

        if (!organization.UsePam || organization.PamSeats.GetValueOrDefault() < 1)
        {
            throw new BadRequestException("Your organization doesn't have Privileged Controls.");
        }

        var seatsNeeded =
            await organizationUserRepository.GetOccupiedPamSeatCountByOrganizationIdAsync(organization.Id) + seatsRequested;

        if (seatsNeeded > organization.PamSeats)
        {
            if (organization.MaxAutoscalePamSeats < seatsNeeded)
            {
                return new AutoscalePrivilegedControlsSeatsResult(false, organization.MaxAutoscalePamSeats);
            }

            if (!await isOnTrialQuery.Run(organization))
            {
                var changeSet = await seatChangeSetFactory.CreateAsync(organization, seatsNeeded);
                (await updateOrganizationSubscriptionCommand.Run(organization, changeSet)).GetValueOrThrowHttpException();
            }

            organization.PamSeats = seatsNeeded;
            await replaceOrganizationAndUpdateCacheCommand.RunAsync(organization);
        }

        if (organization.MaxAutoscalePamSeats == seatsNeeded)
        {
            await NotifyOwnersOfSeatLimitAsync(organization, seatsNeeded);
        }

        return new AutoscalePrivilegedControlsSeatsResult(true);
    }

    private async Task NotifyOwnersOfSeatLimitAsync(OrganizationEntity organization, int maxSeatCount)
    {
        try
        {
            var ownerEmails = (await organizationUserRepository.GetManyByMinimumRoleAsync(
                    organization.Id, OrganizationUserType.Owner))
                .Select(u => u.Email)
                .Distinct();

            await mailer.SendEmail(new PrivilegedControlsSeatLimitReachedMail
            {
                ToEmails = ownerEmails,
                Subject = $"{organization.DisplayName()} has reached its Privileged Controls seat limit",
                View = new PrivilegedControlsSeatLimitReachedView(globalSettings)
                {
                    OrganizationId = organization.Id,
                    OrganizationName = CoreHelpers.PreventEmailAutoLinking(organization.DisplayName()),
                    MaxSeatCount = maxSeatCount
                }
            });
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error encountered notifying organization owners of Privileged Controls seat limit reached.");
        }
    }
}
