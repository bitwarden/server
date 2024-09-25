using Bit.Core.Models.StaticStore;

namespace Bit.Core.Billing.Models.Sales;

#nullable enable

public class SubscriptionSetup
{
    public required Plan Plan { get; init; }
    public required PasswordManager PasswordManagerOptions { get; init; }
    public SecretsManager? SecretsManagerOptions { get; init; }

    public class PasswordManager
    {
        public required int Seats { get; init; }
        public short? Storage { get; init; }
        public bool? PremiumAccess { get; init; }
    }

    public class SecretsManager
    {
        public required int Seats { get; init; }
        public int? ServiceAccounts { get; init; }
    }
}
