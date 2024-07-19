namespace Bit.Core.NotificationHub;

public class PushRegistrationData
{
    public string Token { get; set; }
    public (string Endpoint, string P256dh, string Auth)? WebPush { get; set; }
    public PushRegistrationData(string token)
    {
        Token = token;
    }

    public PushRegistrationData(string Endpoint, string P256dh, string Auth) : this((Endpoint, P256dh, Auth))
    {
    }
    public PushRegistrationData((string Endpoint, string P256dh, string Auth) webPush)
    {
        WebPush = webPush;
    }
}
