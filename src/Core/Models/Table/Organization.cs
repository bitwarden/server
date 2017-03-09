using System;
using Bit.Core.Utilities;
using Bit.Core.Enums;

namespace Bit.Core.Models.Table
{
    public class Organization : IDataObject<Guid>
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string Name { get; set; }
        public string Plan { get; set; }
        public PlanType PlanType { get; set; }
        public decimal PlanPrice { get; set; }
        public decimal PlanRenewalPrice { get; set; }
        public DateTime? PlanRenewalDate { get; set; }
        public bool PlanTrial { get; set; }
        public short MaxUsers { get; set; }
        public DateTime CreationDate { get; internal set; } = DateTime.UtcNow;
        public DateTime RevisionDate { get; internal set; } = DateTime.UtcNow;

        public void SetNewId()
        {
            Id = CoreHelpers.GenerateComb();
        }
    }
}
