//! Field-level encryption functions for the Seeder.
//!
//! This module provides FFI functions for encrypting and decrypting individual string
//! values and JSON fields using AES-256-CBC-HMAC-SHA256 via bitwarden_crypto.
//! No dependency on bitwarden_vault types — the caller drives which fields to encrypt.

use std::ffi::{c_char, CStr, CString};

use base64::{engine::general_purpose::STANDARD, Engine};

use bitwarden_crypto::{
    BitwardenLegacyKeyBytes, EncString, KeyDecryptable, KeyEncryptable, SymmetricCryptoKey,
    SymmetricKeyAlgorithm,
};

use crate::crypto_util::{error_response, parse_key, wrap_key};

/// Encrypt a plaintext string with a symmetric key, returning an EncString.
///
/// # Arguments
/// * `plaintext` - The plaintext string to encrypt
/// * `symmetric_key_b64` - Base64-encoded symmetric key (64 bytes for AES-256-CBC-HMAC-SHA256)
///
/// # Returns
/// EncString in format "2.{iv}|{data}|{mac}"
///
/// # Safety
/// Both pointers must be valid null-terminated strings.
#[no_mangle]
pub unsafe extern "C" fn encrypt_string(
    plaintext: *const c_char,
    symmetric_key_b64: *const c_char,
) -> *const c_char {
    let Ok(plaintext) = CStr::from_ptr(plaintext).to_str() else {
        return error_response("Invalid UTF-8 in plaintext");
    };

    let Ok(key_b64) = CStr::from_ptr(symmetric_key_b64).to_str() else {
        return error_response("Invalid UTF-8 in symmetric_key_b64");
    };

    let Ok(key_bytes) = STANDARD.decode(key_b64) else {
        return error_response("Failed to decode base64 key");
    };

    let Ok(key) =
        SymmetricCryptoKey::try_from(&BitwardenLegacyKeyBytes::from(key_bytes.as_slice()))
    else {
        return error_response("Failed to create symmetric key: invalid key format or length");
    };

    let Ok(encrypted) = plaintext.to_string().encrypt_with_key(&key) else {
        return error_response("Failed to encrypt string");
    };

    CString::new(encrypted.to_string()).unwrap().into_raw()
}

/// Decrypt an EncString with a symmetric key, returning the plaintext.
///
/// # Arguments
/// * `enc_string` - EncString in format "2.{iv}|{data}|{mac}"
/// * `symmetric_key_b64` - Base64-encoded symmetric key (64 bytes for AES-256-CBC-HMAC-SHA256)
///
/// # Returns
/// The decrypted plaintext string
///
/// # Safety
/// Both pointers must be valid null-terminated strings.
#[no_mangle]
pub unsafe extern "C" fn decrypt_string(
    enc_string: *const c_char,
    symmetric_key_b64: *const c_char,
) -> *const c_char {
    let Ok(enc_str) = CStr::from_ptr(enc_string).to_str() else {
        return error_response("Invalid UTF-8 in enc_string");
    };

    let Ok(key_b64) = CStr::from_ptr(symmetric_key_b64).to_str() else {
        return error_response("Invalid UTF-8 in symmetric_key_b64");
    };

    let Ok(parsed): Result<EncString, _> = enc_str.parse() else {
        return error_response("Failed to parse EncString");
    };

    let Ok(key_bytes) = STANDARD.decode(key_b64) else {
        return error_response("Failed to decode base64 key");
    };

    let Ok(key) =
        SymmetricCryptoKey::try_from(&BitwardenLegacyKeyBytes::from(key_bytes.as_slice()))
    else {
        return error_response("Failed to create symmetric key: invalid key format or length");
    };

    let Ok(plaintext): Result<String, _> = parsed.decrypt_with_key(&key) else {
        return error_response("Failed to decrypt string");
    };

    CString::new(plaintext).unwrap().into_raw()
}

