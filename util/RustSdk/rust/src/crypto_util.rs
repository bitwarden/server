//! Shared crypto plumbing for the Seeder FFI shim: key parsing, key wrapping/unwrapping, and the
//! error-response helper. Used by both `cipher` and `attachment`.

use std::ffi::{c_char, CString};

use base64::{engine::general_purpose::STANDARD, Engine};

use bitwarden_crypto::{
    BitwardenLegacyKeyBytes, EncString, KeyDecryptable, KeyEncryptable, SymmetricCryptoKey,
};

/// Create an error JSON response and return it as a C string pointer.
pub(crate) fn error_response(message: &str) -> *const c_char {
    let error_json = serde_json::json!({ "error": message }).to_string();
    CString::new(error_json).unwrap().into_raw()
}

/// Decode a base64 symmetric key into a [SymmetricCryptoKey].
pub(crate) fn parse_key(key_b64: &str) -> Result<SymmetricCryptoKey, String> {
    let key_bytes = STANDARD
        .decode(key_b64)
        .map_err(|_| "Failed to decode base64 key".to_string())?;
    SymmetricCryptoKey::try_from(&BitwardenLegacyKeyBytes::from(key_bytes.as_slice()))
        .map_err(|_| "Failed to create symmetric key: invalid key format or length".to_string())
}

/// Wrap a symmetric key with another symmetric key, returning the wrapped key as an EncString.
pub(crate) fn wrap_key(
    key_to_wrap: &SymmetricCryptoKey,
    wrapping_key: &SymmetricCryptoKey,
) -> Result<String, String> {
    let wrapped = key_to_wrap
        .to_encoded()
        .encrypt_with_key(wrapping_key)
        .map_err(|_| "Failed to wrap key".to_string())?;
    Ok(wrapped.to_string())
}

/// Unwrap a wrapped-key EncString with the wrapping key, returning the recovered [SymmetricCryptoKey].
pub(crate) fn unwrap_key(
    wrapped: &str,
    wrapping_key: &SymmetricCryptoKey,
) -> Result<SymmetricCryptoKey, String> {
    let parsed: EncString = wrapped
        .parse()
        .map_err(|_| "Failed to parse wrapped key EncString".to_string())?;
    let bytes: Vec<u8> = parsed
        .decrypt_with_key(wrapping_key)
        .map_err(|_| "Failed to unwrap key".to_string())?;
    SymmetricCryptoKey::try_from(&BitwardenLegacyKeyBytes::from(bytes.as_slice()))
        .map_err(|_| "Failed to reconstruct unwrapped key".to_string())
}
