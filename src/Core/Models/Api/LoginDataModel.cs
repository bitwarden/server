using System;
using Bit.Core.Models.Table;
using Newtonsoft.Json;

namespace Bit.Core.Models.Api
{
    public class LoginDataModel : CipherDataModel
    {
        public LoginDataModel() { }

        public LoginDataModel(CipherRequestModel cipher)
        {
            Name = cipher.Name;
            Notes = cipher.Notes;
            Fields = cipher.Fields;

            Uri = cipher.Login.Uri;
            Username = cipher.Login.Username;
            Password = cipher.Login.Password;
            Totp = cipher.Login.Totp;
        }

        public LoginDataModel(Cipher cipher)
        {
            if(cipher.Type != Enums.CipherType.Login)
            {
                throw new ArgumentException("Cipher is not correct type.");
            }

            var data = JsonConvert.DeserializeObject<LoginDataModel>(cipher.Data);

            Name = data.Name;
            Notes = data.Notes;
            Fields = data.Fields;

            Uri = data.Uri;
            Username = data.Username;
            Password = data.Password;
            Totp = data.Totp;
        }

        public string Uri { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public string Totp { get; set; }
    }
}
