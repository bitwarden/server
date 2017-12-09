using System;
using Bit.Core.Enums;

namespace Bit.Core.Models.Data
{
    public interface IEvent
    {
        Guid? ActingUserId { get; set; }
        Guid? CipherId { get; set; }
        Guid? CollectionId { get; set; }
        DateTime Date { get; set; }
        Guid? GroupId { get; set; }
        Guid? OrganizationId { get; set; }
        Guid? OrganizationUserId { get; set; }
        EventType Type { get; set; }
        Guid? UserId { get; set; }
    }
}
