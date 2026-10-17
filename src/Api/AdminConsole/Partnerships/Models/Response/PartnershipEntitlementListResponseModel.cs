using System.Text.Json.Serialization;
using Bit.Core.AdminConsole.OrganizationFeatures.Partnerships;
using Bit.HttpExtensions;

namespace Bit.Api.AdminConsole.Partnerships.Models.Response;

/// <summary>
/// One page of the partnership's entitlements, ordered by when they were last updated.
/// </summary>
public class PartnershipEntitlementListResponseModel(PartnershipEntitlementPage page) : ResponseModel("list")
{
    /// <summary>
    /// The entitlements on this page.
    /// </summary>
    public IEnumerable<PartnershipEntitlementResponseModel> Data { get; } =
        page.Entitlements.Select(e => new PartnershipEntitlementResponseModel(e)).ToList();

    /// <summary>
    /// Pass back with the same filters to read the next page. Absent on the last page.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ContinuationToken { get; } = page.ContinuationToken;
}
