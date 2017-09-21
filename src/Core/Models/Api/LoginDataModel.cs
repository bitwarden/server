using System;
using Bit.Core.Models.Table;
using Newtonsoft.Json;

namespace Bit.Core.Models.Api
{
    public class LoginDataModel : CipherDataModel
    {
        public LoginDataModel() { }

        public LoginDataModel(LoginRequestModel login)
        {
            Name = login.Name;
            Notes = login.Notes;
            Fields = login.Fields;

            Uri = login.Uri;
            Username = login.Username;
            Password = login.Password;
            Totp = login.Totp;
        }

        public LoginDataModel(CipherRequestModel cipher)
        {
            Name = cipher.Name;
            Notes = cipher.Notes;
            Fields = cipher.Fields;

            if(cipher.Login == null)
            {
                Uri = cipher.Uri;
                Username = cipher.Username;
                Password = cipher.Password;
                Totp = cipher.Totp;
            }
            else
            {
                Uri = cipher.Login.Uri;
                Username = cipher.Login.Username;
                Password = cipher.Login.Password;
                Totp = cipher.Login.Totp;
            }
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
