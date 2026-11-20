using Bit.Core;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers;
using Bit.Core.AdminConsole.Utilities.v2;
using Bit.Core.Billing.Organizations;
using Bit.Core.Billing.Organizations.Queries;
using Bit.Core.Repositories;
using Bitwarden.Server.Sdk.Environment;
using Bitwarden.Server.Sdk.Features;
using OrganizationEntity = Bit.Core.AdminConsole.Entities.Organization;

namespace Bit.Subscriptions.Organization.Queries;

internal sealed class ValidatePrivilegedControlsSeatReservationQuery(
    ICountNewPamSeatsRequiredQuery countNewPamSeatsRequiredQuery,
    IOrganizationUserRepository organizationUserRepository,
    IIsOnPrivilegedControlsSalesInitiatedTrialQuery isOnTrialQuery,
    IBitwardenEnvironment environment,
    IFeatureService featureService) : IValidatePrivilegedControlsSeatReservationQuery
{
    public async Task<Error?> RunAsync(OrganizationEntity organization, int seatsRequested, int passwordManagerSeatsToAdd)
    {
        // A null seat count means no seat count applies to the organization yet.
        if (seatsRequested < 1 || !organization.UsePam || organization.PamSeats is null)
        {
            return null;
        }

        if (environment.SelfHosted)
        {
            return await CountNewPamSeatsRequiredAsync(organization, seatsRequested) > 0
                ? new CannotAutoscalePrivilegedControlsSeatsOnSelfHostError()
                : null;
        }

        // With the flag off, Privileged Controls is sales-assisted: seats are a hard limit and nothing autoscales.
        if (!featureService.IsEnabled(FeatureFlagKeys.PM39883_Pam_Billing_Support))
        {
            return await CountNewPamSeatsRequiredAsync(organization, seatsRequested) > 0
                ? new NotEnoughPrivilegedControlsSeatsError()
                : null;
        }

        // Autoscaling can't add seats to an organization that has none, so zero seats means none are free.
        if (organization.PamSeats < 1)
        {
            return new PrivilegedControlsSeatLimitReachedError();
        }

        var seatsNeeded =
            await organizationUserRepository.GetOccupiedPamSeatCountByOrganizationIdAsync(organization.Id) + seatsRequested;

        if (seatsNeeded <= organization.PamSeats)
        {
            return null;
        }

        if (organization.MaxAutoscalePamSeats < seatsNeeded)
        {
            return new PrivilegedControlsSeatLimitReachedError();
        }

        // The trial check reads Stripe, so it runs only once the seat comparison has failed.
        if (organization.Seats is { } seats
            && seatsNeeded > seats + passwordManagerSeatsToAdd
            && !await isOnTrialQuery.Run(organization))
        {
            return new PrivilegedControlsSeatsMustNotExceedPasswordManagerSeatsError();
        }

        return null;
    }

    private Task<int> CountNewPamSeatsRequiredAsync(OrganizationEntity organization, int seatsRequested) =>
        countNewPamSeatsRequiredQuery.CountNewPamSeatsRequiredAsync(organization, seatsRequested);
}
