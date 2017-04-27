using System;
using Core.Models.Data;
using Bit.Core.Models.Table;

namespace Bit.Core.Models.Api
{
    public class LoginResponseModel : ResponseModel
    {
        public LoginResponseModel(Cipher cipher, string obj = "login")
            : base(obj)
        {
            if(cipher == null)
            {
                throw new ArgumentNullException(nameof(cipher));
            }

            if(cipher.Type != Enums.CipherType.Login)
            {
                throw new ArgumentException(nameof(cipher.Type));
            }

            var data = new LoginDataModel(cipher);

            Id = cipher.Id.ToString();
            OrganizationId = cipher.OrganizationId?.ToString();
            Name = data.Name;
            Uri = data.Uri;
            Username = data.Username;
            Password = data.Password;
            Notes = data.Notes;
            RevisionDate = cipher.RevisionDate;
        }

        public LoginResponseModel(CipherDetails cipher, string obj = "login")
            : this(cipher as Cipher, obj)
        {
            FolderId = cipher.FolderId?.ToString();
            Favorite = cipher.Favorite;
        }

        public string Id { get; set; }
        public string OrganizationId { get; set; }
        public string FolderId { get; set; }
        public bool Favorite { get; set; }
        public string Name { get; set; }
        public string Uri { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public string Notes { get; set; }
        public DateTime RevisionDate { get; set; }
    }

    public class LoginDetailsResponseModel : LoginResponseModel
    {
        public LoginDetailsResponseModel(CipherFullDetails cipher)
            : base(cipher, "loginDetails")
        {
            Edit = cipher.Edit;
        }

        public bool Edit { get; set; }
    }
}
