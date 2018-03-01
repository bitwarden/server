using System.Collections.Generic;
using System.Linq;
using Bit.Core.Enums;
using Bit.Core.Models.Api;

namespace Bit.Core.Models.Data
{
    public class CipherLoginData : CipherData
    {
        private string _uri;

        public CipherLoginData() { }

        public CipherLoginData(CipherRequestModel cipher)
            : base(cipher)
        {
            Uris = cipher.Login.Uris?.Where(u => u != null).Select(u => new CipherLoginUriData(u));
            Username = cipher.Login.Username;
            Password = cipher.Login.Password;
            Totp = cipher.Login.Totp;
        }

        public string Uri
        {
            get => Uris?.FirstOrDefault()?.Uri ?? _uri;
            set { _uri = value; }
        }
        public IEnumerable<CipherLoginUriData> Uris { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public string Totp { get; set; }

        public class CipherLoginUriData
        {
            public CipherLoginUriData() { }

            public CipherLoginUriData(CipherLoginModel.CipherLoginUriModel uri)
            {
                Uri = uri.Uri;
                Match = uri.Match;
            }

            public string Uri { get; set; }
            public UriMatchType? Match { get; set; } = null;
        }
    }
}
