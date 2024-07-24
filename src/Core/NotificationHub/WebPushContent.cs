using System.Security.Cryptography;
using System.Text;
using Bit.Core.Utilities;

namespace Bit.Core.NotificationHub;

public class WebPushContent
{
    const string _contentEncoding = "aes128gcm";
    const int _headingSize = 86;
    const int _padLengthGoal = 1024;
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
        _senderPublicKey = new Lazy<byte[]>(() => UncompressedPublicKey(_senderDh.Value.PublicKey));
    }

    private static byte[] UncompressedPublicKey(ECDiffieHellmanPublicKey publicKey)
    {
        var publicKeyParameters = publicKey.ExportParameters();
        return new Span<byte>([0x04, .. publicKeyParameters.Q.X, .. publicKeyParameters.Q.Y]).ToArray();
    }

    public HttpContent ToHttpContent(RecipientWebPushSubscription subscription)
    {
        if (subscription == null)
        {
            throw new ArgumentNullException(nameof(subscription));
        }
        Subscription = subscription;
        var encryptedContent = EncryptContent(_content);
        var result = new ByteArrayContent(encryptedContent);
        result.Headers.Add("Content-Encoding", _contentEncoding);
        return result;
    }

    private byte[] EncryptContent(byte[] content)
    {
        var ecdhSecret = _senderDh.Value.DeriveRawSecretAgreement(Subscription.PublicKey);

        var prk = HKDF.DeriveKey(HashAlgorithmName.SHA256, ecdhSecret, 32, Subscription.RecipientSecret, CreateInfo("WebPush: info", UncompressedPublicKey(Subscription.PublicKey), _senderPublicKey.Value));
        var contentEncryptionKey = HKDF.DeriveKey(HashAlgorithmName.SHA256, prk, 16, _salt.Value, CreateInfo("Content-Encoding: aes128gcm"));
        var nonce = HKDF.DeriveKey(HashAlgorithmName.SHA256, prk, 12, _salt.Value, CreateInfo("Content-Encoding: nonce"));
        var record_size = content.Length > _padLengthGoal - _headingSize - 1 ?
            content.Length + 1 : // Content + 1 padding byte
             1024 - _headingSize; // typical padded size
        var header = CreateHeaderBlock(_salt.Value, record_size, _senderPublicKey.Value);
        var contentBuffer = PadBytes(content, record_size, true);

        // Encrypt aesgcm
        var cipher = new AesGcm(contentEncryptionKey, 16);
        var cipherText = new Span<byte>(new byte[contentBuffer.Length]);
        var tag = new Span<byte>(new byte[16]);
        cipher.Encrypt(nonce, contentBuffer.ToArray(), cipherText, tag);

        return new Span<byte>([.. header, .. cipherText, .. tag]).ToArray();
    }

    private static byte[] CreateInfo(string type, params byte[][] values)
    {
        var output = new List<byte>();
        output.AddRange(Encoding.UTF8.GetBytes(type));
        output.Add(0);
        foreach (var value in values)
        {
            output.AddRange(value);
        }
        return output.ToArray();
    }

    private static byte[] ByteConvert(byte value) => [value];
    private static byte[] ByteConvert(ushort value, ushort byteLength = 2) => ByteConvert((int)value, byteLength);
    private static byte[] ByteConvert(int value, ushort byteLength)
    {
        var result = BitConverter.GetBytes(value);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(result);
        }
        return result.Take(byteLength).ToArray();
    }

    private byte[] PadBytes(byte[] bytes, int blockSize, bool isLastBlock)
    {
        if (!isLastBlock && bytes.Length != blockSize - 16 - 1)
        {
            // 16 bytes for the tag, 1 byte for the padding length
            throw new ArgumentException("Invalid block size");
        }
        var output = new List<byte>();
        output.AddRange(bytes);
        if (isLastBlock)
        {
            var paddingLength = blockSize - bytes.Length - 1;
            output.Add(2);
            output.AddRange(new byte[paddingLength]);
        }
        else
        {
            output.Add(1);
        }
        return output.ToArray();
    }

    private static byte[] CreateHeaderBlock(byte[] salt, int rs, byte[] keyId)
    {
        var output = new List<byte>();
        output.AddRange(salt);
        output.AddRange(ByteConvert(rs, 4));
        output.AddRange(ByteConvert((byte)keyId.Length));
        output.AddRange(keyId);
        return output.ToArray();
    }
}
