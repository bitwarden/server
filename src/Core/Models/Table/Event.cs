using System;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Utilities;

namespace Bit.Core.Models.Table
{
    public class Event : ITableObject<Guid>, IEvent
    {
        public Event() { }

        public Event(IEvent e)
        {
            Date = e.Date;
            Type = e.Type;
            UserId = e.UserId;
            OrganizationId = e.OrganizationId;
            CipherId = e.CipherId;
            CollectionId = e.CollectionId;
            PolicyId = e.PolicyId;
            GroupId = e.GroupId;
            OrganizationUserId = e.OrganizationUserId;
            DeviceType = e.DeviceType;
            IpAddress = e.IpAddress;
            ActingUserId = e.ActingUserId;
        }

        public Guid Id { get; set; }
        public DateTime Date { get; set; }
        public EventType Type { get; set; }
        public Guid? UserId { get; set; }
        public Guid? OrganizationId { get; set; }
        public Guid? CipherId { get; set; }
        public Guid? CollectionId { get; set; }
        public Guid? PolicyId { get; set; }
        public Guid? GroupId { get; set; }
        public Guid? OrganizationUserId { get; set; }
        public DeviceType? DeviceType { get; set; }
        public string IpAddress { get; set; }
        public Guid? ActingUserId { get; set; }

        public void SetNewId()
        {
            Id = CoreHelpers.GenerateComb();
        }
    }
}
