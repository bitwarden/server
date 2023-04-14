using Bit.Core.Auth.Models.Business;
using Bit.Core.Entities;

namespace Bit.Identity.IdentityServer;

public class CustomValidatorRequestContext
{
    public User User { get; set; }
    public bool KnownDevice { get; set; }
    public CaptchaResponse CaptchaResponse { get; set; }
}
