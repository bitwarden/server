# RustSdk API Surface Inventory

> **Auto-generated from actual source files.** Last updated: 2026-02-25
> Pinned rev: `abba7fdab687753268b63248ec22639dff35d07c`

This documents every type, trait, and function the server's RustSdk imports from
`bitwarden-crypto`. Use this to assess breaking change impact when bumping revs.

**Location:** `util/RustSdk/rust/src/`

## bitwarden-crypto

### Types Used — lib.rs (key generation and management)

| Type                      | Usage                                                                                        |
| ------------------------- | -------------------------------------------------------------------------------------------- |
| `BitwardenLegacyKeyBytes` | `BitwardenLegacyKeyBytes::from()` — wraps raw key bytes for `SymmetricCryptoKey::try_from()` |
| `HashPurpose`             | `HashPurpose::ServerAuthorization` enum variant                                              |
| `Kdf`                     | `Kdf::PBKDF2 { iterations }` enum variant with `NonZeroU32`                                  |
| `MasterKey`               | `MasterKey::derive()`, `.derive_master_key_hash()`, `.make_user_key()`                       |
| `PrivateKey`              | `PrivateKey::from_pem()`, `.to_public_key()`, `.to_der()`                                    |
| `PublicKey`               | `PublicKey::from_der()`                                                                      |
| `RsaKeyPair`              | Struct literal: `RsaKeyPair { private, public }`                                             |
| `SpkiPublicKeyBytes`      | `SpkiPublicKeyBytes::from()` — wraps public key DER bytes                                    |
| `SymmetricCryptoKey`      | `.make_aes256_cbc_hmac_key()`, `::try_from()`, `.to_base64()`                                |
| `UnsignedSharedKey`       | `::encapsulate_key_unsigned()` (deprecated — wrapped with `#[allow(deprecated)]`)            |
| `UserKey`                 | `UserKey::new()`, `.make_key_pair()`, `.0` field access                                      |

### Types Used — cipher.rs (field-level encryption)

| Type                      | Usage                                                                                           |
| ------------------------- | ----------------------------------------------------------------------------------------------- |
| `BitwardenLegacyKeyBytes` | `BitwardenLegacyKeyBytes::from()` — wraps raw key bytes for `SymmetricCryptoKey::try_from()`    |
| `EncString`               | `enc_str.parse::<EncString>()`, `.to_string()` — parsed from and serialized to EncString format |
| `SymmetricCryptoKey`      | `::try_from()`, `.make_aes256_cbc_hmac_key()`, `.to_base64()` — key construction and testing    |

### Traits Used

| Trait            | File      | Methods Called                                                       |
| ---------------- | --------- | -------------------------------------------------------------------- |
| `KeyEncryptable` | lib.rs    | `.encrypt_with_key(&key)` — encrypts DER bytes and strings           |
| `KeyEncryptable` | cipher.rs | `.encrypt_with_key(&key)` — encrypts plaintext strings to EncStrings |
| `KeyDecryptable` | cipher.rs | `.decrypt_with_key(&key)` — decrypts EncString back to plaintext     |

## FFI Functions Exposed

The Rust layer exposes these functions to C# via csbindgen:

| Function                         | File      | Purpose                                                   |
| -------------------------------- | --------- | --------------------------------------------------------- |
| `generate_user_keys`             | lib.rs    | Derive master key, user key, key pair from email/password |
| `generate_organization_keys`     | lib.rs    | Generate org symmetric key + RSA key pair                 |
| `generate_user_organization_key` | lib.rs    | Encapsulate org key with user's public key (unsigned)     |
| `encrypt_string`                 | cipher.rs | Encrypt a single plaintext string with a symmetric key    |
| `decrypt_string`                 | cipher.rs | Decrypt an EncString with a symmetric key                 |
| `encrypt_fields`                 | cipher.rs | Encrypt specified fields in a JSON object by dot-path     |
| `free_c_string`                  | lib.rs    | Free a C string returned by any of the above functions    |

## Breaking Change Risk Matrix

When reviewing upstream commits, prioritize checking for changes to:

**Critical (compilation failure):**

- Any type rename or removal listed above
- Changes to `EncString` parsing or serialization format
- Changes to `KeyEncryptable` or `KeyDecryptable` trait method signatures
- Changes to `SymmetricCryptoKey::try_from()` or `BitwardenLegacyKeyBytes`

**High (runtime failure):**

- Changes to `Kdf::PBKDF2` enum variant
- Changes to `HashPurpose::ServerAuthorization`
- Changes to `MasterKey::derive()` or key derivation behavior
- Changes to `UnsignedSharedKey::encapsulate_key_unsigned()` signature

**Medium (deprecation warnings):**

- Functions annotated with `#[deprecated]` — suppress with `#[allow(deprecated)]`
  and add a comment explaining why and what the migration path is

**Low (transparent):**

- Internal implementation changes that don't affect the public API
- New methods added to existing types (additive, non-breaking)

## How to Check for Changes

```bash
cd /path/to/sdk-internal
# bitwarden-crypto public API
git diff <old>..<new> -- crates/bitwarden-crypto/src/lib.rs crates/bitwarden-crypto/src/keys/mod.rs
```
