using System.Text.Json.Serialization;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Tools.Entities;
using Bit.Core.Tools.Enums;

namespace Bit.Core.Tools.Models.Business;

public class ReferenceEvent
{
    public ReferenceEvent() { }

    public ReferenceEvent(ReferenceEventType type, IReferenceable source, ICurrentContext currentContext)
    {
        Type = type;
        if (source != null)
        {
            Source = source.IsUser() ? ReferenceEventSource.User : ReferenceEventSource.Organization;
            Id = source.Id;
            ReferenceData = source.ReferenceData;
        }
        if (currentContext != null)
        {
            ClientId = currentContext.ClientId;
            ClientVersion = currentContext.ClientVersion;
        }
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ReferenceEventType Type { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
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

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public SendType? SendType { get; set; }

    public bool? SendHasNotes { get; set; }

    public int? MaxAccessCount { get; set; }

    public bool? HasPassword { get; set; }

    public string EventRaisedByUser { get; set; }

    public bool? SalesAssistedTrialStarted { get; set; }

    public string ClientId { get; set; }
    public Version? ClientVersion { get; set; }
    public int? SmSeats { get; set; }
    public int? ServiceAccounts { get; set; }
}
