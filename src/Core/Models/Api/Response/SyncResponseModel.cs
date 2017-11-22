using System;
using System.Collections.Generic;
using System.Linq;
using Bit.Core.Models.Data;
using Bit.Core.Models.Table;
using Core.Models.Data;

namespace Bit.Core.Models.Api
{
    public class SyncResponseModel : ResponseModel
    {
        public SyncResponseModel(
            GlobalSettings globalSettings,
            User user,
            IEnumerable<OrganizationUserOrganizationDetails> organizationUserDetails,
            IEnumerable<Folder> folders,
            IEnumerable<Collection> collections,
            IEnumerable<CipherDetails> ciphers,
            IDictionary<Guid, IGrouping<Guid, CollectionCipher>> collectionCiphersDict)
            : base("sync")
        {
            Profile = new ProfileResponseModel(user, organizationUserDetails);
            Folders = folders.Select(f => new FolderResponseModel(f));
            Ciphers = ciphers.Select(c => new CipherDetailsResponseModel(c, globalSettings, collectionCiphersDict));
            Collections = collections.Select(c => new CollectionResponseModel(c));
            Domains = new DomainsResponseModel(user, false);
        }

        public ProfileResponseModel Profile { get; set; }
        public IEnumerable<FolderResponseModel> Folders { get; set; }
        public IEnumerable<CollectionResponseModel> Collections { get; set; }
        public IEnumerable<CipherDetailsResponseModel> Ciphers { get; set; }
        public DomainsResponseModel Domains { get; set; }
    }
}
