using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bit.Core.Utilities;

namespace Bit.Core.NotificationHub;

public class WebPushRequest
{
    private string VapidPrivateKey { get; init; }
    public string VapidPublicKey { get; init; }
    public RecipientWebPushSubscription Subscription { get; set; }
    public WebPushContent Content { get; set; }
    public WebPushRequest(RecipientWebPushSubscription subscription, string vapidPrivateKey, string vapidPublicKey)
    {
        if (subscription == null)
        {
            throw new ArgumentNullException(nameof(subscription));
        }
        Subscription = subscription;

        if (string.IsNullOrWhiteSpace(vapidPublicKey))
        {
            throw new ArgumentNullException(nameof(vapidPublicKey));
        }
        VapidPublicKey = vapidPublicKey;

        if (string.IsNullOrWhiteSpace(vapidPrivateKey))
        {
            throw new ArgumentNullException(nameof(vapidPrivateKey));
        }
        VapidPrivateKey = vapidPrivateKey;
    }

    public HttpRequestMessage ToHttpRequestMessage()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, Subscription.Endpoint);
        request.Headers.Add("Authorization", VapidAuthHeader(Subscription.Endpoint, VapidPrivateKey, VapidPublicKey));
        if (Content != null)
        {
            request.Content = Content.ToHttpContent(Subscription);
        }

        return request;
    }

    // Can build a service to generate these with a memory cache per PNS for performance.
    public static string VapidAuthHeader(string audience, string vapidPrivateKey, string vapidPublicKey)
    {
        var subject = "mailto:webpush_ops@bitwarden.com";
        var expiration = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 43200; // 12 hours

        var privateApplicationServerKey = CoreHelpers.Base64UrlDecode(vapidPrivateKey);

        var tokenHeader = new
        {
            typ = "JWT",
            alg = "ES256"
        };

        var tokenBody = new
        {
            aud = new UriBuilder()
            {
                Scheme = "https",
                Host = new Uri(audience).Host
            }.Uri.ToString(),
            exp = expiration,
            sub = subject
        };

        var unsignedToken = CoreHelpers.Base64UrlEncodeString(JsonSerializer.Serialize(tokenHeader)) + "." +
            CoreHelpers.Base64UrlEncodeString(JsonSerializer.Serialize(tokenBody));

        using var ecdsa = ECDsa.Create(
            new ECParameters()
            {
                Curve = ECCurve.NamedCurves.nistP256,
                D = privateApplicationServerKey,
            }
        );
        var signature = ecdsa.SignData(Encoding.UTF8.GetBytes(unsignedToken), HashAlgorithmName.SHA256);

        return $"vapid t={unsignedToken}.{CoreHelpers.Base64UrlEncode(signature)}, k={vapidPublicKey}";
    }

}

public class RecipientWebPushSubscription
{
    public string Endpoint { get; set; }
    public byte[] RecipientPublicKey { get; set; }
    public byte[] RecipientSecret { get; set; }
    private ECDiffieHellman _eCDiffieHellman;
    public ECDiffieHellmanPublicKey PublicKey
    {
        get => _eCDiffieHellman.PublicKey;
    }

    public RecipientWebPushSubscription(string endpoint, string p256dh, string auth)
    {
        if (!endpoint.StartsWith("https://"))
        {
            throw new ArgumentException("Endpoint must be a secure URL");
        }

        Endpoint = endpoint;
        RecipientPublicKey = CoreHelpers.Base64UrlDecode(p256dh);
        RecipientSecret = CoreHelpers.Base64UrlDecode(auth);

        if (RecipientPublicKey[0] != 0x04)
        {
            throw new ArgumentException("Only uncompressed public key representations supported");
        }
        if (RecipientPublicKey.Length != 65)
        {
            throw new ArgumentException("Invalid public key length");
        }

        _eCDiffieHellman = ECDiffieHellman.Create(new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            Q = new ECPoint
            {
                // first byte is 0x04, which indicates an uncompressed public key
                X = RecipientPublicKey[1..33],
                Y = RecipientPublicKey[33..]
            }
        });
    }
}
