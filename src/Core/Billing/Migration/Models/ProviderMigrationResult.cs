using Bit.Core.Billing.Entities;

namespace Bit.Core.Billing.Migration.Models;

public class ProviderMigrationResult
{
    public Guid ProviderId { get; set; }
    public string ProviderName { get; set; }
    public string Result { get; set; }
    public List<ClientMigrationResult> Clients { get; set; }
}

public class ClientMigrationResult
{
    public Guid OrganizationId { get; set; }
    public string OrganizationName { get; set; }
    public string Result { get; set; }
    public ClientPreviousState PreviousState { get; set; }
}

public class ClientPreviousState
{
    public ClientPreviousState() { }

    public ClientPreviousState(ClientOrganizationMigrationRecord migrationRecord)
    {
        PlanType = migrationRecord.PlanType.ToString();
        Seats = migrationRecord.Seats;
        MaxStorageGb = migrationRecord.MaxStorageGb;
        GatewayCustomerId = migrationRecord.GatewayCustomerId;
        GatewaySubscriptionId = migrationRecord.GatewaySubscriptionId;
        ExpirationDate = migrationRecord.ExpirationDate;
        MaxAutoscaleSeats = migrationRecord.MaxAutoscaleSeats;
        Status = migrationRecord.Status.ToString();
    }

    public string PlanType { get; set; }
    public int Seats { get; set; }
    public short? MaxStorageGb { get; set; }
    public string GatewayCustomerId { get; set; } = null!;
    public string GatewaySubscriptionId { get; set; } = null!;
    public DateTime? ExpirationDate { get; set; }
    public int? MaxAutoscaleSeats { get; set; }
    public string Status { get; set; }
}
