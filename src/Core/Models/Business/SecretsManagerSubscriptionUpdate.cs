using Bit.Core.Entities;
using Bit.Core.Exceptions;

namespace Bit.Core.Models.Business;

public class SecretsManagerSubscriptionUpdate
{
    public Guid OrganizationId { get; }

    /// <summary>
    /// The seats to be added or removed from the organization
    /// </summary>
    public int SmSeatsAdjustment { get; }

    /// <summary>
    /// The total seats the organization will have after the update, including any base seats included in the plan
    /// </summary>
    public int SmSeats { get; }

    /// <summary>
    /// The seats the organization will have after the update, excluding the base seats included in the plan
    /// Usually this is what the organization is billed for
    /// </summary>
    public int SmSeatsExcludingBase { get; }

    /// <summary>
    /// The new autoscale limit for seats, expressed as a total (not an adjustment).
    /// This may or may not be the same as the current autoscale limit.
    /// </summary>
    public int? MaxAutoscaleSmSeats { get; }

    /// <summary>
    /// The service accounts to be added or removed from the organization
    /// </summary>
    public int SmServiceAccountsAdjustment { get; }

    /// <summary>
    /// The total service accounts the organization will have after the update, including the base service accounts
    /// included in the plan
    /// </summary>
    public int SmServiceAccounts { get; }

    /// <summary>
    /// The seats the organization will have after the update, excluding the base seats included in the plan
    /// Usually this is what the organization is billed for
    /// </summary>
    public int SmServiceAccountsExcludingBase { get; }

    /// <summary>
    /// The new autoscale limit for service accounts, expressed as a total (not an adjustment).
    /// This may or may not be the same as the current autoscale limit.
    /// </summary>
    public int? MaxAutoscaleSmServiceAccounts { get; }

    public bool SmSeatsChanged => SmSeatsAdjustment != 0;
    public bool SmServiceAccountsChanged => SmServiceAccountsAdjustment != 0;
    public bool MaxAutoscaleSmSeatsChanged { get; }
    public bool MaxAutoscaleSmServiceAccountsChanged { get; }

    public SecretsManagerSubscriptionUpdate(
        Organization organization,
        int seatAdjustment, int? maxAutoscaleSeats,
        int serviceAccountAdjustment, int? maxAutoscaleServiceAccounts)
    {
        var secretsManagerPlan = Utilities.StaticStore.GetSecretsManagerPlan(organization.PlanType);
        if (secretsManagerPlan == null)
        {
            throw new NotFoundException("Invalid Secrets Manager plan.");
        }

        var newTotalSeats = organization.SmSeats.GetValueOrDefault() + seatAdjustment;
        var autoscaleSeatsChanged = maxAutoscaleSeats.HasValue
                                    && maxAutoscaleSeats != organization.MaxAutoscaleSmSeats.GetValueOrDefault();

        var newTotalServiceAccounts = organization.SmServiceAccounts.GetValueOrDefault() + serviceAccountAdjustment;
        var autoscaleServiceAccountsChanged = maxAutoscaleServiceAccounts.HasValue
                                              && maxAutoscaleServiceAccounts != organization.MaxAutoscaleSmServiceAccounts.GetValueOrDefault();

        OrganizationId = organization.Id;

        SmSeats = newTotalSeats;
        SmSeatsAdjustment = seatAdjustment;
        SmSeatsExcludingBase = newTotalSeats - secretsManagerPlan.BaseSeats;
        MaxAutoscaleSmSeats = maxAutoscaleSeats;

        SmServiceAccounts = newTotalServiceAccounts;
        SmServiceAccountsAdjustment = serviceAccountAdjustment;
        SmServiceAccountsExcludingBase = newTotalServiceAccounts - secretsManagerPlan.BaseServiceAccount.GetValueOrDefault();
        MaxAutoscaleSmServiceAccounts = maxAutoscaleServiceAccounts;

        MaxAutoscaleSmSeatsChanged = autoscaleSeatsChanged;
        MaxAutoscaleSmServiceAccountsChanged = autoscaleServiceAccountsChanged;
    }

    public SecretsManagerSubscriptionUpdate(Organization organization, int serviceAccountAdjustment)
        : this(organization, seatAdjustment: 0, maxAutoscaleSeats: null, serviceAccountAdjustment, maxAutoscaleServiceAccounts: null)
    {
    }
}
