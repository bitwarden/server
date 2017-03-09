using System;
using Bit.Core.Utilities;

namespace Bit.Core.Models.Table
{
    public class Cipher : IDataObject<Guid>
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public Guid? FolderId { get; set; }
        public Enums.CipherType Type { get; set; }
        public bool Favorite { get; set; }
        public string Data { get; set; }
        public DateTime CreationDate { get; internal set; } = DateTime.UtcNow;
        public DateTime RevisionDate { get; internal set; } = DateTime.UtcNow;

        public void SetNewId()
        {
            Id = CoreHelpers.GenerateComb();
        }
    }
}
