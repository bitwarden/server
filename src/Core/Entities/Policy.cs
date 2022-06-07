using System;
using System.Text.Json;
using Bit.Core.Enums;
using Bit.Core.Models.Data.Organizations.Policies;
using Bit.Core.Utilities;

namespace Bit.Core.Entities
{
    public class Policy : ITableObject<Guid>
    {
        public Guid Id { get; set; }
        public Guid OrganizationId { get; set; }
        public PolicyType Type { get; set; }
        public string Data { get; set; }
        public bool Enabled { get; set; }
        public DateTime CreationDate { get; internal set; } = DateTime.UtcNow;
        public DateTime RevisionDate { get; internal set; } = DateTime.UtcNow;

        public void SetNewId()
        {
            Id = CoreHelpers.GenerateComb();
        }
    }

    public class Policy<T> : Policy where T : IPolicyDataModel
    {
        public T DataModel
        {
            get => JsonSerializer.Deserialize<T>(Data);
            set => Data = JsonSerializer.Serialize(value);
        }
    }
}
