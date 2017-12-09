using System;
using Bit.Core.Enums;

namespace Bit.Core.Models.Data
{
    public interface IEvent
    {
        DateTime Date { get; set; }
        EventType Type { get; set; }
        Guid? UserId { get; set; }
        Guid? OrganizationId { get; set; }
        Guid? CipherId { get; set; }
        Guid? CollectionId { get; set; }
        Guid? GroupId { get; set; }
        Guid? OrganizationUserId { get; set; }
        Guid? ActingUserId { get; set; }
    }
}
