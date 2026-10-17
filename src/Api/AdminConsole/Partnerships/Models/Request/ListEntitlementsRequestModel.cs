using System.Globalization;
using System.Text.Json;
using Bit.Core.AdminConsole.Enums.Partnerships;
using Bit.Core.AdminConsole.OrganizationFeatures.Partnerships;
using Bit.Core.AdminConsole.Utilities.v2.Results;

namespace Bit.Api.AdminConsole.Partnerships.Models.Request;

/// <summary>
/// Query parameters for listing the partnership's entitlements.
/// </summary>
public class ListEntitlementsRequestModel
{
    /// <summary>
    /// Only entitlements in this state: <c>provisioned</c>, <c>active</c>, <c>suspended</c>, or <c>canceled</c>.
    /// </summary>
    public string? State { get; set; }

    /// <summary>
    /// Only entitlements updated strictly after this ISO 8601 timestamp. A timestamp without an offset is UTC.
    /// </summary>
    public string? RevisedAfter { get; set; }

    /// <summary>
    /// The <c>continuationToken</c> from the previous page. Send the same filters with it.
    /// </summary>
    public string? ContinuationToken { get; set; }

    public CommandResult<ListPartnershipEntitlementsRequest> ToRequest(Guid organizationPartnershipId)
    {
        PartnershipEntitlementState? state = null;
        if (State is not null)
        {
            state = Enum.GetValues<PartnershipEntitlementState>()
                .Cast<PartnershipEntitlementState?>()
                .FirstOrDefault(s => JsonNamingPolicy.SnakeCaseLower.ConvertName(s!.Value.ToString()) == State);
            if (state is null)
            {
                return new InvalidEntitlementStateFilter();
            }
        }

        DateTime? revisedAfter = null;
        if (RevisedAfter is not null)
        {
            if (!DateTimeOffset.TryParse(RevisedAfter, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal,
                    out var parsed))
            {
                return new InvalidRevisedAfter();
            }
            revisedAfter = parsed.UtcDateTime;
        }

        return new ListPartnershipEntitlementsRequest
        {
            OrganizationPartnershipId = organizationPartnershipId,
            State = state,
            RevisedAfter = revisedAfter,
            ContinuationToken = ContinuationToken,
        };
    }
}
