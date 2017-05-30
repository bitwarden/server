using Bit.Core.Enums;
using Newtonsoft.Json;
using System;

namespace Bit.Core.Models
{
    public class PayloadPushNotification
    {
        [JsonProperty(PropertyName = "data")]
        public DataObj Data { get; set; }

        public class DataObj
        {
            public DataObj(PushType type, string payload)
            {
                Type = type;
                Payload = payload;
            }

            [JsonProperty(PropertyName = "type")]
            public PushType Type { get; set; }
            [JsonProperty(PropertyName = "payload")]
            public string Payload { get; set; }
        }
    }

    public class ApplePayloadPushNotification : PayloadPushNotification
    {
        [JsonProperty(PropertyName = "aps")]
        public AppleData Aps { get; set; } = new AppleData { ContentAvailable = 1 };

        public class AppleData
        {
            [JsonProperty(PropertyName = "badge")]
            public dynamic Badge { get; set; } = null;
            [JsonProperty(PropertyName = "alert")]
            public string Alert { get; set; }
            [JsonProperty(PropertyName = "content-available")]
            public int ContentAvailable { get; set; }
        }
    }

    public class SyncCipherPushNotification
    {
        public Guid Id { get; set; }
        public Guid? UserId { get; set; }
        public Guid? OrganizationId { get; set; }
        public DateTime RevisionDate { get; set; }
    }

    public class SyncFolderPushNotification
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public DateTime RevisionDate { get; set; }
    }

    public class SyncUserPushNotification
    {
        public Guid UserId { get; set; }
        public DateTime Date { get; set; }
    }
}
