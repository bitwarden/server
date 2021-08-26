using System;

namespace Bit.Core.Models.Mail
{
    public class OrganizationSeatsAutoscaledViewModel : BaseMailModel
    {
        public Guid OrganizationId { get; set; }
        public string OrganizationName { get; set; }
        public int CurrentSeatCount { get; set; }
        public int? MaxAutoscaleSeatCount { get; set; }
        public bool LimitedAutoscaling => MaxAutoscaleSeatCount.HasValue;
        public int MaxSeatCount => MaxAutoscaleSeatCount.Value;
    }
}
