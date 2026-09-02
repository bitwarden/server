using System.Buffers.Binary;
using System.Formats.Cbor;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Fido2NetLib;
using Fido2NetLib.Objects;

namespace Bit.Identity.IntegrationTest.RequestValidation.VaultAccess;

/// <summary>
/// Minimal in-memory WebAuthn authenticator for integration tests. Generates valid
/// ECDSA P-256 assertions that pass Fido2NetLib verification end-to-end.
/// </summary>
internal sealed class FakeWebAuthnAuthenticator : IDisposable
{
    private readonly ECDsa _keyPair;

    public byte[] CredentialId { get; } = RandomNumberGenerator.GetBytes(32);
    public uint SignatureCounter { get; private set; }

    public FakeWebAuthnAuthenticator()
    {
        _keyPair = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    }

    /// <summary>
    /// Returns the credential's public key as a COSE_Key CBOR map (what Fido2NetLib expects
    /// to see in the server-stored public key blob).
    /// </summary>
    public byte[] GetCosePublicKey()
    {
        var parameters = _keyPair.ExportParameters(includePrivateParameters: false);
        var writer = new CborWriter(CborConformanceMode.Ctap2Canonical);
        writer.WriteStartMap(5);
        // Per CTAP2 canonical ordering: keys sorted ascending as signed integers, with
        // non-negative keys before negative keys.
        writer.WriteInt32(1); writer.WriteInt32(2);             // kty = EC2
        writer.WriteInt32(3); writer.WriteInt32(-7);            // alg = ES256
        writer.WriteInt32(-1); writer.WriteInt32(1);            // crv = P-256
        writer.WriteInt32(-2); writer.WriteByteString(parameters.Q.X!);
        writer.WriteInt32(-3); writer.WriteByteString(parameters.Q.Y!);
        writer.WriteEndMap();
        return writer.Encode();
    }

    /// <summary>
    /// Produce a valid assertion for the given challenge and relying-party context.
    /// </summary>
    public AuthenticatorAssertionRawResponse MakeAssertion(
        byte[] challenge,
        string rpId,
        string origin,
        byte[] userHandle)
    {
        // clientDataJSON per WebAuthn spec
        var clientData = new
        {
            type = "webauthn.get",
            challenge = Base64UrlEncode(challenge),
            origin,
            crossOrigin = false,
        };
        var clientDataJson = JsonSerializer.SerializeToUtf8Bytes(clientData);

        // authenticatorData: rpIdHash (32) || flags (1) || signCount (4, big-endian)
        var rpIdHash = SHA256.HashData(Encoding.UTF8.GetBytes(rpId));
        const byte flags = 0x05; // UP (0x01) | UV (0x04)
        SignatureCounter++;
        var counterBytes = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(counterBytes, SignatureCounter);

        var authenticatorData = new byte[rpIdHash.Length + 1 + counterBytes.Length];
        Buffer.BlockCopy(rpIdHash, 0, authenticatorData, 0, rpIdHash.Length);
        authenticatorData[rpIdHash.Length] = flags;
        Buffer.BlockCopy(counterBytes, 0, authenticatorData, rpIdHash.Length + 1, counterBytes.Length);

        // Signature covers authenticatorData || SHA256(clientDataJson), encoded as DER
        var clientDataHash = SHA256.HashData(clientDataJson);
        var toSign = new byte[authenticatorData.Length + clientDataHash.Length];
        Buffer.BlockCopy(authenticatorData, 0, toSign, 0, authenticatorData.Length);
        Buffer.BlockCopy(clientDataHash, 0, toSign, authenticatorData.Length, clientDataHash.Length);

        var signature = _keyPair.SignData(toSign, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence);

        return new AuthenticatorAssertionRawResponse
        {
            Id = CredentialId,
            RawId = CredentialId,
            Type = PublicKeyCredentialType.PublicKey,
            Extensions = new AuthenticationExtensionsClientOutputs(),
            Response = new AuthenticatorAssertionRawResponse.AssertionResponse
            {
                AuthenticatorData = authenticatorData,
                Signature = signature,
                ClientDataJson = clientDataJson,
                UserHandle = userHandle,
            },
        };
    }

    private static string Base64UrlEncode(byte[] input)
        => Convert.ToBase64String(input)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    public void Dispose() => _keyPair.Dispose();
}
