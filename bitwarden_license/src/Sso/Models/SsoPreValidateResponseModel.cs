using Microsoft.AspNetCore.Mvc;

namespace Bit.Sso.Models;

public class SsoPreValidateResponseModel : JsonResult
{
    public SsoPreValidateResponseModel(string token)
        : base(new { token }) { }
}
