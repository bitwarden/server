namespace Bit.Core.Models.Business;

public class SecretsManagerSubscriptionUpdate
{
    public Guid OrganizationId { get; set; }

    /// <summary>
    /// The seats to be added or removed from the organization
    /// </summary>
    public int SmSeatsAdjustment { get; set; }

    /// <summary>
    /// The total seats the organization will have after the update, including any base seats included in the plan
    /// </summary>
    public int SmSeats { get; set; }

    /// <summary>
    /// The seats the organization will have after the update, excluding the base seats included in the plan
    /// Usually this is what the organization is billed for
    /// </summary>
    public int SmSeatsExcludingBase { get; set; }

    /// <summary>
    /// The new autoscale limit for seats, expressed as a total (not an adjustment).
    /// This may or may not be the same as the current autoscale limit.
    /// </summary>
    public int? MaxAutoscaleSmSeats { get; set; }

    /// <summary>
    /// The service accounts to be added or removed from the organization
    /// </summary>
    public int SmServiceAccountsAdjustment { get; set; }

    /// <summary>
    /// The total service accounts the organization will have after the update, including the base service accounts
    /// included in the plan
    /// </summary>
    public int SmServiceAccounts { get; set; }

    /// <summary>
    /// The seats the organization will have after the update, excluding the base seats included in the plan
    /// Usually this is what the organization is billed for
    /// </summary>
    public int SmServiceAccountsExcludingBase { get; set; }
    
    /// <summary>
    /// The new autoscale limit for service accounts, expressed as a total (not an adjustment).
    /// This may or may not be the same as the current autoscale limit.
    /// </summary>
    public int? MaxAutoscaleSmServiceAccounts { get; set; }

    public bool SmSeatsChanged => SmSeatsAdjustment != 0;
    public bool SmServiceAccountsChanged => SmServiceAccountsAdjustment != 0;
    public bool MaxAutoscaleSmSeatsChanged { get; set; }
    public bool MaxAutoscaleSmServiceAccountsChanged { get; set; }
}
