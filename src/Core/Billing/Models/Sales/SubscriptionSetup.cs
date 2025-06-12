using Bit.Core.Billing.Enums;

namespace Bit.Core.Billing.Models.Sales;

#nullable enable

public class SubscriptionSetup
{
    public required PlanType PlanType { get; set; }
    public required PasswordManager PasswordManagerOptions { get; set; }
    public SecretsManager? SecretsManagerOptions { get; set; }
    public bool SkipTrial = false;
    public string? InitiationPath { get; set; }

    public class PasswordManager
    {
        public required int Seats { get; set; }
        public short? Storage { get; set; }
        public bool? PremiumAccess { get; set; }
    }

    public class SecretsManager
    {
        public required int Seats { get; set; }
        public int? ServiceAccounts { get; set; }
    }
}
