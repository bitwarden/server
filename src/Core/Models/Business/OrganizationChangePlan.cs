using Bit.Core.Enums;
using System;

namespace Bit.Core.Models.Business
{
    public class OrganizationChangePlan
    {
        public Guid OrganizationId { get; set; }
        public PlanType PlanType { get; set; }
        public short AdditionalUsers { get; set; }
        public bool Monthly { get; set; }
    }
}
