using Bit.Core.Entities.Provider;
using Bit.Core.Models.Api;
using Bit.Core.Models.Data;

namespace Bit.Api.Models.Response.Providers;

public class ProviderOrganizationResponseModel : ResponseModel
{
    public ProviderOrganizationResponseModel(ProviderOrganization providerOrganization,
        string obj = "providerOrganization") : base(obj)
    {
        if (providerOrganization == null)
        {
            throw new ArgumentNullException(nameof(providerOrganization));
        }

        Id = providerOrganization.Id;
        ProviderId = providerOrganization.ProviderId;
        OrganizationId = providerOrganization.OrganizationId;
        Key = providerOrganization.Key;
        Settings = providerOrganization.Settings;
        CreationDate = providerOrganization.CreationDate;
        RevisionDate = providerOrganization.RevisionDate;
    }

    public ProviderOrganizationResponseModel(ProviderOrganizationOrganizationDetails providerOrganization,
        string obj = "providerOrganization") : base(obj)
    {
        if (providerOrganization == null)
        {
            throw new ArgumentNullException(nameof(providerOrganization));
        }

        Id = providerOrganization.Id;
        ProviderId = providerOrganization.ProviderId;
        OrganizationId = providerOrganization.OrganizationId;
        Key = providerOrganization.Key;
        Settings = providerOrganization.Settings;
        CreationDate = providerOrganization.CreationDate;
        RevisionDate = providerOrganization.RevisionDate;
        UserCount = providerOrganization.UserCount;
        Seats = providerOrganization.Seats;
        Plan = providerOrganization.Plan;
    }

    public Guid Id { get; set; }
    public Guid ProviderId { get; set; }
    public Guid OrganizationId { get; set; }
    public string Key { get; set; }
    public string Settings { get; set; }
    public DateTime CreationDate { get; set; }
    public DateTime RevisionDate { get; set; }
    public int UserCount { get; set; }
    public int? Seats { get; set; }
    public string Plan { get; set; }
}

public class ProviderOrganizationOrganizationDetailsResponseModel : ProviderOrganizationResponseModel
{
    public ProviderOrganizationOrganizationDetailsResponseModel(ProviderOrganizationOrganizationDetails providerOrganization,
        string obj = "providerOrganizationOrganizationDetail") : base(providerOrganization, obj)
    {
        if (providerOrganization == null)
        {
            throw new ArgumentNullException(nameof(providerOrganization));
        }

        OrganizationName = providerOrganization.OrganizationName;
    }

    public string OrganizationName { get; set; }
}
