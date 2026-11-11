namespace Bit.Pam.Models;

/// <summary>One page of the approver history, newest end first, with what the filters can offer.</summary>
public class AccessRequestHistoryPage
{
    public List<AccessRequestDetails> Requests { get; init; } = [];

    /// <summary>How many rows match the filters, across all pages.</summary>
    public int Total { get; init; }

    /// <summary>Everyone with a row in the history, whatever the filters.</summary>
    public List<AccessRequestHistoryRequester> Requesters { get; init; } = [];

    /// <summary>Every collection with a row in the history, whatever the filters.</summary>
    public List<Guid> CollectionIds { get; init; } = [];
}
