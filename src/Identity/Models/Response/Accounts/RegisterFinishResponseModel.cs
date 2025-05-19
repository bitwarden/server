using Bit.Core.Models.Api;

namespace Bit.Identity.Models.Response.Accounts;

public class RegisterFinishResponseModel : ResponseModel
{
    public RegisterFinishResponseModel()
        : base("registerFinish")
    {
        // We are setting this to an empty string so that old mobile clients don't break, as they reqiure a non-null value.
        // This will be cleaned up in https://bitwarden.atlassian.net/browse/PM-21720.
        CaptchaBypassToken = string.Empty;
    }

    public string CaptchaBypassToken { get; set; }

}
