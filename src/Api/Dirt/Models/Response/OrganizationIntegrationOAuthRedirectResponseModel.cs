using Bit.HttpExtensions;

namespace Bit.Api.Dirt.Models.Response;

/// <summary>
/// The provider's authorize URL for an OAuth-based integration. The web vault navigates the browser to it,
/// since a <c>fetch</c> can't follow a cross-origin redirect as a page navigation.
/// </summary>
public class OrganizationIntegrationOAuthRedirectResponseModel(string url)
    : ResponseModel("organizationIntegrationOAuthRedirect")
{
    public string Url { get; set; } = url;
}
