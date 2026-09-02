//! Attachment encryption for the Seeder.
//!
//! Encrypts an attachment's file bytes and filename in one of Bitwarden's canonical attachment scheme
//! versions (v0/v1/v2) so clients exercise every attachment decrypt branch. All crypto runs through the
//! same `bitwarden_crypto` primitives real clients use; only ciphertext ever leaves this module.

use std::ffi::{c_char, CStr, CString};

use base64::{engine::general_purpose::STANDARD, Engine};

use bitwarden_crypto::{
    EncString, KeyEncryptable, OctetStreamBytes, SymmetricCryptoKey, SymmetricKeyAlgorithm,
};

use crate::crypto_util::{error_response, parse_key, unwrap_key, wrap_key};

/// Encrypt an attachment's file bytes and filename for the Seeder in one of Bitwarden's attachment
/// scheme versions (v0/v1/v2) so clients exercise every decrypt branch.
///
/// # Arguments
/// * `file_bytes_b64` - Base64-encoded plaintext file bytes
/// * `vault_key_b64` - Base64-encoded vault key (the user or organization symmetric key)
/// * `wrapped_cipher_key` - The cipher's wrapped `Key` EncString; only used for v2. Pass "" for none.
/// * `filename` - The plaintext filename
/// * `mode` - 0 = v0 no attachment key (bytes+filename with vault key); 1 = v1 attachment key wrapped by
///   vault key; 2 = v2 attachment key wrapped by the cipher key (unwrapped from `wrapped_cipher_key`)
///
/// # Returns
/// JSON `{ "key": <EncString|null>, "fileName": <EncString>, "blob": <base64 EncArrayBuffer>, "size": <u64> }`.
/// The blob is the AES-256-CBC-HMAC EncArrayBuffer binary layout (`0x02 | iv | mac | ciphertext`).
///
/// # Safety
/// All pointers must be valid null-terminated strings.
#[no_mangle]
pub unsafe extern "C" fn encrypt_attachment(
    file_bytes_b64: *const c_char,
    vault_key_b64: *const c_char,
    wrapped_cipher_key: *const c_char,
    filename: *const c_char,
    mode: u32,
) -> *const c_char {
    let Ok(file_b64) = CStr::from_ptr(file_bytes_b64).to_str() else {
        return error_response("Invalid UTF-8 in file_bytes_b64");
    };
    let Ok(vault_key_b64) = CStr::from_ptr(vault_key_b64).to_str() else {
        return error_response("Invalid UTF-8 in vault_key_b64");
    };
    let Ok(wrapped_cipher_key) = CStr::from_ptr(wrapped_cipher_key).to_str() else {
        return error_response("Invalid UTF-8 in wrapped_cipher_key");
    };
    let Ok(filename) = CStr::from_ptr(filename).to_str() else {
        return error_response("Invalid UTF-8 in filename");
    };

    match encrypt_attachment_internal(file_b64, vault_key_b64, wrapped_cipher_key, filename, mode) {
        Ok(json) => CString::new(json).unwrap().into_raw(),
        Err(msg) => error_response(&msg),
    }
}

