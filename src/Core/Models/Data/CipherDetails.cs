using Bit.Core.Models.Table;
using System;

namespace Core.Models.Data
{
    public class CipherDetails : Cipher
    {
        public Guid? FolderId { get; set; }
        public bool Favorite { get; set; }
    }
}
