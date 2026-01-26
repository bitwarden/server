# Seeder - Claude Code Context

## When to Use the Seeder

✅ Use for:

- Local development database setup
- Integration test data creation
- Performance testing with realistic encrypted data

❌ Do NOT use for:

- Production data
- Copying real user vaults (use backup/restore instead)

## Zero-Knowledge Architecture

**Critical Principle:** Unencrypted vault data never leaves the client. The server never sees plaintext.

### Why Seeder Uses the Rust SDK

The Seeder must behave exactly like any other Bitwarden client. Since the server:

- Never receives plaintext
- Cannot perform encryption (doesn't have keys)
- Only stores/retrieves encrypted blobs

...the Seeder cannot simply write plaintext to the database. It must:

1. Generate encryption keys (like a client does during account setup)
2. Encrypt vault data client-side (using the same SDK the real clients use)
3. Store only the encrypted result

This is why we use the Rust SDK via FFI - it's the same cryptographic implementation used by the official clients.

## Cipher Encryption Architecture

### The Two-State Pattern

Bitwarden uses a clean separation between encrypted and decrypted data:

| State     | SDK Type     | Description               | Stored in DB? |
| --------- | ------------ | ------------------------- | ------------- |
| Plaintext | `CipherView` | Decrypted, human-readable | Never         |
| Encrypted | `Cipher`     | EncString values          | Yes           |

**Encryption flow:**

```
CipherView (plaintext) → encrypt_composite() → Cipher (encrypted)
```

**Decryption flow:**

```
Cipher (encrypted) → decrypt() → CipherView (plaintext)
```

### EncString Format

All encrypted fields use the EncString format:

```
2.{base64_iv}|{base64_data}|{base64_mac}
│ └──────────┘ └──────────┘ └──────────┘
│     IV         Ciphertext      HMAC
└─ Type 2 = AES-256-CBC-HMAC-SHA256
```

### SDK vs Server Format Difference

**Critical:** The SDK and server use different JSON structures.

**SDK Cipher (nested):**

```json
{
  "name": "2.abc...",
  "login": {
    "username": "2.def...",
    "password": "2.ghi..."
  }
}
```

**Server Cipher.Data (flat CipherLoginData):**

```json
{
  "Name": "2.abc...",
  "Username": "2.def...",
  "Password": "2.ghi..."
}
```

The `CipherSeeder.TransformToServerCipher()` method performs this flattening.

### Data Flow in Seeder

```
┌─────────────────┐     ┌──────────────────┐     ┌─────────────────────┐
│  CipherViewDto  │────▶│  Rust SDK        │────▶│  EncryptedCipherDto │
│  (plaintext)    │     │  encrypt_cipher  │     │  (SDK Cipher)       │
└─────────────────┘     └──────────────────┘     └─────────────────────┘
                                                           │
                                                           ▼
                                               ┌───────────────────────┐
                                               │  TransformToServer    │
                                               │  (flatten nested →    │
                                               │   flat structure)     │
                                               └───────────────────────┘
                                                           │
                                                           ▼
┌─────────────────┐     ┌──────────────────┐     ┌─────────────────────┐
│  Server Cipher  │◀────│  CipherLoginData │◀────│  Flattened JSON     │
│  Entity         │     │  (serialized)    │     │                     │
└─────────────────┘     └──────────────────┘     └─────────────────────┘
```

### Key Files

| File                           | Purpose                                                |
| ------------------------------ | ------------------------------------------------------ |
| `Models/CipherViewDto.cs`      | Plaintext input matching SDK's CipherView              |
| `Models/EncryptedCipherDto.cs` | Parses SDK's encrypted Cipher output                   |
| `Factories/CipherSeeder.cs`    | Creates encrypted ciphers, transforms to server format |
| `Recipes/CiphersRecipe.cs`     | Bulk cipher creation with collection assignment        |

### Key Hierarchy

Bitwarden uses a two-level encryption hierarchy:

1. **User/Organization Key** - Encrypts the cipher's individual key
2. **Cipher Key** (optional) - Encrypts the actual cipher data

For seeding, we use the organization's symmetric key directly (no per-cipher key).

## Rust SDK FFI

### Available Functions

| Function                     | Input                 | Output                      |
| ---------------------------- | --------------------- | --------------------------- |
| `encrypt_cipher`             | CipherView JSON + key | Cipher JSON                 |
| `decrypt_cipher`             | Cipher JSON + key     | CipherView JSON             |
| `generate_organization_keys` | (none)                | Org symmetric key + keypair |

### Error Handling

SDK functions return JSON with an `"error"` field on failure:

```json
{ "error": "Failed to parse CipherView JSON" }
```

Always check for `"error"` in the response before parsing.

## Testing

Integration tests in `test/SeederApi.IntegrationTest/RustSdkCipherTests.cs` verify:

1. **Roundtrip encryption** - Encrypt then decrypt preserves plaintext
2. **Server format compatibility** - Output matches CipherLoginData structure
3. **Field encryption** - Custom fields are properly encrypted
4. **Security** - Plaintext never appears in encrypted output

## Common Patterns

### Creating a Cipher

```csharp
var sdk = new RustSdkService();
var seeder = new CipherSeeder(sdk);

var cipher = seeder.CreateOrganizationLoginCipher(
    organizationId,
    orgKey,  // Base64-encoded symmetric key
    name: "My Login",
    username: "user@example.com",
    password: "secret123");
```

### Bulk Cipher Creation

```csharp
var recipe = new CiphersRecipe(dbContext, sdkService);

var cipherIds = recipe.AddLoginCiphersToOrganization(
    organizationId,
    orgKey,
    collectionIds,
    count: 100);
```

## Security Reminders

- Generated test passwords are intentionally weak (`asdfasdfasdf`)
- Never commit database dumps containing seeded data to version control
- Seeded keys are for testing only - regenerate for each test run
