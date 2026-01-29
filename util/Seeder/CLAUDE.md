# Seeder - Claude Code Context

## Ubiquitous Language

The Seeder follows six core patterns:

1. **Factories** - Create ONE entity with encryption. Named `{Entity}Seeder` with `Create{Type}{Entity}()` methods. Do not interact with database.

2. **Recipes** - Orchestrate MANY entities. Named `{DomainConcept}Recipe`. **MUST have `Seed()` method** as primary interface, not `AddToOrganization()` or similar. Use parameters for variations, not separate methods. Compose Factories internally.

3. **Models** - DTOs bridging SDK ↔ Server format. Named `{Entity}ViewDto` (plaintext), `Encrypted{Entity}Dto` (SDK format). Pure data, no logic.

4. **Scenes** - Complete test scenarios with ID mangling. Implement `IScene<TReques, TResult>`. Async, returns `SceneResult<TResult>` with MangleMap and result property populated with `TResult`. Named `{Scenario}Scene`.

5. **Queries** - Read-only data retrieval. Implement `IQuery<TRequest, TResult>`. Synchronous, no DB modifications. Named `{DataToRetrieve}Query`.

6. **Data** - Static, filterable test data collections (Companies, Passwords, Names, OrgStructures). Deterministic, composable. Enums provide public API.

## The Recipe Contract

Recipes follow strict rules (like a cooking recipe that you follow completely):

1. A Recipe SHALL have exactly one public method named `Seed()`
2. A Recipe MUST produce one cohesive result (like baking one complete cake)
3. A Recipe MAY have overloaded `Seed()` methods with different parameters
4. A Recipe SHALL use private helper methods for internal steps
5. A Recipe SHALL use BulkCopy for performance when creating multiple entities
6. A Recipe SHALL compose Factories for individual entity creation
7. A Recipe SHALL NOT expose implementation details as public methods

**Current violations** (to be refactored):

- `CiphersRecipe` - Uses `AddLoginCiphersToOrganization()` instead of `Seed()`
- `CollectionsRecipe` - Uses `AddFromStructure()` and `AddToOrganization()` instead of `Seed()`
- `GroupsRecipe` - Uses `AddToOrganization()` instead of `Seed()`
- `OrganizationDomainRecipe` - Uses `AddVerifiedDomainToOrganization()` instead of `Seed()`

## Pattern Decision Tree

```
Need to create test data?
├─ ONE entity with encryption? → Factory
├─ MANY entities as cohesive operation? → Recipe
├─ Complete test scenario with ID mangling? → Scene
├─ READ existing seeded data? → Query
└─ Data transformation SDK ↔ Server? → Model
```

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

### Key Hierarchy

Bitwarden uses a two-level encryption hierarchy:

1. **User/Organization Key** - Encrypts the cipher's individual key
2. **Cipher Key** (optional) - Encrypts the actual cipher data

For seeding, we use the organization's symmetric key directly (no per-cipher key).

## Rust SDK FFI

### Error Handling

SDK functions return JSON with an `"error"` field on failure:

```json
{ "error": "Failed to parse CipherView JSON" }
```

Always check for `"error"` in the response before parsing.

## Testing

Integration tests in `test/SeederApi.IntegrationTest` verify:

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
