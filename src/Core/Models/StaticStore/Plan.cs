using Bit.Core.Enums;
using System;

namespace Bit.Core.Models.StaticStore
{
    public class Plan
    {
        public string Name { get; set; }
        public string StripeId { get; set; }
        public PlanType Type { get; set; }
        public short MaxUsers { get; set; }
        public decimal Price { get; set; }
        public TimeSpan? Trial { get; set; }
        public Func<DateTime, TimeSpan> Cycle { get; set; }
        public bool Disabled { get; set; }
    }
}
