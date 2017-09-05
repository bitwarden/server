using System.Collections.Generic;

namespace Bit.Core.Models.Api
{
    public class ImportCiphersRequestModel
    {
        public FolderRequestModel[] Folders { get; set; }
        public LoginRequestModel[] Logins { get; set; }
        public KeyValuePair<int, int>[] FolderRelationships { get; set; }
    }
}