/// Wrap a symmetric key with another symmetric key, returning the wrapped key as an EncString.
///
/// Unlike [`encrypt_string`], this encrypts the *raw key bytes* of `key_to_wrap_b64` (not its base64
/// text), so the result unwraps back into a `SymmetricCryptoKey`. Use this for keys a client will later
/// unwrap via `unwrap_symmetric_key` — e.g. a `ProviderOrganization.Key`, which is an organization's
/// symmetric key wrapped with the provider's symmetric key.
///
/// # Arguments
/// * `key_to_wrap_b64` - Base64-encoded symmetric key to wrap (e.g. the organization key)
/// * `wrapping_key_b64` - Base64-encoded symmetric key to wrap it with (e.g. the provider key)
///
/// # Returns
/// EncString in format "2.{iv}|{data}|{mac}" whose decrypted plaintext is the encoded key bytes
///
/// # Safety
/// Both pointers must be valid null-terminated strings.
#[no_mangle]
pub unsafe extern "C" fn wrap_symmetric_key(
    key_to_wrap_b64: *const c_char,
    wrapping_key_b64: *const c_char,
) -> *const c_char {
    let Ok(key_to_wrap_b64) = CStr::from_ptr(key_to_wrap_b64).to_str() else {
        return error_response("Invalid UTF-8 in key_to_wrap_b64");
    };

    let Ok(wrapping_key_b64) = CStr::from_ptr(wrapping_key_b64).to_str() else {
        return error_response("Invalid UTF-8 in wrapping_key_b64");
    };

    let key_to_wrap = match parse_key(key_to_wrap_b64) {
        Ok(key) => key,
        Err(msg) => return error_response(&msg),
    };

    let wrapping_key = match parse_key(wrapping_key_b64) {
        Ok(key) => key,
        Err(msg) => return error_response(&msg),
    };

    match wrap_key(&key_to_wrap, &wrapping_key) {
        Ok(wrapped) => CString::new(wrapped).unwrap().into_raw(),
        Err(msg) => error_response(&msg),
    }
}

/// Encrypt specified fields in a JSON object, returning the modified JSON.
///
/// Takes a JSON object, a JSON array of dot-notation field paths (with `[*]` for
/// array elements), and a symmetric key. Walks the JSON tree and encrypts string
/// values at matching paths. Non-string values and unmatched paths are left unchanged.
///
/// # Arguments
/// * `json` - JSON object string
/// * `field_paths_json` - JSON array of path strings, e.g. `["name","login.username","login.uris[*].uri"]`
/// * `symmetric_key_b64` - Base64-encoded symmetric key
///
/// # Returns
/// Modified JSON with matching string fields encrypted as EncStrings
///
/// # Safety
/// All pointers must be valid null-terminated strings.
#[no_mangle]
pub unsafe extern "C" fn encrypt_fields(
    json: *const c_char,
    field_paths_json: *const c_char,
    symmetric_key_b64: *const c_char,
) -> *const c_char {
    let Ok(json_str) = CStr::from_ptr(json).to_str() else {
        return error_response("Invalid UTF-8 in json");
    };

    let Ok(paths_str) = CStr::from_ptr(field_paths_json).to_str() else {
        return error_response("Invalid UTF-8 in field_paths_json");
    };

    let Ok(key_b64) = CStr::from_ptr(symmetric_key_b64).to_str() else {
        return error_response("Invalid UTF-8 in symmetric_key_b64");
    };

    let Ok(mut value): Result<serde_json::Value, _> = serde_json::from_str(json_str) else {
        return error_response("Failed to parse JSON");
    };

    let Ok(paths): Result<Vec<String>, _> = serde_json::from_str(paths_str) else {
        return error_response("Failed to parse field paths JSON");
    };

    let Ok(key_bytes) = STANDARD.decode(key_b64) else {
        return error_response("Failed to decode base64 key");
    };

    let Ok(key) =
        SymmetricCryptoKey::try_from(&BitwardenLegacyKeyBytes::from(key_bytes.as_slice()))
    else {
        return error_response("Failed to create symmetric key: invalid key format or length");
    };

    for path in &paths {
        if let Err(msg) = encrypt_at_path(&mut value, path, &key) {
            return error_response(&msg);
        }
    }

    match serde_json::to_string(&value) {
        Ok(result) => CString::new(result).unwrap().into_raw(),
        Err(_) => error_response("Failed to serialize result JSON"),
    }
}

