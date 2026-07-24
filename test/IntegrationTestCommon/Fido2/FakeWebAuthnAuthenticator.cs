using System.Buffers.Binary;
using System.Formats.Cbor;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Fido2NetLib;
using Fido2NetLib.Objects;

namespace Bit.IntegrationTestCommon.Fido2;

/// <summary>
/// Minimal in-memory WebAuthn authenticator for integration tests. Generates valid
/// ECDSA P-256 assertions and attestations that pass Fido2NetLib verification end-to-end.
/// </summary>
public sealed class FakeWebAuthnAuthenticator : IDisposable
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
            Id = Base64UrlEncode(CredentialId),
            RawId = CredentialId,
            Type = PublicKeyCredentialType.PublicKey,
            ClientExtensionResults = new AuthenticationExtensionsClientOutputs(),
            Response = new AuthenticatorAssertionRawResponse.AssertionResponse
            {
                AuthenticatorData = authenticatorData,
                Signature = signature,
                ClientDataJson = clientDataJson,
                UserHandle = userHandle,
            },
        };
    }

    /// <summary>
    /// Produce a valid "none"-format attestation for the given challenge and relying-party context,
    /// as returned by a fresh credential registration ceremony.
    /// </summary>
    public AuthenticatorAttestationRawResponse MakeAttestation(
        byte[] challenge,
        string rpId,
        string origin)
    {
        // clientDataJSON per WebAuthn spec
        var clientData = new
        {
            type = "webauthn.create",
            challenge = Base64UrlEncode(challenge),
            origin,
            crossOrigin = false,
        };
        var clientDataJson = JsonSerializer.SerializeToUtf8Bytes(clientData);

        // authenticatorData: rpIdHash (32) || flags (1) || signCount (4, big-endian) || attestedCredentialData
        var rpIdHash = SHA256.HashData(Encoding.UTF8.GetBytes(rpId));
        const byte flags = 0x45; // UP (0x01) | UV (0x04) | AT (0x40, attested credential data included)
        var signCountBytes = new byte[4]; // 0 for a freshly registered credential

        var aaguid = new byte[16];
        var credentialIdLength = new byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(credentialIdLength, (ushort)CredentialId.Length);
        var cosePublicKey = GetCosePublicKey();

        var attestedCredentialData = new byte[aaguid.Length + credentialIdLength.Length + CredentialId.Length + cosePublicKey.Length];
        var offset = 0;
        Buffer.BlockCopy(aaguid, 0, attestedCredentialData, offset, aaguid.Length); offset += aaguid.Length;
        Buffer.BlockCopy(credentialIdLength, 0, attestedCredentialData, offset, credentialIdLength.Length); offset += credentialIdLength.Length;
        Buffer.BlockCopy(CredentialId, 0, attestedCredentialData, offset, CredentialId.Length); offset += CredentialId.Length;
        Buffer.BlockCopy(cosePublicKey, 0, attestedCredentialData, offset, cosePublicKey.Length);

        var authenticatorData = new byte[rpIdHash.Length + 1 + signCountBytes.Length + attestedCredentialData.Length];
        offset = 0;
        Buffer.BlockCopy(rpIdHash, 0, authenticatorData, offset, rpIdHash.Length); offset += rpIdHash.Length;
        authenticatorData[offset] = flags; offset += 1;
        Buffer.BlockCopy(signCountBytes, 0, authenticatorData, offset, signCountBytes.Length); offset += signCountBytes.Length;
        Buffer.BlockCopy(attestedCredentialData, 0, authenticatorData, offset, attestedCredentialData.Length);

        var attestationObject = GetNoneAttestationObject(authenticatorData);

        return new AuthenticatorAttestationRawResponse
        {
            Id = Base64UrlEncode(CredentialId),
            RawId = CredentialId,
            Type = PublicKeyCredentialType.PublicKey,
            ClientExtensionResults = new AuthenticationExtensionsClientOutputs(),
            Response = new AuthenticatorAttestationRawResponse.AttestationResponse
            {
                AttestationObject = attestationObject,
                ClientDataJson = clientDataJson,
                Transports = [],
            },
        };
    }

    /// <summary>
    /// Builds a "none"-format attestation object CBOR map: { fmt: "none", attStmt: {}, authData: &lt;bytes&gt; }.
    /// "none" requires no signature or certificate chain, keeping this fake authenticator simple.
    /// </summary>
    private static byte[] GetNoneAttestationObject(byte[] authenticatorData)
    {
        var writer = new CborWriter(CborConformanceMode.Ctap2Canonical);
        writer.WriteStartMap(3);
        writer.WriteTextString("fmt");
        writer.WriteTextString("none");
        writer.WriteTextString("attStmt");
        writer.WriteStartMap(0);
        writer.WriteEndMap();
        writer.WriteTextString("authData");
        writer.WriteByteString(authenticatorData);
        writer.WriteEndMap();
        return writer.Encode();
    }

    private static string Base64UrlEncode(byte[] input)
        => Convert.ToBase64String(input)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    public void Dispose() => _keyPair.Dispose();
}
