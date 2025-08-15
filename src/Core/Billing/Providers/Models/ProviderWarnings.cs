namespace Bit.Core.Billing.Providers.Models;

public class ProviderWarnings
{
    public SuspensionWarning? Suspension { get; set; }
    public TaxIdWarning? TaxId { get; set; }

    public record SuspensionWarning
    {
        public required string Resolution { get; set; }
        public DateTime? SubscriptionCancelsAt { get; set; }
    }

    public record TaxIdWarning
    {
        public required string Type { get; set; }
    }
}
