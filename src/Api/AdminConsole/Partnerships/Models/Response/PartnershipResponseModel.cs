using System.Text.Json;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Settings;
using Bit.HttpExtensions;

namespace Bit.Api.AdminConsole.Partnerships.Models.Response;

/// <summary>
/// The authenticated partner's partnership configuration.
/// </summary>
public class PartnershipResponseModel(
    OrganizationPartnership partnership,
    GlobalSettings.PartnershipSettings partnershipSettings) : ResponseModel("partnership")
{
    /// <summary>
    /// The partnership's display name.
    /// </summary>
    public string Name { get; } = partnership.Name;
    /// <summary>
    /// One of <c>active</c> or <c>inactive</c>.
    /// </summary>
    public string Status { get; } = SnakeCase(partnership.Status.ToString());
    /// <summary>
    /// The plan types the partnership may provision, e.g. <c>premium</c>.
    /// </summary>
    public IEnumerable<string> AllowedPlanTypes { get; } =
        partnership.GetAllowedSponsoredPlanTypes().Select(p => SnakeCase(p.ToString())).ToList();
    /// <summary>
    /// How customers prove they hold an entitlement when activating: <c>token</c> or <c>oidc</c>.
    /// </summary>
    public string BindingMode { get; } = SnakeCase(partnership.BindingMode.ToString());
    /// <summary>
    /// The origins an activation link's return URL may use.
    /// </summary>
    public IEnumerable<string> RegisteredReturnOrigins { get; } = partnership.GetRegisteredReturnOrigins().ToList();
    /// <summary>
    /// The request limit applied to the partnership.
    /// </summary>
    public PartnershipRateLimitResponseModel RateLimit { get; } = new(partnershipSettings);

    private static string SnakeCase(string name) => JsonNamingPolicy.SnakeCaseLower.ConvertName(name);
}
