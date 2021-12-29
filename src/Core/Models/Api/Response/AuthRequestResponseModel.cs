using System;
using Bit.Core.Models.Table;

namespace Bit.Core.Models.Api
{
    public class AuthRequestResponseModel : ResponseModel
    {
        public AuthRequestResponseModel(AuthRequest authRequest, string obj = "auth-request")
            : base(obj)
        {
            if (authRequest == null)
            {
                throw new ArgumentNullException(nameof(authRequest));
            }

            Id = authRequest.Id.ToString();
            PublicKey = authRequest.PublicKey;
            Key = authRequest.Key;
        }

        public string Id { get; set; }
        public string PublicKey { get; set; }
        public string Key { get; set; }
    }
}
