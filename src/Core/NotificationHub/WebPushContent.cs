using System.Security.Cryptography;
using System.Text;
using Bit.Core.Utilities;

namespace Bit.Core.NotificationHub;

public class WebPushContent
{
    private byte[] _content { get; }
    public RecipientWebPushSubscription Subscription { get; set; }
    private Lazy<ECDiffieHellman> _senderDh;
    private Lazy<byte[]> _senderPublicKey;
    public string SenderPublicKey => CoreHelpers.Base64UrlEncode(_senderPublicKey.Value);
    private Lazy<byte[]> _salt;
    public string Salt => CoreHelpers.Base64UrlEncode(_salt.Value);

    public WebPushContent(byte[] content, RecipientWebPushSubscription subscription = null)
    {
        if (content == null)
        {
            throw new ArgumentNullException(nameof(content));
        }
        _content = content;
        Subscription = subscription;
        _salt = new Lazy<byte[]>(() =>
        {
            var salt = new byte[16];
            RandomNumberGenerator.Create().GetBytes(salt);
            return salt;
        });
        _senderDh = new Lazy<ECDiffieHellman>(ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256));
        _senderPublicKey = new Lazy<byte[]>(() =>
        {
            var publicKeyParameters = _senderDh.Value.ExportParameters(false);
            return new Span<byte>([0x04, .. publicKeyParameters.Q.X, .. publicKeyParameters.Q.Y]).ToArray();
        });
    }

    public HttpContent ToHttpContent()
    {
        var encryptedContent = EncryptContent(_content);
        var result = new ByteArrayContent(encryptedContent);
        result.Headers.Add("Content-Encoding", "aesgcm");
        result.Headers.Add("Content-Type", "application/octet-stream");
        return result;
    }

    private byte[] EncryptContent(byte[] content)
    {
        var derivedKey = _senderDh.Value.DeriveRawSecretAgreement(Subscription.PublicKey);

        var prk = HKDF.DeriveKey(HashAlgorithmName.SHA256, derivedKey, 32, Subscription.RecipientSecret, Encoding.UTF8.GetBytes("Content-Encoding: auth\0"));
        var contentEncryptionKey = HKDF.DeriveKey(HashAlgorithmName.SHA256, prk, 16, _salt.Value, CreateInfoChunk("aesgcm", Subscription.RecipientPublicKey, _senderPublicKey.Value));
        var nonce = HKDF.DeriveKey(HashAlgorithmName.SHA256, prk, 12, _salt.Value, CreateInfoChunk("nonce", Subscription.RecipientPublicKey, _senderPublicKey.Value));

        var contentBuffer = PadBytes(content, 128);

        // Encrypt aesgcm
        var cipher = new AesGcm(contentEncryptionKey, 16);
        var cipherText = new Span<byte>(new byte[contentBuffer.Length]);
        var tag = new Span<byte>(new byte[16]);
        cipher.Encrypt(nonce, contentBuffer.ToArray(), cipherText, tag);

        return new Span<byte>([.. cipherText, .. tag]).ToArray();
    }

    private static byte[] CreateInfoChunk(string type, byte[] recipientPublicKey, byte[] senderPublicKey)
    {
        var output = new List<byte>();
        output.AddRange(Encoding.UTF8.GetBytes($"Content-Encoding: {type}\0P-256\0"));
        output.AddRange(ByteConvert((ushort)recipientPublicKey.Length));
        output.AddRange(recipientPublicKey);
        output.AddRange(ByteConvert((ushort)senderPublicKey.Length));
        output.AddRange(senderPublicKey);
        return output.ToArray();
    }

    private static byte[] ByteConvert(ushort value)
    {
        var result = BitConverter.GetBytes(value);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(result);
        }
        return result;
    }

    private byte[] PadBytes(byte[] bytes, int blockSize)
    {
        var paddingLength = blockSize - (bytes.Length % blockSize);
        var output = new List<byte>();
        output.AddRange(ByteConvert((ushort)paddingLength));
        output.AddRange(new byte[paddingLength]);
        output.AddRange(bytes);
        return output.ToArray();
    }
}
