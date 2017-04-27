using System;
using Bit.Core.Models.Table;
using Bit.Core.Models.Data;
using Bit.Core.Enums;

namespace Bit.Core.Models.Api
{
    public class CollectionUserResponseModel : ResponseModel
    {
        public CollectionUserResponseModel(CollectionUserUserDetails collectionUser)
            : base("collectionUser")
        {
            if(collectionUser == null)
            {
                throw new ArgumentNullException(nameof(collectionUser));
            }

            Id = collectionUser.Id?.ToString();
            OrganizationUserId = collectionUser.OrganizationUserId.ToString();
            CollectionId = collectionUser.CollectionId?.ToString();
            AccessAllCollections = collectionUser.AccessAllCollections;
            Name = collectionUser.Name;
            Email = collectionUser.Email;
            Type = collectionUser.Type;
            Status = collectionUser.Status;
            ReadOnly = collectionUser.ReadOnly;
        }

        public string Id { get; set; }
        public string OrganizationUserId { get; set; }
        public string CollectionId { get; set; }
        public bool AccessAllCollections { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public OrganizationUserType Type { get; set; }
        public OrganizationUserStatusType Status { get; set; }
        public bool ReadOnly { get; set; }
    }
}
