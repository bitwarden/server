using Bit.Core.Platform.Services;

namespace Bit.Core.Auth.UserFeatures.Registration.VerifyEmail;

public class VerifyEmail(string url) : BaseMailModel2
{
    public override string Subject { get; set; } = "Verify Your Email";
    public string Url { get; } = url;
}
