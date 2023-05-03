namespace Bit.Core.Auth.Models.Api.Response.Accounts;

public interface ICaptchaProtectedResponseModel
{
    public string CaptchaBypassToken { get; set; }
}
