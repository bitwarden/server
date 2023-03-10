namespace Bit.Core.Settings;

public interface IDomainVerificationSettings
{
    public int VerificationInterval { get; set; }
    public int ExpirationPeriod { get; set; }
}
