using System;
using System.Collections.Generic;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;

namespace Bit.Scim.Models
{
    public class ScimUserResponseModel : BaseScimUserModel
    {
        public ScimUserResponseModel()
            : base(true)
        {
            Meta = new ScimMetaModel("User");
            Groups = new List<string>();
        }

        public ScimUserResponseModel(OrganizationUserUserDetails orgUser)
            : this()
        {
            Id = orgUser.Id.ToString();
            ExternalId = orgUser.ExternalId;
            UserName = orgUser.Email;
            DisplayName = orgUser.Name;
            Emails = new List<EmailModel> { new EmailModel(orgUser.Email) };
            Name = new NameModel(orgUser.Name);
            Active = true;
        }

        public string Id { get; set; }
        public string ExternalId { get; set; }
        public ScimMetaModel Meta { get; private set; }
    }
}
