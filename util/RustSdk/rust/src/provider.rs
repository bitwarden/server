//! Provider key-wrapping functions for the Seeder.
//!
//! Wraps one symmetric key under another so clients can unwrap it back into a `SymmetricCryptoKey` —
//! e.g. a `ProviderOrganization.Key`, which is an organization's symmetric key wrapped with the
//! provider's symmetric key. All crypto runs through the same `bitwarden_crypto` primitives real
//! clients use; only ciphertext ever leaves this module.

use std::ffi::{c_char, CStr, CString};

use crate::crypto_util::{error_response, parse_key, wrap_key};

/// Wrap a symmetric key with another symmetric key, returning the wrapped key as an EncString.
///
/// Unlike `encrypt_string`, this encrypts the *raw key bytes* of `key_to_wrap_b64` (not its base64
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

#[cfg(test)]
mod tests {
    use super::*;
    use crate::crypto_util::unwrap_key;
    use crate::free_c_string;
    use bitwarden_crypto::{SymmetricCryptoKey, SymmetricKeyAlgorithm};

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
}