/// Walks a JSON value tree and encrypts string values at the given dot-path.
/// Supports `[*]` segments for iterating array elements.
fn encrypt_at_path(
    value: &mut serde_json::Value,
    path: &str,
    key: &SymmetricCryptoKey,
) -> Result<(), String> {
    let segments: Vec<&str> = path.split('.').collect();
    encrypt_segments(value, &segments, key)
}

fn encrypt_segments(
    value: &mut serde_json::Value,
    segments: &[&str],
    key: &SymmetricCryptoKey,
) -> Result<(), String> {
    if segments.is_empty() {
        return Ok(());
    }

    let segment = segments[0];
    let rest = &segments[1..];

    // Handle array wildcard: "uris[*]" means iterate all elements of the "uris" array
    if let Some(field_name) = segment.strip_suffix("[*]") {
        let Some(arr) = value.get_mut(field_name).and_then(|v| v.as_array_mut()) else {
            return Ok(()); // Field missing or not an array — skip
        };

        for element in arr.iter_mut() {
            encrypt_segments(element, rest, key)?;
        }

        return Ok(());
    }

    // Last segment — encrypt the value if it's a string
    if rest.is_empty() {
        if let Some(s) = value.get(segment).and_then(|v| v.as_str()) {
            let encrypted = s
                .to_string()
                .encrypt_with_key(key)
                .map_err(|_| format!("Failed to encrypt field '{segment}'"))?;
            value[segment] = serde_json::Value::String(encrypted.to_string());
        }
        // null or missing — leave unchanged
        return Ok(());
    }

    // Intermediate segment — recurse into nested object
    let Some(nested) = value.get_mut(segment) else {
        return Ok(()); // Field missing — skip
    };

    encrypt_segments(nested, rest, key)
}

/// Encrypt specified JSON fields under a freshly generated per-cipher key, and return the modified
/// JSON with the cipher key wrapped by the vault key injected as the top-level `key` field.
///
/// This reproduces a "cipher key" cipher (the fields are encrypted with the cipher key rather than
/// the vault key directly). The returned JSON deserializes into the same shape as `encrypt_fields`,
/// plus a populated `key`.
///
/// # Arguments
/// * `json` - JSON object string (the cipher view)
/// * `field_paths_json` - JSON array of dot-notation field paths
/// * `symmetric_key_b64` - Base64-encoded vault key that wraps the generated cipher key
///
/// # Safety
/// All pointers must be valid null-terminated strings.
#[no_mangle]
pub unsafe extern "C" fn encrypt_fields_with_cipher_key(
    json: *const c_char,
    field_paths_json: *const c_char,
    symmetric_key_b64: *const c_char,
) -> *const c_char {
    let Ok(json_str) = CStr::from_ptr(json).to_str() else {
        return error_response("Invalid UTF-8 in json");
    };
    let Ok(paths_str) = CStr::from_ptr(field_paths_json).to_str() else {
        return error_response("Invalid UTF-8 in field_paths_json");
    };
    let Ok(vault_key_b64) = CStr::from_ptr(symmetric_key_b64).to_str() else {
        return error_response("Invalid UTF-8 in symmetric_key_b64");
    };

    match encrypt_fields_with_cipher_key_internal(json_str, paths_str, vault_key_b64) {
        Ok(json) => CString::new(json).unwrap().into_raw(),
        Err(msg) => error_response(&msg),
    }
}

