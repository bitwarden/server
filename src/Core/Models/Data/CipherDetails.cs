using Bit.Core.Enums;
using System;

namespace Core.Models.Data
{
    class CipherDetails
    {
        public Guid Id { get; set; }
        public Guid? UserId { get; set; }
        public Guid? OrganizationId { get; set; }
        public Guid? FolderId { get; set; }
        public CipherType Type { get; set; }
        public bool Favorite { get; set; }
        public string Data { get; set; }
        public DateTime CreationDate { get; set; }
        public DateTime RevisionDate { get; set; }
    }
}
