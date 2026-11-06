using Bit.Api.Dirt.Utilities;
using Bit.HttpExtensions;

namespace Bit.Api.Dirt.Models.Response;

/// <summary>
/// The outcome of finishing an OAuth-based integration setup, using the <see cref="IntegrationOAuthCallbackRedirect"/>
/// result values the web vault already displays.
/// </summary>
public class OrganizationIntegrationOAuthCompleteResponseModel(string result)
    : ResponseModel("organizationIntegrationOAuthComplete")
{
    public string Result { get; set; } = result;
}
