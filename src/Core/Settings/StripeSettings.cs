namespace Bit.Core.Settings;
public class StripeSettings
{
    public string ApiKey { get; set; }
    public int MaxNetworkRetries { get; set; } = 2;
}
