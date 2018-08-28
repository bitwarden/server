using Bit.Core.Enums;
using System;
using System.Collections.Generic;

namespace Bit.Core.Models
{
    public class PushNotificationData<T>
    {
        public PushNotificationData(PushType type, T payload, string contextId)
        {
            Type = type;
            Payload = payload;
            ContextId = contextId;
        }
        
        public PushType Type { get; set; }
        public T Payload { get; set; }
        public string ContextId { get; set; }
    }

    public class SyncCipherPushNotification
    {
        public Guid Id { get; set; }
        public Guid? UserId { get; set; }
        public Guid? OrganizationId { get; set; }
        public IEnumerable<Guid> CollectionIds { get; set; }
        public DateTime RevisionDate { get; set; }
    }

    public class SyncFolderPushNotification
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public DateTime RevisionDate { get; set; }
    }

    public class UserPushNotification
    {
        public Guid UserId { get; set; }
        public DateTime Date { get; set; }
    }
}
