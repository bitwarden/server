namespace Bit.Core.Settings;

public class CaptchaSettings
{
    public bool ForceCaptchaRequired { get; set; } = false;
    public string HCaptchaSecretKey { get; set; }
    public string HCaptchaSiteKey { get; set; }
    public int MaximumFailedLoginAttempts { get; set; }
    public double MaybeBotScoreThreshold { get; set; } = double.MaxValue;
    public double IsBotScoreThreshold { get; set; } = double.MaxValue;
}
