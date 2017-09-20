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
            IEnumerable<CipherDetails> ciphers)
            : base("sync")
        {
            Profile = new ProfileResponseModel(user, organizationUserDetails);
            Folders = folders.Select(f => new FolderResponseModel(f));
            Ciphers = ciphers.Select(c => new CipherResponseModel(c, globalSettings));
            Domains = new DomainsResponseModel(user, false);
        }

        public ProfileResponseModel Profile { get; set; }
        public IEnumerable<FolderResponseModel> Folders { get; set; }
        public IEnumerable<CipherResponseModel> Ciphers { get; set; }
        public DomainsResponseModel Domains { get; set; }
    }
}
