namespace Bit.Core.Models.Mail.Auth;

/// <summary>
/// Send email OTP view model
/// </summary>
public class DefaultEmailOtpViewModel : BaseMailModel
{
    public string? Token { get; set; }
    public string? TheDate { get; set; }
    public string? TheTime { get; set; }
    public string? TimeZone { get; set; }
}
