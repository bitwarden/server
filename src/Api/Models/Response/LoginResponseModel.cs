using System;
using Bit.Core.Domains;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;

namespace Bit.Api.Models
{
    public class LoginResponseModel : ResponseModel
    {
        public LoginResponseModel(Cipher cipher, Guid userId)
            : base("login")
        {
            if(cipher == null)
            {
                throw new ArgumentNullException(nameof(cipher));
            }

            if(cipher.Type != Core.Enums.CipherType.Login)
            {
                throw new ArgumentException(nameof(cipher.Type));
            }

            var data = new LoginDataModel(cipher);

            Id = cipher.Id.ToString();
            FolderId = cipher.FolderId?.ToString();
            Favorite = cipher.Favorite;
            Name = data.Name;
            Uri = data.Uri;
            Username = data.Username;
            Password = data.Password;
            Notes = data.Notes;
            RevisionDate = cipher.RevisionDate;
        }

        public string Id { get; set; }
        public string FolderId { get; set; }
        public bool Favorite { get; set; }
        public string Name { get; set; }
        public string Uri { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public string Notes { get; set; }
        public string Key { get; set; }
        public DateTime RevisionDate { get; set; }

        // Expandables
        public FolderResponseModel Folder { get; set; }
    }
}
