using System.Collections.Generic;
using Bit.Core.Models.Data;
using Bit.Core.Models.Table;
using Newtonsoft.Json;

namespace Bit.Scim.Models
{
    public class ScimUser : ScimResource
    {
        public ScimUser() { }

        public ScimUser(OrganizationUserUserDetails userDetails)
        {
            Id = userDetails.Id.ToString();
            ExternalId = userDetails.ExternalId;
            UserName = userDetails.Email;
            Name = new ScimName
            {
                Formatted = userDetails.Name
            };
            DisplayName = userDetails.Name;
            Active = true;
            Emails = new List<ScimMultiValuedAttribute> {
                new ScimMultiValuedAttribute { Type =  "work", Value = userDetails.Email } };
            Meta = new ScimResourceMetadata("User");
        }

        public ScimUser(OrganizationUser orgUser)
        {
            Id = orgUser.Id.ToString();
            ExternalId = orgUser.ExternalId;
            UserName = orgUser.Email;
            Active = true;
            Emails = new List<ScimMultiValuedAttribute> {
                new ScimMultiValuedAttribute { Type =  "work", Value = orgUser.Email } };
            Meta = new ScimResourceMetadata("User");
        }

        public override string SchemaIdentifier => Constants.Schemas.User;
        [JsonProperty("userName")]
        public string UserName { get; set; }
        [JsonProperty("name")]
        public ScimName Name { get; set; }
        [JsonProperty("displayName")]
        public string DisplayName { get; set; }
        [JsonProperty("active")]
        public bool Active { get; set; }
        [JsonProperty("emails")]
        public IEnumerable<ScimMultiValuedAttribute> Emails { get; set; }
        [JsonProperty("groups")]
        public IEnumerable<ScimMultiValuedAttribute> Groups { get; set; }

        public class ScimName
        {
            [JsonProperty("formatted")]
            public string Formatted { get; set; }
            [JsonProperty("familyName")]
            public string FamilyName { get; set; }
            [JsonProperty("givenName")]
            public string GivenName { get; set; }
        }
    }
}
