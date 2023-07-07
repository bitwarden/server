namespace Bit.Core.Models.Business;

public class SecretsManagerSubscriptionUpdate
{
    public Guid OrganizationId { get; set; }

    /// <summary>
    /// The seats to be added or removed from the organization
    /// </summary>
    public int SeatAdjustment { get; set; }

    /// <summary>
    /// The total seats the organization will have after the update, including any base seats included in the plan
    /// </summary>
    public int NewTotalSeats { get; set; }

    /// <summary>
    /// The seats the organization will have after the update, excluding the base seats included in the plan
    /// Usually this is what the organization is billed for
    /// </summary>
    public int NewAdditionalSeats { get; set; }

    /// <summary>
    /// The new autoscale limit for seats, expressed as a total (not an adjustment).
    /// This may or may not be the same as the current autoscale limit.
    /// </summary>
    public int? MaxAutoscaleSeats { get; set; }

    /// <summary>
    /// The service accounts to be added or removed from the organization
    /// </summary>
    public int ServiceAccountsAdjustment { get; set; }

    /// <summary>
    /// The total service accounts the organization will have after the update, including the base service accounts
    /// included in the plan
    /// </summary>
    public int NewTotalServiceAccounts { get; set; }

    /// <summary>
    /// The seats the organization will have after the update, excluding the base seats included in the plan
    /// Usually this is what the organization is billed for
    /// </summary>
    public int NewAdditionalServiceAccounts { get; set; }
    
    /// <summary>
    /// The new autoscale limit for service accounts, expressed as a total (not an adjustment).
    /// This may or may not be the same as the current autoscale limit.
    /// </summary>
    public int? MaxAutoscaleServiceAccounts { get; set; }

    public bool SeatAdjustmentRequired => SeatAdjustment != 0;
    public bool ServiceAccountAdjustmentRequired => ServiceAccountsAdjustment != 0;
    public bool AutoscaleSeatAdjustmentRequired { get; set; }
    public bool AutoscaleServiceAccountAdjustmentRequired { get; set; }
}
