using Bit.Core.Auth.Models.Data;

namespace Bit.Api.Auth.Models.Response;

public class PendingAuthRequestResponseModel : AuthRequestResponseModel
{
    public PendingAuthRequestResponseModel(PendingAuthRequestDetails authRequest, string vaultUri, string obj = "auth-request")
        : base(authRequest, vaultUri, obj)
    {
        ArgumentNullException.ThrowIfNull(authRequest);
        RequestDeviceId = authRequest.RequestDeviceId;
    }

    public Guid? RequestDeviceId { get; set; }
}
