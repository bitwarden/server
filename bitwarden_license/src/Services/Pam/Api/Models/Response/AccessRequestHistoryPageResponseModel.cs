using Bit.HttpExtensions;
using Bit.Pam.Models;

namespace Bit.Services.Pam.Api.Models.Response;

/// <summary>One page of the approver history, newest request first, with what the filters can offer.</summary>
public class AccessRequestHistoryPageResponseModel : ResponseModel
{
    public AccessRequestHistoryPageResponseModel(AccessRequestHistoryPage page)
        : base("accessRequestHistoryPage")
    {
        ArgumentNullException.ThrowIfNull(page);

        Data = page.Requests.Select(request => new AccessRequestDetailsResponseModel(request));
        Total = page.Total;
        Requesters = page.Requesters.Select(requester => new AccessRequestHistoryRequesterResponseModel(requester));
        CollectionIds = page.CollectionIds;
    }

    public IEnumerable<AccessRequestDetailsResponseModel> Data { get; }

    /// <summary>How many rows match the filters, across all pages.</summary>
    public int Total { get; }

    /// <summary>Everyone with a row in the history, whatever the filters, for the Requester filter.</summary>
    public IEnumerable<AccessRequestHistoryRequesterResponseModel> Requesters { get; }

    /// <summary>
    /// Every collection with a row in the history, whatever the filters. Collection names are vault data, so the
    /// client names them itself.
    /// </summary>
    public IEnumerable<Guid> CollectionIds { get; }
}
