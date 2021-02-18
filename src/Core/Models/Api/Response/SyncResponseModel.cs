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
            bool userTwoFactorEnabled,
            IEnumerable<OrganizationUserOrganizationDetails> organizationUserDetails,
            IEnumerable<Folder> folders,
            IEnumerable<CollectionDetails> collections,
            IEnumerable<CipherDetails> ciphers,
            IDictionary<Guid, IGrouping<Guid, CollectionCipher>> collectionCiphersDict,
            bool excludeDomains,
            IEnumerable<Policy> policies,
            IEnumerable<Send> sends)
            : base("sync")
        {
            Profile = new ProfileResponseModel(user, organizationUserDetails, userTwoFactorEnabled);
            Folders = folders.Select(f => new FolderResponseModel(f));
            Ciphers = ciphers.Select(c => new CipherDetailsResponseModel(c, globalSettings, collectionCiphersDict));
            Collections = collections?.Select(
                c => new CollectionDetailsResponseModel(c)) ?? new List<CollectionDetailsResponseModel>();
            Domains = excludeDomains ? null : new DomainsResponseModel(user, false);
            Policies = policies?.Select(p => new PolicyResponseModel(p)) ?? new List<PolicyResponseModel>();
            Sends = sends.Select(s => new SendResponseModel(s, globalSettings));
        }

        public ProfileResponseModel Profile { get; set; }
        public IEnumerable<FolderResponseModel> Folders { get; set; }
        public IEnumerable<CollectionDetailsResponseModel> Collections { get; set; }
        public IEnumerable<CipherDetailsResponseModel> Ciphers { get; set; }
        public DomainsResponseModel Domains { get; set; }
        public IEnumerable<PolicyResponseModel> Policies { get; set; }
        public IEnumerable<SendResponseModel> Sends { get; set; }
    }
}
