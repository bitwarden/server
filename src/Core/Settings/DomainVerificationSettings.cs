namespace Bit.Core.Settings;

public class DomainVerificationSettings : IDomainVerificationSettings
{
    public int VerificationInterval { get; set; } = 12;
    public int ExpirationPeriod { get; set; } = 7;
}

