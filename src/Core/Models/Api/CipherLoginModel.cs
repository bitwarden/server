using System.ComponentModel.DataAnnotations;
using Bit.Core.Utilities;
using Bit.Core.Enums;
using System.Collections.Generic;
using System.Linq;
using Bit.Core.Models.Data;

namespace Bit.Core.Models.Api
{
    public class CipherLoginModel
    {
        public CipherLoginModel(CipherLoginData data)
        {
            Uris = data.Uris.Select(u => new LoginApiUriModel(u));
            Username = data.Username;
            Password = data.Password;
            Totp = data.Totp;
        }

        [EncryptedString]
        [StringLength(10000)]
        public string Uri
        {
            get => Uris?.FirstOrDefault()?.Uri;
            set
            {
                if(Uris == null)
                {
                    Uris = new List<LoginApiUriModel>();
                }

                Uris.Append(new LoginApiUriModel(value));
            }
        }
        public IEnumerable<LoginApiUriModel> Uris { get; set; }
        [EncryptedString]
        [StringLength(1000)]
        public string Username { get; set; }
        [EncryptedString]
        [StringLength(1000)]
        public string Password { get; set; }
        [EncryptedString]
        [StringLength(1000)]
        public string Totp { get; set; }

        public class LoginApiUriModel
        {
            public LoginApiUriModel() { }

            public LoginApiUriModel(string uri)
            {
                Uri = uri;
                MatchType = UriMatchType.BaseDomain;
            }

            public LoginApiUriModel(CipherLoginData.LoginDataUriModel uri)
            {
                Uri = uri.Uri;
                MatchType = uri.MatchType;
            }

            [EncryptedString]
            [StringLength(10000)]
            public string Uri { get; set; }
            public UriMatchType MatchType { get; set; }
        }
    }
}
