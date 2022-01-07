using System;
using Bit.Core.Models.Api;
using Bit.Core.Models.Table;

namespace Bit.Api.Models.Response
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
            MasterPasswordHash = authRequest.MasterPasswordHash;
        }

        public string Id { get; set; }
        public string PublicKey { get; set; }
        public string Key { get; set; }
        public string MasterPasswordHash { get; set; }
    }
}
