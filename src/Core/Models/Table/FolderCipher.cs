using System;

namespace Bit.Core.Models.Table
{
    public class FolderCipher
    {
        public Guid CipherId { get; set; }
        public Guid FolderId { get; set; }
        public Guid UserId { get; set; }
    }
}
