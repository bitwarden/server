using Bit.Core.Models.Table;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Bit.Core.Models.Api
{
    public class OrganizationUserInviteRequestModel
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; }
        [Required]
        public Enums.OrganizationUserType? Type { get; set; }
        public bool AccessAllCollections { get; set; }
        public IEnumerable<OrganizationUserCollectionRequestModel> Collections { get; set; }
    }

    public class OrganizationUserAcceptRequestModel
    {
        [Required]
        public string Token { get; set; }
    }

    public class OrganizationUserConfirmRequestModel
    {
        [Required]
        public string Key { get; set; }
    }

    public class OrganizationUserUpdateRequestModel
    {
        [Required]
        public Enums.OrganizationUserType? Type { get; set; }
        public bool AccessAllCollections { get; set; }
        public IEnumerable<OrganizationUserCollectionRequestModel> Collections { get; set; }

        public OrganizationUser ToOrganizationUser(OrganizationUser existingUser)
        {
            existingUser.Type = Type.Value;
            existingUser.AccessAllCollections = AccessAllCollections;
            return existingUser;
        }
    }

    public class OrganizationUserCollectionRequestModel
    {
        [Required]
        public string CollectionId { get; set; }
        public bool ReadOnly { get; set; }

        public CollectionUser ToCollectionUser()
        {
            var collection = new CollectionUser
            {
                ReadOnly = ReadOnly,
                CollectionId = new Guid(CollectionId)
            };

            return collection;
        }
    }
}
