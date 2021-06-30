using System;
using Bit.Core.Models.Data;

namespace Bit.Core.Models.Api
{
    public class ProviderOrganizationOrganizationDetailsResponseModel : ResponseModel
    {
        public ProviderOrganizationOrganizationDetailsResponseModel(ProviderOrganizationOrganizationDetails providerOrganization,
            string obj = "providerOrganization") : base(obj)
        {
            if (providerOrganization == null)
            {
                throw new ArgumentNullException(nameof(providerOrganization));
            }

            Id = providerOrganization.Id;
            ProviderId = providerOrganization.ProviderId;
            OrganizationId = providerOrganization.OrganizationId;
            OrganizationName = providerOrganization.OrganizationName;
            Key = providerOrganization.Key;
            Settings = providerOrganization.Settings;
            CreationDate = providerOrganization.CreationDate;
            RevisionDate = providerOrganization.RevisionDate;
        }
        
        public Guid Id { get; set; }
        public Guid ProviderId { get; set; }
        public Guid OrganizationId { get; set; }
        public string OrganizationName { get; set; }
        public string Key { get; set; }
        public string Settings { get; set; }
        public DateTime CreationDate { get; set; }
        public DateTime RevisionDate { get; set; }
    }
}
