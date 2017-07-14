using System;
using Bit.Core.Utilities;

namespace Bit.Core.Models.Table
{
    public class Device : ITableObject<Guid>
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string Name { get; set; }
        public Enums.DeviceType Type { get; set; }
        public string Identifier { get; set; }
        public string PushToken { get; set; }
        public DateTime CreationDate { get; internal set; } = DateTime.UtcNow;
        public DateTime RevisionDate { get; internal set; } = DateTime.UtcNow;

        public void SetNewId()
        {
            Id = CoreHelpers.GenerateComb();
        }
    }
}
