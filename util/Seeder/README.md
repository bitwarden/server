# Bitwarden Database Seeder

A class library for generating and inserting properly encrypted test data into Bitwarden databases.

## Domain Taxonomy

### Cipher Encryption States

| Term | Description | Stored in DB? |
|------|-------------|---------------|
| **CipherView** | Plaintext/decrypted form. Human-readable data. | Never |
| **Cipher** | Encrypted form. All sensitive fields are EncStrings. | Yes |

The "View" suffix always denotes plaintext. No suffix means encrypted.

### EncString Format

Encrypted strings follow this format:
```
2.{iv}|{ciphertext}|{mac}
```
- **2** = Algorithm type (AES-256-CBC-HMAC-SHA256)
- **iv** = Initialization vector (base64)
- **ciphertext** = Encrypted data (base64)
- **mac** = Message authentication code (base64)

### Data Structure Differences

**SDK Structure (nested):**
```json
{ "name": "2.x...", "login": { "username": "2.y...", "password": "2.z..." } }
```

**Server Structure (flat, stored in Cipher.Data):**
```json
{ "Name": "2.x...", "Username": "2.y...", "Password": "2.z..." }
```

The seeder transforms SDK output to server format before database insertion.

### Key Hierarchy

```
Organization Key (or User Key)
       │
       ├──▶ Encrypts Cipher.Key (optional per-cipher key)
       │
       └──▶ Encrypts cipher fields directly (if no per-cipher key)
```

For seeding, we encrypt directly with the organization key.

### Entity Relationships

```
Organization
    │
    ├── Collections ──┬── CollectionCipher ──┐
    │                 │                      │
    └── Ciphers ──────┴──────────────────────┘
```

Ciphers belong to organizations and are assigned to collections via the `CollectionCipher` join table.

## Project Structure

### Factories

Create individual domain entities with realistic encrypted data.

| Factory | Purpose |
|---------|---------|
| `CipherSeeder` | Creates encrypted Cipher entities via Rust SDK |
| `CollectionSeeder` | Creates collections with encrypted names |
| `OrganizationSeeder` | Creates organizations with keys |
| `UserSeeder` | Creates users with encrypted credentials |

### Recipes

Bulk data operations using BulkCopy for performance.

| Recipe | Purpose |
|--------|---------|
| `CiphersRecipe` | Bulk create ciphers and assign to collections |
| `CollectionsRecipe` | Create collections with user permissions |
| `GroupsRecipe` | Create groups with collection access |
| `OrganizationWithUsersRecipe` | Full org setup with users |

### Models

DTOs for SDK interop and data transformation.

| Model | Purpose |
|-------|---------|
| `CipherViewDto` | Plaintext input to SDK encryption |
| `EncryptedCipherDto` | Parses SDK's encrypted output |

## Rust SDK Integration

The seeder uses FFI calls to the Rust SDK for cryptographically correct encryption:

```
CipherViewDto → RustSdkService.EncryptCipher() → EncryptedCipherDto → Server Format
```

This ensures seeded data can be decrypted and displayed in the actual Bitwarden clients.
