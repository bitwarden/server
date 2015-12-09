using System;
using Bit.Core.Domains;

namespace Bit.Api.Models
{
    public class AuthTokenResponseModel : ResponseModel
    {
        public AuthTokenResponseModel(string token, User user = null)
            : base("authToken")
        {
            Token = token;
            Profile = user == null ? null : new ProfileResponseModel(user);
        }

        public string Token { get; set; }
        public ProfileResponseModel Profile { get; set; }
    }
}
