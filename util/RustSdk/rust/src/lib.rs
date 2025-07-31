#![allow(clippy::missing_safety_doc)]
use std::{
    ffi::{c_char, CStr, CString},
    num::NonZeroU32,
};

use base64::{engine::general_purpose::STANDARD, Engine};

use bitwarden_crypto::{
    AsymmetricPublicCryptoKey, BitwardenLegacyKeyBytes, HashPurpose, Kdf, MasterKey,
    SpkiPublicKeyBytes, SymmetricCryptoKey, UnsignedSharedKey, UserKey,
};

#[no_mangle]
pub unsafe extern "C" fn generate_user_keys(
    email: *const c_char,
    password: *const c_char,
) -> *const c_char {
    // TODO: We might want to make KDF configurable in the future.
    let kdf = Kdf::PBKDF2 {
        iterations: NonZeroU32::new(5_000).unwrap(),
    };

    let email = CStr::from_ptr(email).to_str().unwrap();
    let password = CStr::from_ptr(password).to_str().unwrap();

    let master_key = MasterKey::derive(password, email, &kdf).unwrap();
    let master_password_hash = master_key
        .derive_master_key_hash(password.as_bytes(), HashPurpose::ServerAuthorization)
        .unwrap();
    let (user_key, encrypted_user_key) = master_key.make_user_key().unwrap();
    let keypair = user_key.make_key_pair().unwrap();

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
