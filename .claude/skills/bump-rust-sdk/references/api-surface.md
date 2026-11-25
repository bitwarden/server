# RustSdk API Surface Inventory

> **Auto-generated from actual source files.** Last updated: 2026-11-25
> Pinned rev: `5b7fbb413cf0eff108b0abdbc22607945cc36d8b` (sdk-internal `0.2.0-main.1214`, clients `web-v2026.11.0`)

This documents every type, trait, and function the server's RustSdk imports from
`bitwarden-crypto` and `bitwarden-organization-crypto`. Use this to assess breaking change impact
when bumping revs. Pin both crates to the same rev, or Cargo resolves two copies of `bitwarden-crypto`.

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

### Types and macros Used — organization_invite.rs (invite links)

| Item            | Usage                                                                             |
| --------------- | --------------------------------------------------------------------------------- |
| `key_slot_ids!` | Declares local-only `InviteIds` key slots for a throwaway `KeyStore`              |
| `KeyStore`      | `KeyStore::<InviteIds>::default()`, `.context()`, `ctx.add_local_symmetric_key()` |
| `EncString`     | Parses the organization's wrapped private key                                     |

### Traits Used

| Trait            | File      | Methods Called                                                       |
| ---------------- | --------- | -------------------------------------------------------------------- |
| `KeyEncryptable` | lib.rs    | `.encrypt_with_key(&key)` — encrypts DER bytes and strings           |
| `KeyEncryptable` | cipher.rs | `.encrypt_with_key(&key)` — encrypts plaintext strings to EncStrings |
| `KeyDecryptable` | cipher.rs | `.decrypt_with_key(&key)` — decrypts EncString back to plaintext     |

## bitwarden-organization-crypto

| Item                   | File                   | Usage                                                                                |
| ---------------------- | ---------------------- | ------------------------------------------------------------------------------------ |
| `invite::Invite`       | organization_invite.rs | `Invite::make_for_private_key()`, `.disable_confirmation()`, `String::from(&invite)` |
| `invite::InviteSecret` | organization_invite.rs | `String::from(&secret)` (base64url); tests also parse it and unseal the invite       |

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
| `make_organization_invite`       | organization_invite.rs | Make a sealed org invite + invite secret for an invite link |
| `free_c_string`                  | lib.rs    | Free a C string returned by any of the above functions    |

## Breaking Change Risk Matrix

When reviewing upstream commits, prioritize checking for changes to:

**Critical (compilation failure):**

- Any type rename or removal listed above
- Changes to `EncString` parsing or serialization format
- Changes to `KeyEncryptable` or `KeyDecryptable` trait method signatures
- Changes to `SymmetricCryptoKey::try_from()` or `BitwardenLegacyKeyBytes`
- Changes to `key_slot_ids!`, `KeyStore`/`KeyStoreContext`, or `Invite::make_for_private_key()`

**High (runtime failure):**

- Changes to `Kdf::PBKDF2` enum variant
- Changes to `HashPurpose::ServerAuthorization`
- Changes to `MasterKey::derive()` or key derivation behavior
- Changes to `UnsignedSharedKey::encapsulate_key_unsigned()` signature
- Changes to the `Invite` wire format or `InviteSecret` string encoding (seeded invite links stop opening)

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
# invite crypto
git diff <old>..<new> -- crates/bitwarden-organization-crypto/src/invite.rs
```