fn encrypt_attachment_internal(
    file_b64: &str,
    vault_key_b64: &str,
    wrapped_cipher_key: &str,
    filename: &str,
    mode: u32,
) -> Result<String, String> {
    let file_bytes = STANDARD
        .decode(file_b64)
        .map_err(|_| "Failed to decode base64 file bytes".to_string())?;
    let vault_key = parse_key(vault_key_b64)?;

    let (blob, enc_filename, wrapped_key): (Vec<u8>, String, Option<String>) = match mode {
        // v0 (account-key-based): no attachment key. Bytes and filename encrypted directly with the vault key.
        0 => (
            encrypt_buffer(&file_bytes, &vault_key)?,
            encrypt_str(filename, &vault_key)?,
            None,
        ),
        // v1 (attachment-key-based). Bytes with the attachment key; filename with the vault key.
        1 => {
            let attachment_key = SymmetricCryptoKey::make(SymmetricKeyAlgorithm::Aes256CbcHmac);
            (
                encrypt_buffer(&file_bytes, &attachment_key)?,
                encrypt_str(filename, &vault_key)?,
                Some(wrap_key(&attachment_key, &vault_key)?),
            )
        }
        // v2 (attachment-cipher-key-based). Bytes with the attachment key; filename with the cipher key.
        2 => {
            if wrapped_cipher_key.trim().is_empty() {
                return Err("Attachment v2 requires a wrapped_cipher_key".to_string());
            }
            let cipher_key = unwrap_key(wrapped_cipher_key, &vault_key)?;
            let attachment_key = SymmetricCryptoKey::make(SymmetricKeyAlgorithm::Aes256CbcHmac);
            (
                encrypt_buffer(&file_bytes, &attachment_key)?,
                encrypt_str(filename, &cipher_key)?,
                Some(wrap_key(&attachment_key, &cipher_key)?),
            )
        }
        _ => return Err(format!("Unsupported attachment scheme version: {mode}")),
    };

    let result = serde_json::json!({
        "key": wrapped_key,
        "fileName": enc_filename,
        "blob": STANDARD.encode(&blob),
        "size": blob.len() as u64,
    });

    serde_json::to_string(&result).map_err(|_| "Failed to serialize attachment result".to_string())
}

/// Encrypt a raw byte buffer with a symmetric key and serialize it to the EncArrayBuffer binary layout.
fn encrypt_buffer(bytes: &[u8], key: &SymmetricCryptoKey) -> Result<Vec<u8>, String> {
    let encrypted: EncString = OctetStreamBytes::from(bytes.to_vec())
        .encrypt_with_key(key)
        .map_err(|_| "Failed to encrypt attachment data".to_string())?;
    encrypted
        .to_buffer()
        .map_err(|_| "Failed to serialize attachment buffer".to_string())
}

/// Encrypt a plaintext string with a symmetric key, returning an EncString.
fn encrypt_str(plaintext: &str, key: &SymmetricCryptoKey) -> Result<String, String> {
    let encrypted = plaintext
        .to_string()
        .encrypt_with_key(key)
        .map_err(|_| "Failed to encrypt string".to_string())?;
    Ok(encrypted.to_string())
}

#[cfg(test)]
mod tests {
    use super::*;
    use bitwarden_crypto::KeyDecryptable;

    fn make_test_key() -> SymmetricCryptoKey {
        SymmetricCryptoKey::make(SymmetricKeyAlgorithm::Aes256CbcHmac)
    }

    #[test]
    fn encrypt_attachment_v0_no_key_roundtrip() {
        let vault = make_test_key();
        let vault_b64: String = vault.to_base64().into();
        let data = b"hello attachment";
        let data_b64 = STANDARD.encode(data);

        let json = encrypt_attachment_internal(&data_b64, &vault_b64, "", "notes.txt", 0).unwrap();
        let parsed: serde_json::Value = serde_json::from_str(&json).unwrap();

        // v0 carries no attachment key.
        assert!(parsed["key"].is_null(), "v0 must have a null key");

        // Blob is a type-2 EncArrayBuffer and decrypts directly with the vault key.
        let blob = STANDARD.decode(parsed["blob"].as_str().unwrap()).unwrap();
        assert_eq!(
            blob[0], 2,
            "blob must be an AES-256-CBC-HMAC EncArrayBuffer"
        );
        let enc = EncString::from_buffer(&blob).unwrap();
        let decrypted: Vec<u8> = enc.decrypt_with_key(&vault).unwrap();
        assert_eq!(decrypted, data);

        // Filename decrypts with the vault key.
        let fname: EncString = parsed["fileName"].as_str().unwrap().parse().unwrap();
        let fname_dec: String = fname.decrypt_with_key(&vault).unwrap();
        assert_eq!(fname_dec, "notes.txt");

        // Size equals the encrypted blob length.
        assert_eq!(parsed["size"].as_u64().unwrap(), blob.len() as u64);
    }

