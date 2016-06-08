namespace Bit.Api.Models
{
    public class CipherDataModel
    {
        public CipherDataModel() { }

        public CipherDataModel(CipherRequestModel cipher)
        {
            Name = cipher.Name;
            Uri = cipher.Uri;
            Username = cipher.Username;
            Password = cipher.Password;
            Notes = cipher.Notes;
        }

        public CipherDataModel(SiteRequestModel site)
        {
            Name = site.Name;
            Uri = site.Uri;
            Username = site.Username;
            Password = site.Password;
            Notes = site.Notes;
        }

        public CipherDataModel(FolderRequestModel folder)
        {
            Name = folder.Name;
        }

        public string Name { get; set; }
        public string Uri { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public string Notes { get; set; }
    }
}
