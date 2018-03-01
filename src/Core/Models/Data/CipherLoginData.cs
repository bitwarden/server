using System.Collections.Generic;
using System.Linq;
using Bit.Core.Enums;
using Bit.Core.Models.Api;

namespace Bit.Core.Models.Data
{
    public class CipherLoginData : CipherData
    {
        public CipherLoginData() { }

        public CipherLoginData(CipherRequestModel cipher)
            : base(cipher)
        {
            Uris = cipher.Login.Uris?.Where(u => u != null).Select(u => new LoginDataUriModel(u));
            Username = cipher.Login.Username;
            Password = cipher.Login.Password;
            Totp = cipher.Login.Totp;
        }

        public IEnumerable<LoginDataUriModel> Uris { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public string Totp { get; set; }

        public class LoginDataUriModel
        {
            public LoginDataUriModel() { }

            public LoginDataUriModel(CipherLoginModel.LoginApiUriModel uri)
            {
                Uri = uri.Uri;
                Match = uri.Match;
            }

            public string Uri { get; set; }
            public UriMatchType Match { get; set; }
        }
    }
}