    #[test]
    fn encrypt_attachment_v1_vault_wrapped_roundtrip() {
        let vault = make_test_key();
        let vault_b64: String = vault.to_base64().into();
        let data = b"pdf-ish bytes";
        let data_b64 = STANDARD.encode(data);

        let json = encrypt_attachment_internal(&data_b64, &vault_b64, "", "report.pdf", 1).unwrap();
        let parsed: serde_json::Value = serde_json::from_str(&json).unwrap();

        // Attachment key present, wrapped by the vault key.
        let wrapped = parsed["key"].as_str().unwrap();
        assert!(
            wrapped.starts_with("2."),
            "wrapped key must be a type-2 EncString"
        );
        let attachment_key = unwrap_key(wrapped, &vault).unwrap();

        // Blob decrypts with the unwrapped attachment key.
        let blob = STANDARD.decode(parsed["blob"].as_str().unwrap()).unwrap();
        let enc = EncString::from_buffer(&blob).unwrap();
        let decrypted: Vec<u8> = enc.decrypt_with_key(&attachment_key).unwrap();
        assert_eq!(decrypted, data);

        // Filename decrypts with the vault key (never the attachment key).
        let fname: EncString = parsed["fileName"].as_str().unwrap().parse().unwrap();
        let fname_dec: String = fname.decrypt_with_key(&vault).unwrap();
        assert_eq!(fname_dec, "report.pdf");
    }

    #[test]
    fn encrypt_attachment_v2_cipher_wrapped_roundtrip() {
        let vault = make_test_key();
        let vault_b64: String = vault.to_base64().into();

        // Simulate a cipher-key cipher: a cipher key wrapped by the vault key.
        let cipher_key = make_test_key();
        let wrapped_cipher_key = wrap_key(&cipher_key, &vault).unwrap();

        let data = b"secret-5";
        let data_b64 = STANDARD.encode(data);

        let json =
            encrypt_attachment_internal(&data_b64, &vault_b64, &wrapped_cipher_key, "m5.bin", 2)
                .unwrap();
        let parsed: serde_json::Value = serde_json::from_str(&json).unwrap();

        // Attachment key unwraps with the cipher key, not the vault key.
        let wrapped = parsed["key"].as_str().unwrap();
        let attachment_key = unwrap_key(wrapped, &cipher_key).unwrap();
        assert!(
            unwrap_key(wrapped, &vault).is_err(),
            "must not unwrap with the vault key"
        );

        let blob = STANDARD.decode(parsed["blob"].as_str().unwrap()).unwrap();
        let enc = EncString::from_buffer(&blob).unwrap();
        let decrypted: Vec<u8> = enc.decrypt_with_key(&attachment_key).unwrap();
        assert_eq!(decrypted, data);

        // Filename decrypts with the cipher key.
        let fname: EncString = parsed["fileName"].as_str().unwrap().parse().unwrap();
        let fname_dec: String = fname.decrypt_with_key(&cipher_key).unwrap();
        assert_eq!(fname_dec, "m5.bin");
    }

    #[test]
    fn encrypt_attachment_v2_requires_cipher_key() {
        let vault = make_test_key();
        let vault_b64: String = vault.to_base64().into();
        let data_b64 = STANDARD.encode(b"x");

        let err = encrypt_attachment_internal(&data_b64, &vault_b64, "", "x.txt", 2).unwrap_err();
        assert!(err.contains("v2"), "got: {err}");
    }

    #[test]
    fn encrypt_attachment_rejects_unsupported_mode() {
        let vault = make_test_key();
        let vault_b64: String = vault.to_base64().into();
        let data_b64 = STANDARD.encode(b"x");

        let err = encrypt_attachment_internal(&data_b64, &vault_b64, "", "x.txt", 3).unwrap_err();
        assert!(
            err.contains("Unsupported attachment scheme version"),
            "got: {err}"
        );
    }

    #[test]
    fn encrypt_attachment_rejects_invalid_base64_file() {
        let vault = make_test_key();
        let vault_b64: String = vault.to_base64().into();

        let err = encrypt_attachment_internal("not*valid*base64", &vault_b64, "", "x.txt", 0)
            .unwrap_err();
        assert!(
            err.contains("Failed to decode base64 file bytes"),
            "got: {err}"
        );
    }
}
