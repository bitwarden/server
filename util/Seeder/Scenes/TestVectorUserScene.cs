using System.ComponentModel.DataAnnotations;
using System.Formats.Cbor;
using System.Text.Json;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.KeyManagement.Enums;
using Bit.Core.KeyManagement.Models.Data;
using Bit.Core.Repositories;
using Bit.Core.Utilities;
using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Enums;
using Bit.Core.Vault.Models.Data;
using Bit.Core.Vault.Repositories;
using Bit.Seeder.Factories;
using Bit.Seeder.Models;
using Bit.Seeder.Services;
using Microsoft.AspNetCore.Identity;

namespace Bit.Seeder.Scenes;

/// <summary>
/// Loads one of the SDK's committed user test vectors (<c>sdk-internal/test-vectors/users/*.json</c>) as it is: the
/// account and its vault are already encrypted, so nothing is generated or re-encrypted. A client that unlocks the
/// seeded account with the vector's password reads exactly the vault the vector records.
/// </summary>
/// <remarks>
/// <para>
/// The request is the vector file itself. Only what a server holds is read from it: wrapped keys, public keys, the
/// master password authentication hash, and the encrypted vault. The plaintext the vector records for its own
/// assertions (<c>rawCryptographicState</c>'s private halves, decrypted unlock methods, decrypted items) is ignored.
/// </para>
/// <para>
/// Each seeding gets fresh identifiers and a mangled email, so a vector can be seeded any number of times. The
/// master password salt stays the vector's email, which the wrapped user key was derived with. The result maps the
/// vector's item ids to the seeded ones.
/// </para>
/// <para>
/// Supports master-password vectors, V1 and V2: ciphers (field-level or blob-encrypted, with their attachment
/// metadata, folder and favorite) and folders. Sends, organizations, and the trusted-device and Key Connector
/// vectors are not seeded.
/// </para>
/// </remarks>
public class TestVectorUserScene(
    IPasswordHasher<User> passwordHasher,
    IUserRepository userRepository,
    ICipherRepository cipherRepository,
    IFolderRepository folderRepository,
    IManglerService manglerService) : IScene<TestVectorUserScene.Request, TestVectorUserScene.Result>
{
    /// <summary>COSE algorithm identifiers a verifying key names (RFC 9053; ML-DSA draft).</summary>
    private const int CoseAlgorithmLabel = 3;
    private const int CoseEdDsa = -8;
    private const int CoseMlDsa44 = -48;

    private static readonly JsonSerializerOptions _vectorJsonOptions = new() { PropertyNameCaseInsensitive = true };

    public class Request
    {
        [Required]
        public required string Name { get; set; }
        [Required]
        public required VectorAccount Account { get; set; }
        [Required]
        public required List<JsonElement> UnlockMethods { get; set; }
        public string? MasterPasswordAuthenticationHash { get; set; }
        [Required]
        public required VectorRawCryptographicState RawCryptographicState { get; set; }
        public VectorVault Vault { get; set; } = new();
    }

    public class VectorAccount
    {
        [Required]
        public required string Email { get; set; }
        public int? SecurityVersion { get; set; }
        [Required]
        public required JsonElement AccountCryptographicState { get; set; }
    }

    /// <summary>The public halves the vector records. Its private halves are never read.</summary>
    public class VectorRawCryptographicState
    {
        [Required]
        public required string PublicKey { get; set; }
        public string? VerifyingKey { get; set; }
        public string? UserKeyId { get; set; }
    }

    public class VectorVault
    {
        public List<VectorItem> Ciphers { get; set; } = [];
        public List<VectorItem> Folders { get; set; } = [];
    }

    public class VectorItem
    {
        [Required]
        public required string Id { get; set; }
        [Required]
        public required JsonElement Encrypted { get; set; }
    }

    public class Result
    {
        public Guid UserId { get; init; }
        public required string Email { get; init; }
        /// <summary>Vector cipher id → seeded cipher id.</summary>
        public required Dictionary<string, Guid> CipherIds { get; init; }
        /// <summary>Vector folder id → seeded folder id.</summary>
        public required Dictionary<string, Guid> FolderIds { get; init; }
    }

    public async Task<SceneResult<Result>> SeedAsync(Request request)
    {
        var unlock = MasterPasswordUnlock(request);
        if (string.IsNullOrEmpty(request.MasterPasswordAuthenticationHash))
        {
            throw new ArgumentException($"Vector {request.Name} records no master password authentication hash.");
        }

        // 1. The account: wrapped keys as the vector records them, under a fresh id and a mangled email
        var email = manglerService.Mangle(request.Account.Email);
        var user = new User
        {
            Id = CombGuid.Generate(),
            Name = request.Name,
            Email = email,
            EmailVerified = true,
            SecurityStamp = Guid.NewGuid().ToString(),
            ApiKey = CoreHelpers.SecureRandomString(30),
            Key = unlock.MasterKeyWrappedUserKey,
            MasterPasswordSalt = unlock.Salt,
            PublicKey = request.RawCryptographicState.PublicKey,
            PrivateKey = AccountState(request).GetProperty("private_key").GetString(),
            UserKeyId = request.RawCryptographicState.UserKeyId ?? unlock.ContainedKeyId,
        };
        ApplyKdf(user, unlock.Kdf);
        user.MasterPassword = passwordHasher.HashPassword(user, request.MasterPasswordAuthenticationHash);

        await userRepository.CreateAsync(user);

        // 2. A V2 account's signature key pair and security state
        if (V2AccountKeys(request) is { } v2Keys)
        {
            await userRepository.SetV2AccountCryptographicStateAsync(user.Id, v2Keys);
        }

        // 3. The vault, already encrypted, under fresh ids
        var folderIds = new Dictionary<string, Guid>();
        foreach (var item in request.Vault.Folders)
        {
            var folder = new Folder
            {
                Id = CombGuid.Generate(),
                UserId = user.Id,
                Name = item.Encrypted.GetProperty("name").GetString(),
            };
            await folderRepository.CreateAsync(folder);
            folderIds[item.Id] = folder.Id;
        }

        var cipherIds = new Dictionary<string, Guid>();
        foreach (var item in request.Vault.Ciphers)
        {
            var cipher = CreateCipher(item.Encrypted, user.Id, folderIds);
            await cipherRepository.CreateAsync(cipher);
            cipherIds[item.Id] = cipher.Id;
        }

        return new SceneResult<Result>(
            result: new Result { UserId = user.Id, Email = email, CipherIds = cipherIds, FolderIds = folderIds },
            mangleMap: manglerService.GetMangleMap());
    }

    private record Unlock(string MasterKeyWrappedUserKey, string Salt, JsonElement Kdf, string? ContainedKeyId);

    /// <summary>The vector's master-password unlock data, or a refusal for a vector that has none.</summary>
    private static Unlock MasterPasswordUnlock(Request request)
    {
        foreach (var method in request.UnlockMethods)
        {
            if (!method.TryGetProperty("masterPasswordUnlock", out var wrapper))
            {
                continue;
            }

            var unlock = wrapper.GetProperty("master_password_unlock");
            return new Unlock(
                unlock.GetProperty("masterKeyWrappedUserKey").GetString()!,
                unlock.GetProperty("salt").GetString()!,
                unlock.GetProperty("kdf"),
                unlock.TryGetProperty("containedKeyId", out var keyId) ? keyId.GetString() : null);
        }

        throw new ArgumentException(
            $"Vector {request.Name} has no master-password unlock method; only master-password vectors can be seeded.");
    }

    /// <summary>The <c>V1</c> or <c>V2</c> object of the vector's wrapped account cryptographic state.</summary>
    private static JsonElement AccountState(Request request)
    {
        var state = request.Account.AccountCryptographicState;
        return state.TryGetProperty("V2", out var v2) ? v2 : state.GetProperty("V1");
    }

    private static UserAccountKeysData? V2AccountKeys(Request request)
    {
        if (!request.Account.AccountCryptographicState.TryGetProperty("V2", out var v2))
        {
            return null;
        }

        var verifyingKey = request.RawCryptographicState.VerifyingKey
            ?? throw new ArgumentException($"V2 vector {request.Name} records no verifying key.");

        return new UserAccountKeysData
        {
            PublicKeyEncryptionKeyPairData = new PublicKeyEncryptionKeyPairData(
                v2.GetProperty("private_key").GetString()!,
                request.RawCryptographicState.PublicKey,
                v2.GetProperty("signed_public_key").GetString()),
            SignatureKeyPairData = new SignatureKeyPairData(
                SignatureAlgorithmOf(verifyingKey),
                v2.GetProperty("signing_key").GetString()!,
                verifyingKey),
            SecurityStateData = new SecurityStateData
            {
                SecurityState = v2.GetProperty("security_state").GetString()!,
                SecurityVersion = request.Account.SecurityVersion ?? 2,
            },
        };
    }

    /// <summary>The algorithm a COSE verifying key names, in its <c>alg</c> (label 3) entry.</summary>
    private static SignatureAlgorithm SignatureAlgorithmOf(string verifyingKeyB64)
    {
        var reader = new CborReader(Convert.FromBase64String(verifyingKeyB64));
        var entries = reader.ReadStartMap();
        for (var i = 0; entries is null || i < entries; i++)
        {
            if (reader.PeekState() == CborReaderState.EndMap)
            {
                break;
            }

            var label = reader.ReadInt32();
            if (label != CoseAlgorithmLabel)
            {
                reader.SkipValue();
                continue;
            }

            return reader.ReadInt32() switch
            {
                CoseEdDsa => SignatureAlgorithm.Ed25519,
                CoseMlDsa44 => SignatureAlgorithm.MlDsa44,
                var other => throw new ArgumentException($"Unsupported verifying key algorithm {other}."),
            };
        }

        throw new ArgumentException("The verifying key names no algorithm.");
    }

    /// <summary>Applies the vector's KDF: <c>{"pBKDF2": {...}}</c> or <c>{"argon2id": {...}}</c>.</summary>
    private static void ApplyKdf(User user, JsonElement kdf)
    {
        if (kdf.TryGetProperty("pBKDF2", out var pbkdf2))
        {
            user.Kdf = KdfType.PBKDF2_SHA256;
            user.KdfIterations = pbkdf2.GetProperty("iterations").GetInt32();
            return;
        }

        var argon2 = kdf.GetProperty("argon2id");
        user.Kdf = KdfType.Argon2id;
        user.KdfIterations = argon2.GetProperty("iterations").GetInt32();
        user.KdfMemory = argon2.GetProperty("memory").GetInt32();
        user.KdfParallelism = argon2.GetProperty("parallelism").GetInt32();
    }

    /// <summary>
    /// A personal cipher from the vector's encrypted one: a blob-encrypted cipher's <c>data</c> as it is, or a
    /// field-level cipher's fields in the server's per-type shape.
    /// </summary>
    private static Cipher CreateCipher(JsonElement encrypted, Guid userId, Dictionary<string, Guid> folderIds)
    {
        var dto = encrypted.Deserialize<EncryptedCipherDto>(_vectorJsonOptions)
            ?? throw new ArgumentException("A vector cipher did not parse.");
        var type = (CipherType)dto.Type;
        var blob = encrypted.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.String
            ? data.GetString()
            : null;

        var cipher = new Cipher
        {
            Id = CombGuid.Generate(),
            UserId = userId,
            Type = type,
            Key = dto.Key,
            Reprompt = (CipherRepromptType?)dto.Reprompt,
            Data = blob ?? FieldLevelData(dto, type),
            CreationDate = dto.CreationDate,
            RevisionDate = dto.RevisionDate,
            DeletedDate = dto.DeletedDate,
        };

        if (encrypted.TryGetProperty("folderId", out var folderId) && folderId.ValueKind == JsonValueKind.String)
        {
            cipher.Folders = CipherComposer.BuildFoldersJson(new() { [userId] = folderIds[folderId.GetString()!] });
        }

        if (dto.Favorite)
        {
            cipher.Favorites = CipherComposer.BuildFavoritesJson([userId]);
        }

        if (encrypted.TryGetProperty("attachments", out var attachments) && attachments.ValueKind == JsonValueKind.Array)
        {
            cipher.SetAttachments(attachments.EnumerateArray().ToDictionary(
                attachment => attachment.GetProperty("id").GetString()!,
                attachment => new CipherAttachment.MetaData
                {
                    FileName = attachment.GetProperty("fileName").GetString(),
                    Key = attachment.TryGetProperty("key", out var key) ? key.GetString() : null,
                    Size = AttachmentSize(attachment),
                }));
        }

        return cipher;
    }

    /// <summary>An attachment's size, which the SDK records as a string.</summary>
    private static long AttachmentSize(JsonElement attachment)
    {
        if (!attachment.TryGetProperty("size", out var size))
        {
            return 0;
        }

        return size.ValueKind switch
        {
            JsonValueKind.Number => size.GetInt64(),
            JsonValueKind.String when long.TryParse(size.GetString(), out var bytes) => bytes,
            _ => 0,
        };
    }

    private static string FieldLevelData(EncryptedCipherDto dto, CipherType type) =>
        CipherEncryption.CreateEntity(dto, type switch
        {
            CipherType.Login => dto.ToLoginData(),
            CipherType.Card => dto.ToCardData(),
            CipherType.Identity => dto.ToIdentityData(),
            CipherType.SecureNote => dto.ToSecureNoteData(),
            CipherType.SSHKey => dto.ToSshKeyData(),
            CipherType.BankAccount => dto.ToBankAccountData(),
            CipherType.DriversLicense => dto.ToDriversLicenseData(),
            CipherType.Passport => dto.ToPassportData(),
            _ => throw new ArgumentException($"Unsupported cipher type {type}."),
        }, type, organizationId: null, userId: null).Data;
}
