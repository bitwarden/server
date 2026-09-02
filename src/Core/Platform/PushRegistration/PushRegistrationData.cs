namespace Bit.Core.Platform.PushRegistration;

public record struct WebPushRegistrationData
{
    public string Endpoint { get; init; }
    public string P256dh { get; init; }
    public string Auth { get; init; }
}

public record class PushRegistrationData
{
    public string? Token { get; set; }
    public WebPushRegistrationData? WebPush { get; set; }
    public PushRegistrationData(string? token)
    {
        Token = token;
    }

    public PushRegistrationData(string Endpoint, string P256dh, string Auth) : this(new WebPushRegistrationData
    {
        Endpoint = Endpoint,
        P256dh = P256dh,
        Auth = Auth
    })
    { }

    public PushRegistrationData(WebPushRegistrationData webPush)
    {
        WebPush = webPush;
    }
}