fn encrypt_fields_with_cipher_key_internal(
    json_str: &str,
    paths_str: &str,
    vault_key_b64: &str,
) -> Result<String, String> {
    let mut value: serde_json::Value =
        serde_json::from_str(json_str).map_err(|_| "Failed to parse JSON".to_string())?;
    let paths: Vec<String> = serde_json::from_str(paths_str)
        .map_err(|_| "Failed to parse field paths JSON".to_string())?;
    let vault_key = parse_key(vault_key_b64)?;

    // Generate a per-cipher key and encrypt the fields with it (not the vault key directly).
    let cipher_key = SymmetricCryptoKey::make(SymmetricKeyAlgorithm::Aes256CbcHmac);
    for path in &paths {
        encrypt_at_path(&mut value, path, &cipher_key)?;
    }

    // The cipher key is wrapped by the vault key and stored on the cipher as `key`.
    value["key"] = serde_json::Value::String(wrap_key(&cipher_key, &vault_key)?);

    serde_json::to_string(&value).map_err(|_| "Failed to serialize result JSON".to_string())
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::crypto_util::unwrap_key;
    use crate::free_c_string;

    fn make_test_key() -> SymmetricCryptoKey {
        SymmetricCryptoKey::make(SymmetricKeyAlgorithm::Aes256CbcHmac)
    }

    fn call_ffi_string(
        func: unsafe extern "C" fn(*const c_char, *const c_char) -> *const c_char,
        a: &str,
        b: &str,
    ) -> String {
        let a_cstr = CString::new(a).unwrap();
        let b_cstr = CString::new(b).unwrap();
        let ptr = unsafe { func(a_cstr.as_ptr(), b_cstr.as_ptr()) };
        let result = unsafe { CStr::from_ptr(ptr) }.to_str().unwrap().to_owned();
        unsafe { free_c_string(ptr as *mut c_char) };
        result
    }

    #[test]
    fn encrypt_string_decrypt_string_roundtrip() {
        let key = make_test_key();
        let key_b64: String = key.to_base64().into();

        let encrypted = call_ffi_string(encrypt_string, "hello world", &key_b64);
        assert!(
            encrypted.starts_with("2."),
            "Expected EncString, got: {encrypted}"
        );

        let decrypted = call_ffi_string(decrypt_string, &encrypted, &key_b64);
        assert_eq!(decrypted, "hello world");
    }

    #[test]
    fn wrap_symmetric_key_unwraps_back_to_original_key() {
        // Mirrors a ProviderOrganization.Key: an organization key wrapped with the provider key.
        let organization_key = make_test_key();
        let provider_key = make_test_key();
        let organization_key_b64: String = organization_key.to_base64().into();
        let provider_key_b64: String = provider_key.to_base64().into();

        let wrapped = call_ffi_string(wrap_symmetric_key, &organization_key_b64, &provider_key_b64);
        assert!(
            wrapped.starts_with("2."),
            "Expected a type-2 EncString, got: {wrapped}"
        );

        // The client unwraps this via unwrap_symmetric_key using the provider key; the recovered
        // key must be byte-identical to the original organization key (a base64-text encryption
        // would fail this).
        let unwrapped = unwrap_key(&wrapped, &provider_key).unwrap();
        assert_eq!(
            <String>::from(unwrapped.to_base64()),
            organization_key_b64,
            "unwrapped key must equal the original organization key"
        );
    }

    #[test]
    fn encrypt_at_path_encrypts_top_level_string() {
        let key = make_test_key();
        let mut value: serde_json::Value = serde_json::json!({"name": "Test", "type": 1});

        encrypt_at_path(&mut value, "name", &key).unwrap();

        let name = value["name"].as_str().unwrap();
        assert!(name.starts_with("2."), "Expected encrypted, got: {name}");
        assert_ne!(name, "Test");
    }

    #[test]
    fn encrypt_at_path_encrypts_nested_field() {
        let key = make_test_key();
        let mut value: serde_json::Value = serde_json::json!({
            "login": {"username": "user@test.com", "password": "secret"}
        });

        encrypt_at_path(&mut value, "login.username", &key).unwrap();

        let username = value["login"]["username"].as_str().unwrap();
        assert!(
            username.starts_with("2."),
            "Expected encrypted, got: {username}"
        );

        // password should be unchanged
        assert_eq!(value["login"]["password"].as_str().unwrap(), "secret");
    }

    #[test]
    fn encrypt_at_path_encrypts_array_wildcard() {
        let key = make_test_key();
        let mut value: serde_json::Value = serde_json::json!({
            "login": {
                "uris": [
                    {"uri": "https://example.com", "match": 0},
                    {"uri": "https://test.com", "match": 1}
                ]
            }
        });

        encrypt_at_path(&mut value, "login.uris[*].uri", &key).unwrap();

        let uris = value["login"]["uris"].as_array().unwrap();
        for uri_obj in uris {
            let uri = uri_obj["uri"].as_str().unwrap();
            assert!(uri.starts_with("2."), "Expected encrypted URI, got: {uri}");
        }
        // match should be unchanged
        assert_eq!(uris[0]["match"].as_i64().unwrap(), 0);
    }

    #[test]
    fn encrypt_fields_ffi_encrypts_specified_paths() {
        let key = make_test_key();
        let key_b64: String = key.to_base64().into();

        let input_json = serde_json::json!({
            "name": "Test Login",
            "type": 1,
            "login": {"username": "user@test.com", "password": "secret"}
        })
        .to_string();

        let paths_json = r#"["name","login.username","login.password"]"#;

        let json_cstr = CString::new(input_json).unwrap();
        let paths_cstr = CString::new(paths_json).unwrap();
        let key_cstr = CString::new(key_b64.as_str()).unwrap();

        let ptr =
            unsafe { encrypt_fields(json_cstr.as_ptr(), paths_cstr.as_ptr(), key_cstr.as_ptr()) };
        let result = unsafe { CStr::from_ptr(ptr) }.to_str().unwrap().to_owned();
        unsafe { free_c_string(ptr as *mut c_char) };

        assert!(!result.contains("\"error\""), "Got error: {result}");

        let parsed: serde_json::Value = serde_json::from_str(&result).unwrap();
        let name = parsed["name"].as_str().unwrap();
        assert!(
            name.starts_with("2."),
            "name should be encrypted, got: {name}"
        );

        let username = parsed["login"]["username"].as_str().unwrap();
        assert!(
            username.starts_with("2."),
            "username should be encrypted, got: {username}"
        );

        // type should be unchanged
        assert_eq!(parsed["type"].as_i64().unwrap(), 1);
    }

    #[test]
    fn decrypt_string_with_wrong_key_fails() {
        let key1 = make_test_key();
        let key2 = make_test_key();
        let key1_b64: String = key1.to_base64().into();
        let key2_b64: String = key2.to_base64().into();

        let encrypted = call_ffi_string(encrypt_string, "secret", &key1_b64);
        let result = call_ffi_string(decrypt_string, &encrypted, &key2_b64);

        assert!(
            result.contains("\"error\""),
            "Should fail with wrong key, got: {result}"
        );
    }

    #[test]
    fn encrypt_at_path_skips_null_values() {
        let key = make_test_key();
        let mut value: serde_json::Value = serde_json::json!({"name": null, "type": 1});

        encrypt_at_path(&mut value, "name", &key).unwrap();

        assert!(value["name"].is_null(), "Null should remain null");
    }

    #[test]
    fn encrypt_at_path_skips_missing_fields() {
        let key = make_test_key();
        let mut value: serde_json::Value = serde_json::json!({"type": 1});

        // Should not error on missing "name"
        encrypt_at_path(&mut value, "name", &key).unwrap();
        encrypt_at_path(&mut value, "login.username", &key).unwrap();
    }

    #[test]
    fn encrypt_fields_with_cipher_key_roundtrip() {
        let vault = make_test_key();
        let vault_b64: String = vault.to_base64().into();

        let input = serde_json::json!({
            "name": "Cipher-Key Login",
            "type": 1,
            "login": {"username": "u@test.com", "password": "pw"}
        })
        .to_string();
        let paths = r#"["name","login.username","login.password"]"#;

        let out = encrypt_fields_with_cipher_key_internal(&input, paths, &vault_b64).unwrap();
        let parsed: serde_json::Value = serde_json::from_str(&out).unwrap();

        // A wrapped cipher key is injected.
        let wrapped_ck = parsed["key"].as_str().unwrap();
        assert!(wrapped_ck.starts_with("2."));
        let cipher_key = unwrap_key(wrapped_ck, &vault).unwrap();

        // Fields decrypt with the cipher key, and NOT with the vault key directly.
        let name: EncString = parsed["name"].as_str().unwrap().parse().unwrap();
        let name_dec: String = name.decrypt_with_key(&cipher_key).unwrap();
        assert_eq!(name_dec, "Cipher-Key Login");

        let name_again: EncString = parsed["name"].as_str().unwrap().parse().unwrap();
        let via_vault: Result<String, _> = name_again.decrypt_with_key(&vault);
        assert!(
            via_vault.is_err(),
            "fields must not decrypt with the vault key in cipher-key mode"
        );

        // Non-encrypted fields are left intact.
        assert_eq!(parsed["type"].as_i64().unwrap(), 1);
    }
}
