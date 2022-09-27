namespace Bit.Core.Models.Api.Response.Accounts;

public class RegisterResponseModel : ResponseModel, ICaptchaProtectedResponseModel
{
    public RegisterResponseModel(string captchaBypassToken)
        : base("register")
    {
        CaptchaBypassToken = captchaBypassToken;
    }

    public string CaptchaBypassToken { get; set; }
}
