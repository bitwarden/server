using System;
using Bit.Core.Models.Table;

namespace Bit.Core.Models.Api
{
    public class SsoConfigResponseModel : ResponseModel
    {
        public SsoConfigResponseModel(SsoConfig ssoConfig, string obj = "ssoconfig")
            : base(obj)
        {
            if (ssoConfig == null)
            {
                throw new ArgumentNullException(nameof(ssoConfig));
            }

            Id = ssoConfig.Id;
            Enabled = ssoConfig.Enabled;
            OrganizationId = ssoConfig.OrganizationId;
            Data = ssoConfig.Data;
        }

        public long? Id { get; set; }
        public bool Enabled { get; set; }
        public Guid OrganizationId { get; set; }
        public string Data { get; set; }
    }
}
