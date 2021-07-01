using System;
using Bit.Core.Models.Table.Provider;

namespace Bit.Core.Context
{
    public class CurrentContentProviderOrganization
    {
        public CurrentContentProviderOrganization()
        {
        }

        public CurrentContentProviderOrganization(ProviderOrganization providerOrganization)
        {
            OrganizationId = providerOrganization.OrganizationId;
        }

        public Guid OrganizationId { get; set; }
    }
}
