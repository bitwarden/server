using System.Collections.Generic;

namespace Bit.Core.Models.Api
{
    public class ImportCiphersRequestModel
    {
        public FolderRequestModel[] Folders { get; set; }
        public CipherRequestModel[] Ciphers { get; set; }
        public KeyValuePair<int, int>[] FolderRelationships { get; set; }
    }
}
