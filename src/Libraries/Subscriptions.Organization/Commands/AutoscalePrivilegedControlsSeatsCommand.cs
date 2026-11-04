using Bit.Core.Billing.Models.Mail.Mailer;
using Bit.Core.Billing.Organizations.Commands;
using Bit.Core.Billing.Organizations.Helpers;
using Bit.Core.Billing.Organizations.Models;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Platform.Mail.Mailer;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Bitwarden.Server.Sdk.Environment;
using Microsoft.Extensions.Logging;
using OrganizationEntity = Bit.Core.AdminConsole.Entities.Organization;

namespace Bit.Subscriptions.Organization.Commands;

internal sealed class AutoscalePrivilegedControlsSeatsCommand(
    IPrivilegedControlsSeatChangeSetFactory seatChangeSetFactory,
    IUpdateOrganizationSubscriptionCommand updateOrganizationSubscriptionCommand,
    IOrganizationService organizationService,
    IOrganizationUserRepository organizationUserRepository,
    IMailer mailer,
    IGlobalSettings globalSettings,
    IBitwardenEnvironment environment,
    ILogger<AutoscalePrivilegedControlsSeatsCommand> logger) : IAutoscalePrivilegedControlsSeatsCommand
{
    public async Task<AutoscalePrivilegedControlsSeatsResult> Run(OrganizationEntity organization, int seatsToAdd)
    {
        if (environment.SelfHosted)
        {
            throw new BadRequestException("Cannot update subscription on a self-hosted instance.");
        }

        if (seatsToAdd < 1)
        {
            throw new BadRequestException("At least one Privileged Controls seat must be added.");
        }

        if (!organization.UsePam || organization.PamSeats.GetValueOrDefault() < 1)
        {
            throw new BadRequestException("Your organization doesn't have Privileged Controls.");
        }

        if (string.IsNullOrWhiteSpace(organization.GatewaySubscriptionId))
        {
            throw new BadRequestException("No subscription found.");
        }

        var seatsNeeded =
            await organizationUserRepository.GetOccupiedPamSeatCountByOrganizationIdAsync(organization.Id) + seatsToAdd;

        if (seatsNeeded > organization.PamSeats)
        {
            if (organization.MaxAutoscalePamSeats < seatsNeeded)
            {
                return new AutoscalePrivilegedControlsSeatsResult(false, organization.MaxAutoscalePamSeats);
            }

            var changeSet = await seatChangeSetFactory.CreateAsync(organization, seatsNeeded);
            (await updateOrganizationSubscriptionCommand.Run(organization, changeSet)).GetValueOrThrowHttpException();

            organization.PamSeats = seatsNeeded;
            await organizationService.ReplaceAndUpdateCacheAsync(organization);
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
                Subject = $"{organization.DisplayName()} Privileged Controls seat limit reached",
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
