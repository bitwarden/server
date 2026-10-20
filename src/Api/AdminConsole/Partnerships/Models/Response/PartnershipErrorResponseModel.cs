using Bit.HttpExtensions;

namespace Bit.Api.AdminConsole.Partnerships.Models.Response;

/// <summary>
/// The single error body returned by partnership endpoints.
/// </summary>
public class PartnershipErrorResponseModel(string code, string message, string requestId) : ResponseModel("error")
{
    public string Code { get; } = code;
    public string Message { get; } = message;
    public string RequestId { get; } = requestId;
}
