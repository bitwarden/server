using Duende.IdentityServer.Models;

namespace Bit.Sso.Models;

public class ErrorViewModel
{
    private string _requestId;

    public ErrorMessage Error { get; set; }
    public Exception Exception { get; set; }

    public string Message => Error?.Error;
    public string Description => Error?.ErrorDescription ?? Exception?.Message;
    public string RedirectUri => Error?.RedirectUri;
    public string RequestId
    {
        get { return Error?.RequestId ?? _requestId; }
        set { _requestId = value; }
    }
}
