using System.Security.Cryptography;
using System.Text;

namespace Bit.Core.Models.Data;
public class ServerProtectedData
{
    public string PlaintextData { get; set; }
    public string EncryptedData { get; set; }
    public byte[] Nonce { get; set; }
    public byte[] Tag { get; set; }
    public byte[] Ciphertext { get; set; }
    public byte[] Plaintext { get; set; }
    public int KeyId { get; set; }

    public Task<string> EncryptAsync()
    {
        KeyId = 1; // Current key to use

        var key = GetServerKey();
        using var aes = new AesGcm(key);
        Nonce = new byte[AesGcm.NonceByteSizes.MaxSize];
        RandomNumberGenerator.Fill(Nonce);
        Plaintext = Encoding.UTF8.GetBytes(PlaintextData);
        Ciphertext = new byte[Plaintext.Length];
        Tag = new byte[AesGcm.TagByteSizes.MaxSize];
        aes.Encrypt(Nonce, Plaintext, Ciphertext, Tag);

        BuildEncryptedData();
        return Task.FromResult(EncryptedData);
    }

    public Task<string> DecryptAsync()
    {
        if (!Protected())
        {
            throw new Exception(string.Format("{0} is not in the expected format.",
                nameof(EncryptedData)));
        }

        ParseEncryptedData();

        var key = GetServerKey();
        using var aes = new AesGcm(key);
        Plaintext = new byte[Ciphertext.Length];
        aes.Decrypt(Nonce, Ciphertext, Tag, Plaintext);

        PlaintextData = Encoding.UTF8.GetString(Plaintext);
        return Task.FromResult(PlaintextData);
    }

    public bool Protected()
    {
        return EncryptedData?.Contains("_") ?? false;
    }

    private void BuildEncryptedData()
    {
        var encryptedDataBytes = Array.Empty<byte>()
            .Concat(Nonce).Concat(Ciphertext).Concat(Tag).ToArray();
        EncryptedData = string.Format("{0}_{1}", KeyId, Convert.ToBase64String(encryptedDataBytes));
    }

    private void ParseEncryptedData()
    {
        var dataParts = EncryptedData.Split("_");
        KeyId = int.Parse(dataParts[0]);

        var encryptedDataBytes = Convert.FromBase64String(dataParts[1]);
        Nonce = new ArraySegment<byte>(encryptedDataBytes,
            0,
            AesGcm.NonceByteSizes.MaxSize).ToArray();
        Ciphertext = new ArraySegment<byte>(encryptedDataBytes,
            AesGcm.NonceByteSizes.MaxSize,
            encryptedDataBytes.Length - AesGcm.TagByteSizes.MaxSize).ToArray();
        Tag = new ArraySegment<byte>(encryptedDataBytes,
            AesGcm.NonceByteSizes.MaxSize + encryptedDataBytes.Length,
            AesGcm.TagByteSizes.MaxSize).ToArray();
    }

    private byte[] GetServerKey()
    {
        // TODO: Fetch key by KeyId
        var key = new byte[32];
        RandomNumberGenerator.Fill(key);
        return key;
    }
}
