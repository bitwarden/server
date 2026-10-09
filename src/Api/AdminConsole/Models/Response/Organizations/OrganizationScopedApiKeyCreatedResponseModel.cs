using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationScopedApiKeys;

namespace Bit.Api.AdminConsole.Models.Response.Organizations;

public class OrganizationScopedApiKeyCreatedResponseModel : OrganizationScopedApiKeyResponseModel
{
    public OrganizationScopedApiKeyCreatedResponseModel(CreatedOrganizationScopedApiKey created)
        : base(created.ApiKey, "scopedApiKeyCreated")
    {
        ClientSecret = created.ClientSecret;
    }

    public string ClientSecret { get; set; }
}
