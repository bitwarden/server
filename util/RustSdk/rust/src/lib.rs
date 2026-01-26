#![allow(clippy::missing_safety_doc)]
use std::{
    ffi::{c_char, CStr, CString},
    num::NonZeroU32,
};

use base64::{engine::general_purpose::STANDARD, Engine};

use bitwarden_core::key_management::KeyIds;
use bitwarden_crypto::{
    AsymmetricCryptoKey, AsymmetricPublicCryptoKey, BitwardenLegacyKeyBytes, CompositeEncryptable,
    Decryptable, HashPurpose, Kdf, KeyEncryptable, KeyStore, MasterKey, RsaKeyPair,
    SpkiPublicKeyBytes, SymmetricCryptoKey, UnsignedSharedKey, UserKey,
};
use bitwarden_vault::{Cipher, CipherView};

#[no_mangle]
pub unsafe extern "C" fn generate_user_keys(
    email: *const c_char,
    password: *const c_char,
) -> *const c_char {
    let email = CStr::from_ptr(email).to_str().unwrap();
    let password = CStr::from_ptr(password).to_str().unwrap();

    let kdf = Kdf::PBKDF2 {
        iterations: NonZeroU32::new(5_000).unwrap(),
    };

    let master_key = MasterKey::derive(password, email, &kdf).unwrap();

    let master_password_hash =
        master_key.derive_master_key_hash(password.as_bytes(), HashPurpose::ServerAuthorization);

    let (user_key, encrypted_user_key) = master_key.make_user_key().unwrap();

    let keypair = keypair(&user_key.0);

    let json = serde_json::json!({
        "masterPasswordHash": master_password_hash,
        "key": user_key.0.to_base64(),
        "encryptedUserKey": encrypted_user_key.to_string(),
        "publicKey": keypair.public.to_string(),
        "privateKey": keypair.private.to_string(),
    })
    .to_string();

    let result = CString::new(json).unwrap();

    result.into_raw()
}

