using System;
using Bit.Core.Models.Table;
using Bit.Core.Enums;
using Bit.Core.Models.Api;

namespace Bit.Web.Models.Api
{
    public class UserKeyResponseModel : ResponseModel
    {
        public UserKeyResponseModel(Guid id, string key)
            : base("userKey")
        {
            UserId = id.ToString();
            PublicKey = key;
        }

        public string UserId { get; set; }
        public string PublicKey { get; set; }
    }
}
