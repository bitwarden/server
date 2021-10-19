using System;
using System.ComponentModel.DataAnnotations;

namespace Bit.Core.Models.Table
{
    public class U2f : ITableObject<int>
    {
        public int Id { get; set; }
        public Guid UserId { get; set; }
        [MaxLength(200)]
        public string KeyHandle { get; set; }
        [MaxLength(200)]
        public string Challenge { get; set; }
        [MaxLength(50)]
        public string AppId { get; set; }
        [MaxLength(20)]
        public string Version { get; set; }
        public DateTime CreationDate { get; internal set; } = DateTime.UtcNow;

        public void SetNewId()
        {
            // int will be auto-populated
            Id = 0;
        }
    }
}
