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
    public int? MaxAutoscaleServiceAccounts { get; set; }

    public bool AdjustingSeats => SeatAdjustment != 0;
    public bool AdjustingServiceAccounts => ServiceAccountsAdjustment != 0;
    public bool AutoscaleSeats { get; set; }
    public bool AutoscaleServiceAccounts { get; set; }
}
