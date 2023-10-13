namespace Bit.Identity.Models.Response;

public interface ICaptchaProtectedResponseModel
{
    public string CaptchaBypassToken { get; set; }
}
