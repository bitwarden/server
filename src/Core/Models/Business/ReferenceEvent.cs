using System;
using Bit.Core.Enums;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

namespace Bit.Core.Models.Business
{
    [JsonObject(NamingStrategyType = typeof(CamelCaseNamingStrategy))]
    public class ReferenceEvent
    {
        public ReferenceEvent() { }

        public ReferenceEvent(ReferenceEventType type, IReferenceable source)
        {
            Type = type;
            if (source != null)
            {
                Source = source.IsUser() ? ReferenceEventSource.User : ReferenceEventSource.Organization;
                Id = source.Id;
                ReferenceData = source.ReferenceData;
            }
        }

        [JsonConverter(typeof(StringEnumConverter))]
        public ReferenceEventType Type { get; set; }
        
        [JsonConverter(typeof(StringEnumConverter))]
        public ReferenceEventSource Source { get; set; }

        public Guid Id { get; set; }

        public string ReferenceData { get; set; }

        public DateTime EventDate { get; set; } = DateTime.UtcNow;

        public int? Users { get; set; }

        public bool? EndOfPeriod { get; set; }

        public string PlanName { get; set; }

        public PlanType? PlanType { get; set; }

        public string OldPlanName { get; set; }

        public PlanType? OldPlanType { get; set; }

        public int? Seats { get; set; }
        public int? PreviousSeats { get; set; }

        public short? Storage { get; set; }

        [JsonConverter(typeof(StringEnumConverter))]
        public SendType? SendType { get; set; }

        public int? MaxAccessCount { get; set; }

        public bool? HasPassword { get; set; }

        public string EventRaisedByUser { get; set; }

        public bool? SalesAssistedTrialStarted { get; set; }
    }
}
