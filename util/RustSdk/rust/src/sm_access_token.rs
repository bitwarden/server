//! Secrets Manager access-token key derivation for the Seeder.
//!
//! Reproduces the SDK's access-token key derivation so seeded tokens decrypt against the same
//! key the client would derive.

use std::ffi::{c_char, CStr, CString};

use base64::{engine::general_purpose::STANDARD, Engine};

use bitwarden_crypto::{derive_shareable_key, SymmetricCryptoKey};
use zeroize::Zeroizing;

use crate::crypto_util::error_response;

/// Derive the Secrets Manager access-token key from the 16-byte token encryption key.
///
/// Reproduces `bitwarden_crypto::derive_shareable_key(secret, "accesstoken", Some("sm-access-token"))`:
/// HMAC-SHA256 (mac key "bitwarden-accesstoken") over the secret, then HKDF-Expand SHA256 to a
/// 64-byte enc||mac key.
///
/// # Arguments
/// * `encryption_key_b64` - Base64-encoded 16-byte access-token encryption key
///
/// # Returns
/// Base64-encoded 64-byte AES-256-CBC-HMAC-SHA256 key
///
/// # Safety
/// The pointer must be a valid null-terminated string.
#[no_mangle]
pub unsafe extern "C" fn derive_access_token_key(
    encryption_key_b64: *const c_char,
) -> *const c_char {
    let Ok(key_b64) = CStr::from_ptr(encryption_key_b64).to_str() else {
        return error_response("Invalid UTF-8 in encryption_key_b64");
    };

    let Ok(key_bytes) = STANDARD.decode(key_b64) else {
        return error_response("Failed to decode base64 key");
    };

    let Ok(secret) = <[u8; 16]>::try_from(key_bytes.as_slice()) else {
        return error_response("Access-token encryption key must be 16 bytes");
    };

    let derived = derive_shareable_key(Zeroizing::new(secret), "accesstoken", Some("sm-access-token"));

    CString::new(
        SymmetricCryptoKey::Aes256CbcHmacKey(derived)
            .to_base64()
            .to_string(),
    )
    .unwrap()
    .into_raw()
}
