using Bit.Core.Enums;
using System;

namespace Bit.Core.Models.StaticStore
{
    public class Plan
    {
        public PlanType Type { get; set; }
        public short MaxUsers { get; set; }
        public decimal Price { get; set; }
        public TimeSpan? Trial { get; set; }
        public Func<TimeSpan> Cycle { get; set; }
        public bool Disabled { get; set; }
    }
}
