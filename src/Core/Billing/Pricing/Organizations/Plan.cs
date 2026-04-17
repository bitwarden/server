namespace Bit.Core.Billing.Pricing.Organizations;

public class Plan
{
    public required string LookupKey { get; set; }
    public required string Name { get; set; }
    public required string Tier { get; set; }
    public string? Cadence { get; set; }
    public int? LegacyYear { get; set; }
    public bool Available { get; set; }
    public required Feature[] Features { get; set; }
    public required Purchasable Seats { get; set; }
    public Scalable? ManagedSeats { get; set; }
    public Scalable? Storage { get; set; }
    public SecretsManagerPurchasables? SecretsManager { get; set; }
    public int? TrialPeriodDays { get; set; }
    public required string[] CanUpgradeTo { get; set; }
    public required Dictionary<string, string> AdditionalData { get; set; }
}

public class SecretsManagerPurchasables
{
    public required FreeOrScalable Seats { get; set; }
    public required FreeOrScalable ServiceAccounts { get; set; }
}
