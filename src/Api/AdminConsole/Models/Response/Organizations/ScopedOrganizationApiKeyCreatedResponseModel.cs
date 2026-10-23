using Bit.Core.AdminConsole.OrganizationFeatures.ScopedOrganizationApiKeys;

namespace Bit.Api.AdminConsole.Models.Response.Organizations;

public class ScopedOrganizationApiKeyCreatedResponseModel : ScopedOrganizationApiKeyResponseModel
{
    public ScopedOrganizationApiKeyCreatedResponseModel(CreatedScopedOrganizationApiKey created)
        : base(created.ApiKey, "scopedApiKeyCreated")
    {
        ClientSecret = created.ClientSecret;
    }

    public string ClientSecret { get; set; }
}