fn keypair(key: &SymmetricCryptoKey) -> RsaKeyPair {
    const RSA_PRIVATE_KEY: &str = "-----BEGIN PRIVATE KEY-----
MIIEvQIBADANBgkqhkiG9w0BAQEFAASCBKcwggSjAgEAAoIBAQCXRVrCX+2hfOQS
8HzYUS2oc/jGVTZpv+/Ryuoh9d8ihYX9dd0cYh2tl6KWdFc88lPUH11Oxqy20Rk2
e5r/RF6T9yM0Me3NPnaKt+hlhLtfoc0h86LnhD56A9FDUfuI0dVnPcrwNv0YJIo9
4LwxtbqBULNvXl6wJ7WAbODrCQy5ZgMVg+iH+gGpwiqsZqHt+KuoHWcN53MSPDfa
F4/YMB99U3TziJMOOJask1TEEnakMPln11PczNDazT17DXIxYrbPfutPdh6sLs6A
QOajdZijfEvepgnOe7cQ7aeatiOJFrjTApKPGxOVRzEMX4XS4xbyhH0QxQeB6l16
l8C0uxIBAgMBAAECggEASaWfeVDA3cVzOPFSpvJm20OTE+R6uGOU+7vh36TX/POq
92qBuwbd0h0oMD32FxsXywd2IxtBDUSiFM9699qufTVuM0Q3tZw6lHDTOVG08+tP
dr8qSbMtw7PGFxN79fHLBxejjO4IrM9lapjWpxEF+11x7r+wM+0xRZQ8sNFYG46a
PfIaty4BGbL0I2DQ2y8I57iBCAy69eht59NLMm27fRWGJIWCuBIjlpfzET1j2HLX
UIh5bTBNzqaN039WH49HczGE3mQKVEJZc/efk3HaVd0a1Sjzyn0QY+N1jtZN3jTR
buDWA1AknkX1LX/0tUhuS3/7C3ejHxjw4Dk1ZLo5/QKBgQDIWvqFn0+IKRSu6Ua2
hDsufIHHUNLelbfLUMmFthxabcUn4zlvIscJO00Tq/ezopSRRvbGiqnxjv/mYxuc
vOUBeZtlus0Q9RTACBtw9TGoNTmQbEunJ2FOSlqbQxkBBAjgGEppRPt30iGj/VjA
hCATq2MYOa/X4dVR51BqQAFIEwKBgQDBSIfTFKC/hDk6FKZlgwvupWYJyU9Rkyfs
tPErZFmzoKhPkQ3YORo2oeAYmVUbS9I2iIYpYpYQJHX8jMuCbCz4ONxTCuSIXYQY
UcUq4PglCKp31xBAE6TN8SvhfME9/MvuDssnQinAHuF0GDAhF646T3LLS1not6Vs
zv7brwSoGwKBgQC88v/8cGfi80ssQZeMnVvq1UTXIeQcQnoY5lGHJl3K8mbS3TnX
E6c9j417Fdz+rj8KWzBzwWXQB5pSPflWcdZO886Xu/mVGmy9RWgLuVFhXwCwsVEP
jNX5ramRb0/vY0yzenUCninBsIxFSbIfrPtLUYCc4hpxr+sr2Mg/y6jpvQKBgBez
MRRs3xkcuXepuI2R+BCXL1/b02IJTUf1F+1eLLGd7YV0H+J3fgNc7gGWK51hOrF9
JBZHBGeOUPlaukmPwiPdtQZpu4QNE3l37VlIpKTF30E6mb+BqR+nht3rUjarnMXg
AoEZ18y6/KIjpSMpqC92Nnk/EBM9EYe6Cf4eA9ApAoGAeqEUg46UTlJySkBKURGp
Is3v1kkf5I0X8DnOhwb+HPxNaiEdmO7ckm8+tPVgppLcG0+tMdLjigFQiDUQk2y3
WjyxP5ZvXu7U96jaJRI8PFMoE06WeVYcdIzrID2HvqH+w0UQJFrLJ/0Mn4stFAEz
XKZBokBGnjFnTnKcs7nv/O8=
-----END PRIVATE KEY-----";

    let private_key = AsymmetricCryptoKey::from_pem(RSA_PRIVATE_KEY).unwrap();
    let public_key = private_key.to_public_key().to_der().unwrap();

    let p = private_key.to_der().unwrap();

    RsaKeyPair {
        private: p.encrypt_with_key(key).unwrap(),
        public: public_key.into(),
    }
}

#[no_mangle]
pub unsafe extern "C" fn generate_organization_keys() -> *const c_char {
    let key = SymmetricCryptoKey::make_aes256_cbc_hmac_key();

    let key = UserKey::new(key);
    let keypair = key.make_key_pair().expect("Failed to generate key pair");

    let json = serde_json::json!({
        "key": key.0.to_base64(),
        "publicKey": keypair.public.to_string(),
        "privateKey": keypair.private.to_string(),
    })
    .to_string();

    let result = CString::new(json).unwrap();

    result.into_raw()
}

