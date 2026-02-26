//! Cipher encryption and decryption functions for the Seeder.
//!
//! This module provides FFI functions for encrypting and decrypting Bitwarden ciphers
//! using the Rust SDK's cryptographic primitives.

use std::ffi::{c_char, CStr, CString};

use base64::{engine::general_purpose::STANDARD, Engine};

use bitwarden_core::key_management::KeyIds;
use bitwarden_crypto::{
    BitwardenLegacyKeyBytes, CompositeEncryptable, Decryptable, KeyEncryptable, KeyStore,
    SymmetricCryptoKey,
};
use bitwarden_vault::{Cipher, CipherView};

/// Create an error JSON response and return it as a C string pointer.
fn error_response(message: &str) -> *const c_char {
    let error_json = serde_json::json!({ "error": message }).to_string();
    CString::new(error_json).unwrap().into_raw()
}

/// Encrypt a CipherView with a symmetric key, returning an encrypted Cipher as JSON.
///
/// # Arguments
/// * `cipher_view_json` - JSON string representing a CipherView (camelCase format)
/// * `symmetric_key_b64` - Base64-encoded symmetric key (64 bytes for AES-256-CBC-HMAC-SHA256)
///
/// # Returns
/// JSON string representing the encrypted Cipher
///
/// # Safety
/// Both pointers must be valid null-terminated strings.
#[no_mangle]
pub unsafe extern "C" fn encrypt_cipher(
    cipher_view_json: *const c_char,
    symmetric_key_b64: *const c_char,
) -> *const c_char {
    let Ok(cipher_view_json) = CStr::from_ptr(cipher_view_json).to_str() else {
        return error_response("Invalid UTF-8 in cipher_view_json");
    };

    let Ok(key_b64) = CStr::from_ptr(symmetric_key_b64).to_str() else {
        return error_response("Invalid UTF-8 in symmetric_key_b64");
    };

    let Ok(cipher_view): Result<CipherView, _> = serde_json::from_str(cipher_view_json) else {
        return error_response("Failed to parse CipherView JSON");
    };

    let Ok(key_bytes) = STANDARD.decode(key_b64) else {
        return error_response("Failed to decode base64 key");
    };

    let Ok(key) = SymmetricCryptoKey::try_from(&BitwardenLegacyKeyBytes::from(key_bytes.as_slice())) else {
        return error_response("Failed to create symmetric key: invalid key format or length");
    };

    let store: KeyStore<KeyIds> = KeyStore::default();
    let mut ctx = store.context_mut();
    let key_id = ctx.add_local_symmetric_key(key);

    let Ok(cipher) = cipher_view.encrypt_composite(&mut ctx, key_id) else {
        return error_response("Failed to encrypt cipher: encryption operation failed");
    };

    match serde_json::to_string(&cipher) {
        Ok(json) => CString::new(json).unwrap().into_raw(),
        Err(_) => error_response("Failed to serialize encrypted cipher"),
    }
}

