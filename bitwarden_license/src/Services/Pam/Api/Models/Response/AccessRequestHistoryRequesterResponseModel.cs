using Bit.HttpExtensions;
using Bit.Pam.Models;

namespace Bit.Services.Pam.Api.Models.Response;

/// <summary>A requester with a row in the approver history.</summary>
public class AccessRequestHistoryRequesterResponseModel : ResponseModel
{
    public AccessRequestHistoryRequesterResponseModel(AccessRequestHistoryRequester requester)
        : base("accessRequestHistoryRequester")
    {
        ArgumentNullException.ThrowIfNull(requester);

        Id = requester.Id;
        Name = requester.Name;
        Email = requester.Email;
    }

    public Guid Id { get; }
    public string? Name { get; }
    public string? Email { get; }
}
