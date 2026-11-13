namespace Bit.Pam.Models;

/// <summary>A requester with a row in the approver history, for the Requester filter.</summary>
public class AccessRequestHistoryRequester
{
    public Guid Id { get; init; }
    public string? Name { get; init; }
    public string? Email { get; init; }
}
