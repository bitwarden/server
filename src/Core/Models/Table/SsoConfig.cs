using System;
using System.Text.Json;
using Bit.Core.Models.Data;

namespace Bit.Core.Models.Table
{
    public class SsoConfig : ITableObject<long>
    {
        public long Id { get; set; }
        public bool Enabled { get; set; } = true;
        public Guid OrganizationId { get; set; }
        public string Data { get; set; }
        public DateTime CreationDate { get; internal set; } = DateTime.UtcNow;
        public DateTime RevisionDate { get; internal set; } = DateTime.UtcNow;

        private JsonSerializerOptions _jsonSerializerOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        public void SetNewId()
        {
            // int will be auto-populated
            Id = 0;
        }

        public SsoConfigurationData GetData()
        {
            return JsonSerializer.Deserialize<SsoConfigurationData>(Data, _jsonSerializerOptions);
        }

        public void SetData(SsoConfigurationData data)
        {
            Data = JsonSerializer.Serialize(data, _jsonSerializerOptions);
        }
    }
}
