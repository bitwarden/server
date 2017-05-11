using System;
using Bit.Core.Models.Data;
using Bit.Core.Enums;

namespace Bit.Core.Models.Api
{
    public class CollectionUserResponseModel : ResponseModel
    {
        public CollectionUserResponseModel(CollectionUserDetails collectionUser)
            : base("collectionUser")
        {
            if(collectionUser == null)
            {
                throw new ArgumentNullException(nameof(collectionUser));
            }

            OrganizationUserId = collectionUser.OrganizationUserId.ToString();
            AccessAll = collectionUser.AccessAll;
            Name = collectionUser.Name;
            Email = collectionUser.Email;
            Type = collectionUser.Type;
            Status = collectionUser.Status;
            ReadOnly = collectionUser.ReadOnly;
        }

        public string OrganizationUserId { get; set; }
        public bool AccessAll { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public OrganizationUserType Type { get; set; }
        public OrganizationUserStatusType Status { get; set; }
        public bool ReadOnly { get; set; }
    }
}
