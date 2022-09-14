using Bit.Core.Models.Api;

namespace Bit.Identity.Models;

public class RegisterResponseModel : ResponseModel
{
    public RegisterResponseModel(string captchaBypassToken)
        : base("register")
    {
        CaptchaBypassToken = captchaBypassToken;
    }

    public string CaptchaBypassToken { get; set; }
}
