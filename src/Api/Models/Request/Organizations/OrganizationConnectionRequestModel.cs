using System;
using Bit.Core.Enums;
using Bit.Core.Models.Data.Organizations.OrganizationConnections;

namespace Bit.Api.Models.Request.Organizations
{
    public class OrganizationConnectionRequestModel
    {
        public OrganizationConnectionType Type { get; set; }
        public Guid OrganizationId { get; set; }
        public bool Enabled { get; set; }
        public string Config { get; set; }

        public OrganizationConnectionRequestModel() { }

        public OrganizationConnectionData ToData(Guid? id = null) =>
            new()
            {
                Id = id,
                Type = Type,
                OrganizationId = OrganizationId,
                Enabled = Enabled,
                Config = Config,
            };
    }
}
