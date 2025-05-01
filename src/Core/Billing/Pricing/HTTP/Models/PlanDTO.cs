namespace Bit.Core.Billing.Pricing.HTTP.Models;

#nullable enable

public class PlanDTO
{
    public string LookupKey { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string Tier { get; set; } = null!;
    public string? Cadence { get; set; }
    public int? LegacyYear { get; set; }
    public bool Available { get; set; }
    public FeatureDTO[] Features { get; set; } = null!;
    public PurchasableDTO Seats { get; set; } = null!;
    public ScalableDTO? ManagedSeats { get; set; }
    public ScalableDTO? Storage { get; set; }
    public SecretsManagerPurchasablesDTO? SecretsManager { get; set; }
    public int? TrialPeriodDays { get; set; }
    public string[] CanUpgradeTo { get; set; } = null!;
    public Dictionary<string, string> AdditionalData { get; set; } = null!;
}

public class SecretsManagerPurchasablesDTO
{
    public FreeOrScalableDTO Seats { get; set; } = null!;
    public FreeOrScalableDTO ServiceAccounts { get; set; } = null!;
}
