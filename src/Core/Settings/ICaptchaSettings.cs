namespace Bit.Core.Settings;

public interface ICaptchaSettings
{
    bool ForceCaptchaRequired { get; set; }
    string HCaptchaSecretKey { get; set; }
    string HCaptchaSiteKey { get; set; }
    int MaximumFailedLoginAttempts { get; set; }
    double MaybeBotScoreThreshold { get; set; }
    double IsBotScoreThreshold { get; set; }
}
