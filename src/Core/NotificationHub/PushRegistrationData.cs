namespace Bit.Core.NotificationHub;

public struct WebPushRegistrationData : IEquatable<WebPushRegistrationData>
{
    public string Endpoint { get; init; }
    public string P256dh { get; init; }
    public string Auth { get; init; }

    public bool Equals(WebPushRegistrationData other)
    {
        return Endpoint == other.Endpoint && P256dh == other.P256dh && Auth == other.Auth;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Endpoint, P256dh, Auth);
    }
}

public class PushRegistrationData : IEquatable<PushRegistrationData>
{
    public string Token { get; set; }
    public WebPushRegistrationData? WebPush { get; set; }
    public PushRegistrationData(string token)
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
    public bool Equals(PushRegistrationData other)
    {
        return Token == other.Token && WebPush.Equals(other.WebPush);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Token, WebPush.GetHashCode());
    }
}
