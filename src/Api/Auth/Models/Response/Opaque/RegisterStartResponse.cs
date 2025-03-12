using Bit.Core.Models.Api;

namespace Bit.Api.Auth.Models.Response.Opaque;

public class RegisterStartResponse : ResponseModel
{
    public RegisterStartResponse(Guid sessionId, string serverRegistrationStartResult, string obj = "register-start-response")
        : base(obj)
    {
        ServerRegistrationStartResult = serverRegistrationStartResult;
        SessionId = sessionId;
    }

    public String ServerRegistrationStartResult { get; set; }
    public Guid SessionId { get; set; }
}