#[no_mangle]
pub unsafe extern "C" fn generate_user_organization_key(
    user_public_key: *const c_char,
    organization_key: *const c_char,
) -> *const c_char {
    let user_public_key = CStr::from_ptr(user_public_key).to_str().unwrap().to_owned();
    let organization_key = CStr::from_ptr(organization_key)
        .to_str()
        .unwrap()
        .to_owned();

    let user_public_key = STANDARD.decode(user_public_key).unwrap();
    let organization_key = STANDARD.decode(organization_key).unwrap();

    let encapsulation_key =
        AsymmetricPublicCryptoKey::from_der(&SpkiPublicKeyBytes::from(user_public_key)).unwrap();

    let encrypted_key = UnsignedSharedKey::encapsulate_key_unsigned(
        &SymmetricCryptoKey::try_from(&BitwardenLegacyKeyBytes::from(organization_key)).unwrap(),
        &encapsulation_key,
    )
    .unwrap();

    let result = CString::new(encrypted_key.to_string()).unwrap();

    result.into_raw()
}

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
    let cipher_view_json = match CStr::from_ptr(cipher_view_json).to_str() {
        Ok(s) => s,
        Err(_) => return error_response("Invalid UTF-8 in cipher_view_json"),
    };

    let key_b64 = match CStr::from_ptr(symmetric_key_b64).to_str() {
        Ok(s) => s,
        Err(_) => return error_response("Invalid UTF-8 in symmetric_key_b64"),
    };

    let cipher_view: CipherView = match serde_json::from_str(cipher_view_json) {
        Ok(v) => v,
        Err(_) => return error_response("Failed to parse CipherView JSON"),
    };

    let key_bytes = match STANDARD.decode(key_b64) {
        Ok(b) => b,
        Err(_) => return error_response("Failed to decode base64 key"),
    };

    let key =
        match SymmetricCryptoKey::try_from(&BitwardenLegacyKeyBytes::from(key_bytes.as_slice())) {
            Ok(k) => k,
            Err(_) => {
                return error_response(
                    "Failed to create symmetric key: invalid key format or length",
                )
            }
        };

    let store: KeyStore<KeyIds> = KeyStore::default();
    let mut ctx = store.context_mut();
    let key_id = ctx.add_local_symmetric_key(key);

    let cipher = match cipher_view.encrypt_composite(&mut ctx, key_id) {
        Ok(c) => c,
        Err(_) => return error_response("Failed to encrypt cipher: encryption operation failed"),
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
    let cipher_json = match CStr::from_ptr(cipher_json).to_str() {
        Ok(s) => s,
        Err(_) => return error_response("Invalid UTF-8 in cipher_json"),
    };

    let key_b64 = match CStr::from_ptr(symmetric_key_b64).to_str() {
        Ok(s) => s,
        Err(_) => return error_response("Invalid UTF-8 in symmetric_key_b64"),
    };

    let cipher: Cipher = match serde_json::from_str(cipher_json) {
        Ok(c) => c,
        Err(_) => return error_response("Failed to parse Cipher JSON"),
    };

    let key_bytes = match STANDARD.decode(key_b64) {
        Ok(b) => b,
        Err(_) => return error_response("Failed to decode base64 key"),
    };

    let key =
        match SymmetricCryptoKey::try_from(&BitwardenLegacyKeyBytes::from(key_bytes.as_slice())) {
            Ok(k) => k,
            Err(_) => {
                return error_response(
                    "Failed to create symmetric key: invalid key format or length",
                )
            }
        };

    let store: KeyStore<KeyIds> = KeyStore::default();
    let mut ctx = store.context_mut();
    let key_id = ctx.add_local_symmetric_key(key);

    let cipher_view: CipherView = match cipher.decrypt(&mut ctx, key_id) {
        Ok(v) => v,
        Err(_) => return error_response("Failed to decrypt cipher: decryption operation failed"),
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
    let plaintext = match CStr::from_ptr(plaintext).to_str() {
        Ok(s) => s,
        Err(_) => return error_response("Invalid UTF-8 in plaintext"),
    };

    let key_b64 = match CStr::from_ptr(symmetric_key_b64).to_str() {
        Ok(s) => s,
        Err(_) => return error_response("Invalid UTF-8 in symmetric_key_b64"),
    };

    let key_bytes = match STANDARD.decode(key_b64) {
        Ok(b) => b,
        Err(_) => return error_response("Failed to decode base64 key"),
    };

    let key =
        match SymmetricCryptoKey::try_from(&BitwardenLegacyKeyBytes::from(key_bytes.as_slice())) {
            Ok(k) => k,
            Err(_) => {
                return error_response(
                    "Failed to create symmetric key: invalid key format or length",
                )
            }
        };

    let encrypted = match plaintext.to_string().encrypt_with_key(&key) {
        Ok(e) => e,
        Err(_) => return error_response("Failed to encrypt string"),
    };

    CString::new(encrypted.to_string()).unwrap().into_raw()
}

/// # Safety
///
/// The `str` pointer must be a valid pointer previously returned by `CString::into_raw`
/// and must not have already been freed. After calling this function, the pointer must not be used again.
#[no_mangle]
pub unsafe extern "C" fn free_c_string(str: *mut c_char) {
    unsafe {
        drop(CString::from_raw(str));
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use bitwarden_vault::{Cipher, CipherType, LoginView};

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
