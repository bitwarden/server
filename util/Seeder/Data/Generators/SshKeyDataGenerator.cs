using System.Security.Cryptography;
using Bit.Seeder.Models;

namespace Bit.Seeder.Data.Generators;

/// <summary>
/// Generates structurally-valid but intentionally unusable SSH keys for test vault data.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Security by Design:</strong> These keys are deliberately marked with "FAKE" in the
/// PEM headers (e.g., "-----BEGIN FAKE RSA PRIVATE KEY-----") to ensure they cannot be
/// mistaken for or used as real credentials. The keys are cryptographically valid in structure
/// but are explicitly labeled to prevent any accidental production use.
/// </para>
/// <para>
/// <strong>Why realistic structure?</strong> Clients validate SSH key format for
/// display purposes (fingerprint rendering, key type detection, copy-to-clipboard formatting).
/// Using placeholder strings like "FAKE_KEY_HERE" would fail client-side validation and not
/// exercise the full code path during integration testing.
/// </para>
/// <para>
/// <strong>Context:</strong> This generator is part of the Seeder, which creates test data for
/// local development and integration testing only. All generated keys are encrypted with
/// organization keys before database storage, maintaining zero-knowledge architecture even
/// for test data.
/// </para>
/// <para>
/// <strong>Note:</strong> Keys are NOT deterministically seeded - RSA.Create() uses system RNG.
/// The pool provides variety but not cross-run reproducibility.
/// </para>
/// </remarks>
internal static class SshKeyDataGenerator
{
    private const int _poolSize = 500;

    private static readonly Lazy<(string Private, string Public, string Fingerprint)[]> _keyPool =
        new(() => GenerateKeyPool(_poolSize));

    /// <summary>
    /// Generates a deterministic SSH key based on index from the pre-generated pool.
    /// </summary>
    internal static SshKeyViewDto GenerateByIndex(int index)
    {
        var poolLength = _keyPool.Value.Length;
        var poolIndex = ((index % poolLength) + poolLength) % poolLength;
        var (Private, Public, Fingerprint) = _keyPool.Value[poolIndex];
        return new SshKeyViewDto
        {
            PrivateKey = Private,
            PublicKey = Public,
            Fingerprint = Fingerprint
        };
    }

    private static (string, string, string)[] GenerateKeyPool(int count)
    {
        var keys = new (string, string, string)[count];
        for (var i = 0; i < count; i++)
        {
            using var rsa = RSA.Create(2048);
            keys[i] = (ExportPrivateKey(rsa), ExportPublicKey(rsa), ComputeFingerprint(rsa));
        }
        return keys;
    }

    private static string ExportPrivateKey(RSA rsa)
    {
        var privateKeyBytes = rsa.ExportRSAPrivateKey();
        var base64 = Convert.ToBase64String(privateKeyBytes);
        var lines = new List<string> { "-----BEGIN FAKE RSA PRIVATE KEY-----" };
        for (var i = 0; i < base64.Length; i += 64)
        {
            lines.Add(base64.Substring(i, Math.Min(64, base64.Length - i)));
        }
        lines.Add("-----END FAKE RSA PRIVATE KEY-----");

        return string.Join("\n", lines);
    }

    private static string ExportPublicKey(RSA rsa)
    {
        var parameters = rsa.ExportParameters(false);

        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        WriteString(writer, "ssh-rsa");
        WriteBigInteger(writer, parameters.Exponent!);
        WriteBigInteger(writer, parameters.Modulus!);

        var keyBlob = Convert.ToBase64String(ms.ToArray());
        return $"ssh-rsa {keyBlob} test@seeder";
    }

    private static string ComputeFingerprint(RSA rsa)
    {
        var parameters = rsa.ExportParameters(false);

        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        WriteString(writer, "ssh-rsa");
        WriteBigInteger(writer, parameters.Exponent!);
        WriteBigInteger(writer, parameters.Modulus!);

        var hash = SHA256.HashData(ms.ToArray());
        return $"SHA256:{Convert.ToBase64String(hash).TrimEnd('=')}";
    }

    private static void WriteString(BinaryWriter writer, string value)
    {
        var bytes = System.Text.Encoding.ASCII.GetBytes(value);
        WriteBytes(writer, bytes);
    }

    private static void WriteBigInteger(BinaryWriter writer, byte[] value)
    {
        if (value.Length > 0 && (value[0] & 0x80) != 0)
        {
            var padded = new byte[value.Length + 1];
            padded[0] = 0;
            Array.Copy(value, 0, padded, 1, value.Length);
            WriteBytes(writer, padded);
        }
        else
        {
            WriteBytes(writer, value);
        }
    }

    private static void WriteBytes(BinaryWriter writer, byte[] bytes)
    {
        var length = BitConverter.GetBytes(bytes.Length);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(length);
        }
        writer.Write(length);
        writer.Write(bytes);
    }
}
