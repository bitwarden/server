# RustSdk API Surface Inventory

> **Auto-generated from actual source files.** Last updated: 2026-02-25
> Pinned rev: `abba7fdab687753268b63248ec22639dff35d07c`

This documents every type, trait, and function the server's RustSdk imports from the three
sdk-internal crates. Use this to assess breaking change impact when bumping revs.

**Location:** `util/RustSdk/rust/src/`

## bitwarden-crypto (primary dependency)

### Types Used

| Type                      | File              | Usage                                                                                        |
| ------------------------- | ----------------- | -------------------------------------------------------------------------------------------- |
| `BitwardenLegacyKeyBytes` | lib.rs, cipher.rs | `BitwardenLegacyKeyBytes::from()` — wraps raw key bytes for `SymmetricCryptoKey::try_from()` |
| `HashPurpose`             | lib.rs            | `HashPurpose::ServerAuthorization` enum variant                                              |
| `Kdf`                     | lib.rs            | `Kdf::PBKDF2 { iterations }` enum variant with `NonZeroU32`                                  |
| `KeyStore`                | cipher.rs         | `KeyStore::<KeyIds>::default()`, `.context_mut()`, `.add_local_symmetric_key()`              |
| `MasterKey`               | lib.rs            | `MasterKey::derive()`, `.derive_master_key_hash()`, `.make_user_key()`                       |
| `PrivateKey`              | lib.rs            | `PrivateKey::from_pem()`, `.to_public_key()`, `.to_der()`                                    |
| `PublicKey`               | lib.rs            | `PublicKey::from_der()`                                                                      |
| `RsaKeyPair`              | lib.rs            | Struct literal: `RsaKeyPair { private, public }`                                             |
| `SpkiPublicKeyBytes`      | lib.rs            | `SpkiPublicKeyBytes::from()` — wraps public key DER bytes                                    |
| `SymmetricCryptoKey`      | lib.rs, cipher.rs | `.make_aes256_cbc_hmac_key()`, `::try_from()`, `.to_base64()`                                |
| `UnsignedSharedKey`       | lib.rs            | `::encapsulate_key_unsigned()` (deprecated — wrapped with `#[allow(deprecated)]`)            |
| `UserKey`                 | lib.rs            | `UserKey::new()`, `.make_key_pair()`, `.0` field access                                      |

### Traits Used

| Trait                  | File              | Methods Called                                                             |
| ---------------------- | ----------------- | -------------------------------------------------------------------------- |
| `KeyEncryptable`       | lib.rs, cipher.rs | `.encrypt_with_key(&key)` — encrypts DER bytes and strings                 |
| `CompositeEncryptable` | cipher.rs         | `.encrypt_composite(&mut ctx, key_id)` — encrypts `CipherView` -> `Cipher` |
| `Decryptable`          | cipher.rs         | `.decrypt(&mut ctx, key_id)` — decrypts `Cipher` -> `CipherView`           |

## bitwarden-core (minimal dependency)

| Type                     | File      | Usage                                        |
| ------------------------ | --------- | -------------------------------------------- |
| `key_management::KeyIds` | cipher.rs | Generic type parameter: `KeyStore::<KeyIds>` |

## bitwarden-vault (data model dependency)

### Production Code

| Type         | File      | Usage                                                 |
| ------------ | --------- | ----------------------------------------------------- |
| `Cipher`     | cipher.rs | Protected Data container (encrypted), deserialized from JSON via serde |
| `CipherView` | cipher.rs | Vault Data in Use (decrypted view), serialized to/from JSON via serde  |

### Test-Only Types

| Type                 | File            | Usage                                                                                                                                      |
| -------------------- | --------------- | ------------------------------------------------------------------------------------------------------------------------------------------ |
| `CipherRepromptType` | cipher.rs tests | `CipherRepromptType::None` enum variant                                                                                                    |
| `CipherType`         | cipher.rs tests | `CipherType::Login` enum variant                                                                                                           |
| `LoginView`          | cipher.rs tests | Struct literal with fields: `username`, `password`, `password_revision_date`, `uris`, `totp`, `autofill_on_page_load`, `fido2_credentials` |

### CipherView Fields (used in test struct literal)

The test helper `create_test_cipher_view()` constructs a `CipherView` with these fields:

`id`, `organization_id`, `folder_id`, `collection_ids`, `key`, `name`, `notes`, `type`,
`login`, `identity`, `card`, `secure_note`, `ssh_key`, `favorite`, `reprompt`,
`organization_use_totp`, `edit`, `permissions`, `view_password`, `local_data`, `attachments`,
`attachment_decryption_failures`, `fields`, `password_history`, `creation_date`, `deleted_date`,
`revision_date`, `archived_date`

Any new required field added to `CipherView` upstream will break this struct literal.

## Breaking Change Risk Matrix

When reviewing upstream commits, prioritize checking for changes to:

**Critical (compilation failure):**

- Any type rename or removal listed above
- New required fields on `CipherView`, `Cipher`, or `LoginView`
- Changes to `KeyStore` generic parameters or `context_mut()` method
- Changes to encryption/decryption trait method signatures

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
- New optional fields on structs (serde defaults to `None` for `Option<T>`)
- New methods added to existing types (additive, non-breaking)

## How to Check for Changes

For each crate, check the public API exports:

```bash
cd /path/to/sdk-internal
# bitwarden-crypto public API
git diff <old>..<new> -- crates/bitwarden-crypto/src/lib.rs crates/bitwarden-crypto/src/keys/mod.rs

# bitwarden-vault Cipher/CipherView changes
git diff <old>..<new> -- crates/bitwarden-vault/src/cipher/cipher.rs crates/bitwarden-vault/src/cipher/login.rs

# bitwarden-core KeyIds
git diff <old>..<new> -- crates/bitwarden-core/src/key_management/mod.rs
```
