namespace Bit.Core.Models.Api.Response.Accounts;

public interface ICaptchaProtectedResponseModel
{
    public string CaptchaBypassToken { get; set; }
}
