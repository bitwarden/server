using System;
using System.Text.Json;
using Bit.Core.Enums;
using Bit.Core.Utilities;

namespace Bit.Core.Entities
{
    public class OrganizationConnection : ITableObject<Guid>
    {
        public Guid Id { get; set; }
        public OrganizationConnectionType Type { get; set; }
        public Guid OrganizationId { get; set; }
        public bool Enabled { get; set; }
        public string Config { get; set; }

        public void SetNewId()
        {
            Id = CoreHelpers.GenerateComb();
        }
    }
}
