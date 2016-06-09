using System;
using Bit.Core.Domains;
using Newtonsoft.Json;

namespace Bit.Api.Models
{
    public class SiteDataModel
    {
        public SiteDataModel() { }

        public SiteDataModel(SiteRequestModel site)
        {
            Name = site.Name;
            Uri = site.Uri;
            Username = site.Username;
            Password = site.Password;
            Notes = site.Notes;
        }

        public SiteDataModel(CipherRequestModel cipher)
        {
            Name = cipher.Name;
            Uri = cipher.Uri;
            Username = cipher.Username;
            Password = cipher.Password;
            Notes = cipher.Notes;
        }

        public SiteDataModel(Cipher cipher)
        {
            if(cipher.Type != Core.Enums.CipherType.Site)
            {
                throw new ArgumentException("Cipher is not correct type.");
            }

            var data = JsonConvert.DeserializeObject<SiteDataModel>(cipher.Data);

            Name = data.Name;
            Uri = data.Uri;
            Username = data.Username;
            Password = data.Password;
            Notes = data.Notes;
        }

        public string Name { get; set; }
        public string Uri { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public string Notes { get; set; }
    }
}
