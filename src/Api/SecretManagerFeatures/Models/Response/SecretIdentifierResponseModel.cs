using Bit.Core.Entities;
using Bit.Core.Models.Api;

namespace Bit.Api.SecretManagerFeatures.Models.Response
{
    public class SecretIdentifierResponseModel : ResponseModel
    {
        public SecretIdentifierResponseModel(Secret secret, string obj = "secret")
            : base(obj)
        {
            if (secret == null)
            {
                throw new ArgumentNullException(nameof(secret));
            }

            Id = secret.Id.ToString();
            OrganizationId = secret.OrganizationId.ToString();
            Key = secret.Key;
            CreationDate = secret.CreationDate;
            RevisionDate = secret.RevisionDate;
        }

        public string Id { get; set; }

        public string OrganizationId { get; set; }

        public string Key { get; set; }

        public DateTime CreationDate { get; set; }

        public DateTime RevisionDate { get; set; }
    }
}

