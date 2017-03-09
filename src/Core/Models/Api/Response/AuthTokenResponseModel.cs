using System;
using Bit.Core.Models.Table;

namespace Bit.Core.Models.Api
{
    public class AuthTokenResponseModel : ResponseModel
    {
        public AuthTokenResponseModel(string token, User user = null)
            : base("authToken")
        {
            Token = token;
            Profile = user == null ? null : new ProfileResponseModel(user, null);
        }

        public string Token { get; set; }
        public ProfileResponseModel Profile { get; set; }
    }
}
