using Bit.HttpExtensions;

namespace Bit.Api.AdminConsole.Partnerships.Models.Response;

/// <summary>
/// The single error body returned by partnership endpoints.
/// </summary>
public class PartnershipErrorResponseModel(string code, string message, string requestId) : ResponseModel("error")
{
    /// <summary>
    /// A stable, machine-readable error code, e.g. <c>invalid_request</c>, <c>not_found</c>, <c>conflict</c>, or
    /// <c>rate_limited</c>.
    /// </summary>
    public string Code { get; } = code;
    /// <summary>
    /// A human-readable description of the error. Do not parse it; use <c>code</c>.
    /// </summary>
    public string Message { get; } = message;
    /// <summary>
    /// Identifies the request. Quote it when contacting support.
    /// </summary>
    public string RequestId { get; } = requestId;
}