/// Decrypt an encrypted Cipher with a symmetric key, returning a CipherView as JSON.
///
/// # Arguments
/// * `cipher_json` - JSON string representing an encrypted Cipher
/// * `symmetric_key_b64` - Base64-encoded symmetric key (64 bytes for AES-256-CBC-HMAC-SHA256)
///
/// # Returns
/// JSON string representing the decrypted CipherView
///
/// # Safety
/// Both pointers must be valid null-terminated strings.
#[no_mangle]
pub unsafe extern "C" fn decrypt_cipher(
    cipher_json: *const c_char,
    symmetric_key_b64: *const c_char,
) -> *const c_char {
    let Ok(cipher_json) = CStr::from_ptr(cipher_json).to_str() else {
        return error_response("Invalid UTF-8 in cipher_json");
    };

    let Ok(key_b64) = CStr::from_ptr(symmetric_key_b64).to_str() else {
        return error_response("Invalid UTF-8 in symmetric_key_b64");
    };

    let Ok(cipher): Result<Cipher, _> = serde_json::from_str(cipher_json) else {
        return error_response("Failed to parse Cipher JSON");
    };

    let Ok(key_bytes) = STANDARD.decode(key_b64) else {
        return error_response("Failed to decode base64 key");
    };

    let Ok(key) = SymmetricCryptoKey::try_from(&BitwardenLegacyKeyBytes::from(key_bytes.as_slice())) else {
        return error_response("Failed to create symmetric key: invalid key format or length");
    };

    let store: KeyStore<KeyIds> = KeyStore::default();
    let mut ctx = store.context_mut();
    let key_id = ctx.add_local_symmetric_key(key);

    let Ok(cipher_view): Result<CipherView, _> = cipher.decrypt(&mut ctx, key_id) else {
        return error_response("Failed to decrypt cipher: decryption operation failed");
    };

    match serde_json::to_string(&cipher_view) {
        Ok(json) => CString::new(json).unwrap().into_raw(),
        Err(_) => error_response("Failed to serialize decrypted cipher"),
    }
}

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

    let Ok(key) = SymmetricCryptoKey::try_from(&BitwardenLegacyKeyBytes::from(key_bytes.as_slice())) else {
        return error_response("Failed to create symmetric key: invalid key format or length");
    };

    let Ok(encrypted) = plaintext.to_string().encrypt_with_key(&key) else {
        return error_response("Failed to encrypt string");
    };

    CString::new(encrypted.to_string()).unwrap().into_raw()
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::{free_c_string, generate_organization_keys};
    use bitwarden_vault::{CipherType, LoginView};

    fn create_test_cipher_view() -> CipherView {
        CipherView {
            id: None,
            organization_id: None,
            folder_id: None,
            collection_ids: vec![],
            key: None,
            name: "Test Login".to_string(),
            notes: Some("Secret notes".to_string()),
            r#type: CipherType::Login,
            login: Some(LoginView {
                username: Some("testuser@example.com".to_string()),
                password: Some("SuperSecretP@ssw0rd!".to_string()),
                password_revision_date: None,
                uris: None,
                totp: None,
                autofill_on_page_load: None,
                fido2_credentials: None,
            }),
            identity: None,
            card: None,
            secure_note: None,
            ssh_key: None,
            favorite: false,
            reprompt: bitwarden_vault::CipherRepromptType::None,
            organization_use_totp: false,
            edit: true,
            permissions: None,
            view_password: true,
            local_data: None,
            attachments: None,
            attachment_decryption_failures: None,
            fields: None,
            password_history: None,
            creation_date: "2025-01-01T00:00:00Z".parse().unwrap(),
            deleted_date: None,
            revision_date: "2025-01-01T00:00:00Z".parse().unwrap(),
            archived_date: None,
        }
    }

    fn call_encrypt_cipher(cipher_json: &str, key_b64: &str) -> String {
        let cipher_cstr = CString::new(cipher_json).unwrap();
        let key_cstr = CString::new(key_b64).unwrap();

        let result_ptr = unsafe { encrypt_cipher(cipher_cstr.as_ptr(), key_cstr.as_ptr()) };
        let result_cstr = unsafe { CStr::from_ptr(result_ptr) };
        let result = result_cstr.to_str().unwrap().to_owned();
        unsafe { free_c_string(result_ptr as *mut c_char) };

        result
    }

    fn make_test_key_b64() -> String {
        SymmetricCryptoKey::make_aes256_cbc_hmac_key()
            .to_base64()
            .into()
    }

    #[test]
    fn encrypt_cipher_produces_encrypted_fields() {
        let key_b64 = make_test_key_b64();
        let cipher_view = create_test_cipher_view();
        let cipher_json = serde_json::to_string(&cipher_view).unwrap();

        let encrypted_json = call_encrypt_cipher(&cipher_json, &key_b64);

        assert!(
            !encrypted_json.contains("\"error\""),
            "Got error: {}",
            encrypted_json
        );

        let encrypted_cipher: Cipher =
            serde_json::from_str(&encrypted_json).expect("Failed to parse encrypted cipher JSON");

        let encrypted_name = encrypted_cipher.name.to_string();
        assert!(
            encrypted_name.starts_with("2."),
            "Name should be encrypted: {}",
            encrypted_name
        );

        let login = encrypted_cipher.login.expect("Login should be present");
        if let Some(username) = &login.username {
            assert!(
                username.to_string().starts_with("2."),
                "Username should be encrypted"
            );
        }
        if let Some(password) = &login.password {
            assert!(
                password.to_string().starts_with("2."),
                "Password should be encrypted"
            );
        }
    }

    #[test]
    fn encrypt_cipher_works_with_generated_org_key() {
        let org_keys_ptr = unsafe { generate_organization_keys() };
        let org_keys_cstr = unsafe { CStr::from_ptr(org_keys_ptr) };
        let org_keys_json = org_keys_cstr.to_str().unwrap().to_owned();
        unsafe { free_c_string(org_keys_ptr as *mut c_char) };

        let org_keys: serde_json::Value = serde_json::from_str(&org_keys_json).unwrap();
        let org_key_b64 = org_keys["key"].as_str().unwrap();

        let cipher_view = create_test_cipher_view();
        let cipher_json = serde_json::to_string(&cipher_view).unwrap();

        let encrypted_json = call_encrypt_cipher(&cipher_json, org_key_b64);

        assert!(
            !encrypted_json.contains("\"error\""),
            "Got error: {}",
            encrypted_json
        );

        let encrypted_cipher: Cipher = serde_json::from_str(&encrypted_json).unwrap();
        assert!(encrypted_cipher.name.to_string().starts_with("2."));
    }

    #[test]
    fn encrypt_cipher_rejects_invalid_json() {
        let key_b64 = make_test_key_b64();

        let error_json = call_encrypt_cipher("{ this is not valid json }", &key_b64);

        assert!(
            error_json.contains("\"error\""),
            "Should return error for invalid JSON"
        );
        assert!(error_json.contains("Failed to parse CipherView JSON"));
    }

    #[test]
    fn encrypt_cipher_rejects_invalid_base64_key() {
        let cipher_view = create_test_cipher_view();
        let cipher_json = serde_json::to_string(&cipher_view).unwrap();

        let error_json = call_encrypt_cipher(&cipher_json, "not-valid-base64!!!");

        assert!(
            error_json.contains("\"error\""),
            "Should return error for invalid base64"
        );
        assert!(error_json.contains("Failed to decode base64 key"));
    }

    #[test]
    fn encrypt_cipher_rejects_wrong_key_length() {
        let cipher_view = create_test_cipher_view();
        let cipher_json = serde_json::to_string(&cipher_view).unwrap();
        let short_key_b64 = STANDARD.encode(b"too short");

        let error_json = call_encrypt_cipher(&cipher_json, &short_key_b64);

        assert!(
            error_json.contains("\"error\""),
            "Should return error for wrong key length"
        );
        assert!(error_json.contains("invalid key format or length"));
    }

    fn call_decrypt_cipher(cipher_json: &str, key_b64: &str) -> String {
        let cipher_cstr = CString::new(cipher_json).unwrap();
        let key_cstr = CString::new(key_b64).unwrap();

        let result_ptr = unsafe { decrypt_cipher(cipher_cstr.as_ptr(), key_cstr.as_ptr()) };
        let result_cstr = unsafe { CStr::from_ptr(result_ptr) };
        let result = result_cstr.to_str().unwrap().to_owned();
        unsafe { free_c_string(result_ptr as *mut c_char) };

        result
    }

    #[test]
    fn encrypt_decrypt_roundtrip_preserves_plaintext() {
        let key_b64 = make_test_key_b64();
        let original_view = create_test_cipher_view();
        let original_json = serde_json::to_string(&original_view).unwrap();

        let encrypted_json = call_encrypt_cipher(&original_json, &key_b64);
        assert!(
            !encrypted_json.contains("\"error\""),
            "Encryption failed: {}",
            encrypted_json
        );

        let decrypted_json = call_decrypt_cipher(&encrypted_json, &key_b64);
        assert!(
            !decrypted_json.contains("\"error\""),
            "Decryption failed: {}",
            decrypted_json
        );

        let decrypted_view: CipherView = serde_json::from_str(&decrypted_json)
            .expect("Failed to parse decrypted CipherView");

        assert_eq!(decrypted_view.name, original_view.name);
        assert_eq!(decrypted_view.notes, original_view.notes);

        let original_login = original_view.login.expect("Original should have login");
        let decrypted_login = decrypted_view.login.expect("Decrypted should have login");

        assert_eq!(decrypted_login.username, original_login.username);
        assert_eq!(decrypted_login.password, original_login.password);
    }

    #[test]
    fn decrypt_cipher_rejects_wrong_key() {
        let encrypt_key = make_test_key_b64();
        let wrong_key = make_test_key_b64();

        let original_view = create_test_cipher_view();
        let original_json = serde_json::to_string(&original_view).unwrap();

        let encrypted_json = call_encrypt_cipher(&original_json, &encrypt_key);
        assert!(!encrypted_json.contains("\"error\""));

        let decrypted_json = call_decrypt_cipher(&encrypted_json, &wrong_key);

        // Decryption with wrong key should fail or produce garbage
        // The SDK may return an error or the MAC validation will fail
        let result: Result<CipherView, _> = serde_json::from_str(&decrypted_json);
        if !decrypted_json.contains("\"error\"") {
            // If no error, the decrypted data should not match original
            if let Ok(view) = result {
                assert_ne!(
                    view.name, original_view.name,
                    "Decryption with wrong key should not produce original plaintext"
                );
            }
        }
    }
}
