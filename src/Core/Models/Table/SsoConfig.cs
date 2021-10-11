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
        
        public void SetNewId()
        {
            // int will be auto-populated
            Id = 0;
        }

        public SsoConfigurationData GetData()
        {
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            };
            return JsonSerializer.Deserialize<SsoConfigurationData>(Data, options);
        }
    }
}
