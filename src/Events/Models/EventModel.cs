using System;
using Bit.Core.Enums;

namespace Bit.Events.Models
{
    public class EventModel
    {
        public EventType Type { get; set; }
        public Guid? CipherId { get; set; }
    }
}
