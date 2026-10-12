using System.Buffers.Binary;
using System.Buffers.Text;
using System.Formats.Cbor;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models;
using Bit.Core.Entities;
using Fido2NetLib;
using Fido2NetLib.Objects;

namespace Bit.Test.Common.Fakes;

/// <summary>
/// Minimal in-memory WebAuthn authenticator for integration tests. Generates valid
/// ECDSA P-256 assertions and attestations that pass Fido2NetLib verification end-to-end.
/// </summary>
public sealed class FakeWebAuthnAuthenticator : IDisposable
{
    /// <summary>
    /// A realistic signature counter for a key that someone used with U2F before the 2020 migration, which carried
    /// the U2F counter into the WebAuthn record. It is above zero, so the server's counter check compares against it.
    /// </summary>
    public const uint CarriedOverU2fCounter = 7;

    private readonly ECDsa _keyPair;

    public byte[] CredentialId { get; }
    public uint SignatureCounter { get; set; }

    public FakeWebAuthnAuthenticator(byte[]? credentialId = null)
    {
        CredentialId = credentialId ?? RandomNumberGenerator.GetBytes(32);
        _keyPair = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    }

    /// <summary>
    /// Returns the public key in the raw ANSI X9.62 uncompressed form a legacy U2F registration
    /// carries: 0x04 || X (32 bytes) || Y (32 bytes).
    /// </summary>
    public byte[] GetU2fRawPublicKey()
    {
        var parameters = _keyPair.ExportParameters(includePrivateParameters: false);
        return [0x04, .. parameters.Q.X!, .. parameters.Q.Y!];
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
    /// Returns the public key as the 2020 U2F-to-WebAuthn migration stored it: a COSE EC2 / ES256 / P-256 key
    /// built from the raw U2F key, with the map keys in CTAP2 canonical order (1, 3, -1, -2, -3).
    /// </summary>
    public byte[] GetMigratedU2fCosePublicKey()
    {
        var u2fPublicKey = GetU2fRawPublicKey();
        var x = u2fPublicKey[1..33];
        var y = u2fPublicKey[33..65];

        var writer = new CborWriter(CborConformanceMode.Ctap2Canonical);
        writer.WriteStartMap(5);
        writer.WriteInt32(1); writer.WriteInt32(2);             // kty = EC2
        writer.WriteInt32(3); writer.WriteInt32(-7);            // alg = ES256
        writer.WriteInt32(-1); writer.WriteInt32(1);            // crv = P-256
        writer.WriteInt32(-2); writer.WriteByteString(x);
        writer.WriteInt32(-3); writer.WriteByteString(y);
        writer.WriteEndMap();
        return writer.Encode();
    }

    /// <summary>
    /// Returns the user's <c>TwoFactorProviders</c> JSON as the 2020 U2F-to-WebAuthn migration wrote it, for a user
    /// who has not saved their two-factor providers since. The migration serialized the whole provider dictionary
    /// with Newtonsoft defaults and the Fido2 1.1.0 models, which gives these properties:
    /// <list type="number">
    /// <item>Provider keys are enum names ("U2f", "WebAuthn"), not numbers.</item>
    /// <item>The original U2f entry stays next to the WebAuthn entry, and both use the U2F key name "Key1".</item>
    /// <item>The descriptor is <c>{"type":"public-key","id":&lt;Base64url&gt;}</c>.</item>
    /// </list>
    /// Sets <see cref="SignatureCounter"/> to <paramref name="signatureCounter"/>, so the stored counter and the
    /// key's counter match. The server ignores the U2f entry, so its certificate is opaque filler.
    /// </summary>
    public string GetMigratedU2fTwoFactorProvidersJson(string name, uint signatureCounter)
    {
        SignatureCounter = signatureCounter;

        var providers = new JsonObject
        {
            ["U2f"] = new JsonObject
            {
                ["Enabled"] = true,
                ["MetaData"] = new JsonObject
                {
                    ["Key1"] = new JsonObject
                    {
                        ["Name"] = name,
                        ["KeyHandle"] = Base64Url.EncodeToString(CredentialId),
                        ["PublicKey"] = Base64Url.EncodeToString(GetU2fRawPublicKey()),
                        ["Certificate"] = Base64Url.EncodeToString("u2f-attestation-certificate"u8),
                        ["Counter"] = SignatureCounter,
                        ["Compromised"] = false,
                    },
                },
            },
            ["WebAuthn"] = new JsonObject
            {
                ["Enabled"] = true,
                ["MetaData"] = new JsonObject
                {
                    ["Key1"] = new JsonObject
                    {
                        ["Name"] = name,
                        ["Descriptor"] = new JsonObject
                        {
                            ["type"] = "public-key",
                            ["id"] = Base64Url.EncodeToString(CredentialId),
                        },
                        ["PublicKey"] = Convert.ToBase64String(GetMigratedU2fCosePublicKey()),
                        ["UserHandle"] = null,
                        ["SignatureCounter"] = SignatureCounter,
                        ["CredType"] = null,
                        ["RegDate"] = "0001-01-01T00:00:00",
                        ["AaGuid"] = "00000000-0000-0000-0000-000000000000",
                        ["Migrated"] = true,
                    },
                },
            },
        };

        // Match Newtonsoft, which does not escape '+' in Base64 strings.
        return providers.ToJsonString(new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
    }

    /// <summary>
    /// Returns the <see cref="GetMigratedU2fTwoFactorProvidersJson"/> record after the server saved each key
    /// again as a <see cref="TwoFactorProvider.WebAuthnData"/>, as a successful two-factor login with the key does.
    /// </summary>
    public string GetResavedMigratedU2fTwoFactorProvidersJson(string name, uint signatureCounter)
    {
        var user = new User { TwoFactorProviders = GetMigratedU2fTwoFactorProvidersJson(name, signatureCounter) };
        var providers = user.GetTwoFactorProviders()!;
        var metaData = providers[TwoFactorProviderType.WebAuthn].MetaData;
        foreach (var keyName in metaData.Keys.Where(k => k.StartsWith("Key")).ToList())
        {
            metaData[keyName] = new TwoFactorProvider.WebAuthnData((dynamic)metaData[keyName]);
        }

        user.SetTwoFactorProviders(providers);
        return user.TwoFactorProviders;
    }

    /// <summary>
    /// A U2F key handle is an opaque, authenticator-chosen blob (64 bytes here). The leading bytes make the
    /// Base64url form ('-', '_') differ from the standard Base64 form ('+', '/'), so decoding a stored
    /// descriptor id with the wrong alphabet fails.
    /// </summary>
    public static byte[] GetLegacyU2fKeyHandle()
    {
        var keyHandle = new byte[64];
        for (var i = 0; i < keyHandle.Length; i++)
        {
            keyHandle[i] = (byte)(i * 7 + 3);
        }
        keyHandle[0] = 0xfb;
        keyHandle[1] = 0xff;
        return keyHandle;
    }

    /// <summary>
    /// Same JSON shape as <c>buildDataString</c> in clients/apps/web/src/connectors/common-webauthn.ts.
    /// The extension results travel under the "extensions" key, as the web client sends them.
    /// </summary>
    public static string MakeWebClientTokenString(AuthenticatorAssertionRawResponse assertion)
    {
        return JsonSerializer.Serialize(new
        {
            id = assertion.Id,
            rawId = Base64Url.EncodeToString(assertion.RawId),
            type = "public-key",
            extensions = assertion.ClientExtensionResults.AppID == true
                ? new Dictionary<string, object> { ["appid"] = true }
                : new Dictionary<string, object>(),
            response = new
            {
                authenticatorData = Base64Url.EncodeToString(assertion.Response.AuthenticatorData),
                clientDataJson = Base64Url.EncodeToString(assertion.Response.ClientDataJson),
                signature = Base64Url.EncodeToString(assertion.Response.Signature),
            },
        });
    }

    /// <summary>
    /// Produce a valid assertion for the given challenge and relying-party context.
    /// When <paramref name="appId"/> is set, the authenticator data is scoped to that AppID
    /// instead of <paramref name="rpId"/> and the client extension results report
    /// <c>appid: true</c>, as a browser does after falling back to a U2F-scoped credential.
    /// </summary>
    public AuthenticatorAssertionRawResponse MakeAssertion(
        byte[] challenge,
        string rpId,
        string origin,
        byte[]? userHandle,
        string? appId = null)
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
        var rpIdHash = SHA256.HashData(Encoding.UTF8.GetBytes(appId ?? rpId));
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
            ClientExtensionResults = new AuthenticationExtensionsClientOutputs
            {
                AppID = appId is not null,
            },
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
