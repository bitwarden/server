using Bit.Core.Models.Api;

namespace Bit.Api.Auth.Models.Response.Opaque;

public class OpaqueLoginStartResponse : ResponseModel
{
    public OpaqueLoginStartResponse(Guid sessionId, string credentialResponse, string obj = "login-start-response")
        : base(obj)
    {
        CredentialResponse = credentialResponse;
        SessionId = sessionId;
    }

    public string CredentialResponse { get; set; }
    public Guid SessionId { get; set; }
}

