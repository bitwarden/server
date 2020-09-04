using System;
using IdentityServer4.Models;

namespace Bit.Sso.Models
{
    public class ErrorViewModel
    {
        public ErrorMessage Error { get; set; }

        public string Message => Error?.Error;
        public string Description => Error?.ErrorDescription;
        public string RequestId => Error?.RequestId;
        public string RedirectUri => Error?.RedirectUri;
    }
}
