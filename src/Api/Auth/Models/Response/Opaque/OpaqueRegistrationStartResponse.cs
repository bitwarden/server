using Bit.Core.Models.Api;

namespace Bit.Api.Auth.Models.Response.Opaque;

public class OpaqueRegistrationStartResponse : ResponseModel
{
    public OpaqueRegistrationStartResponse(Guid sessionId, string registrationResponse, string obj = "register-start-response")
        : base(obj)
    {
        RegistrationResponse = registrationResponse;
        SessionId = sessionId;
    }

    public String RegistrationResponse { get; set; }
    public Guid SessionId { get; set; }
}

