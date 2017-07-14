using System;

namespace Bit.Core.Models.Table
{
    public class U2f : ITableObject<int>
    {
        public int Id { get; set; }
        public Guid UserId { get; set; }
        public string KeyHandle { get; set; }
        public string Challenge { get; set; }
        public string AppId { get; set; }
        public string Version { get; set; }
        public DateTime CreationDate { get; internal set; } = DateTime.UtcNow;

        public void SetNewId()
        {
            // do nothing since it is an identity
        }
    }
}
