namespace Bit.Core.Auth.Models.Api;

public interface ICaptchaProtectedModel
{
    string CaptchaResponse { get; set; }
}
