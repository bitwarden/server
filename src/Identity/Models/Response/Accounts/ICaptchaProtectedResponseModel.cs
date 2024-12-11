namespace Bit.Identity.Models.Response.Accounts;

public interface ICaptchaProtectedResponseModel
{
    public string CaptchaBypassToken { get; set; }
}
