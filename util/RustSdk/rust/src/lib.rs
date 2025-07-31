#![allow(clippy::missing_safety_doc)]
use std::{
    ffi::{c_char, CStr, CString},
    num::NonZeroU32,
};

use bitwarden_crypto::{HashPurpose, Kdf, MasterKey};

#[no_mangle]
pub extern "C" fn my_add(x: i32, y: i32) -> i32 {
    x + y
}

#[no_mangle]
pub unsafe extern "C" fn generate_user_keys(
    email: *const c_char,
    password: *const c_char,
) -> *const c_char {
    // TODO: We might want to make KDF configurable in the future.
    let kdf = Kdf::PBKDF2 {
        iterations: NonZeroU32::new(600_000).unwrap(),
    };

    let email = CStr::from_ptr(email).to_str().unwrap();
    let password = CStr::from_ptr(password).to_str().unwrap();

    let master_key = MasterKey::derive(password, email, &kdf).unwrap();
    let master_password_hash = master_key
        .derive_master_key_hash(password.as_bytes(), HashPurpose::ServerAuthorization)
        .unwrap();
    let (user_key, encrypted_user_key) = master_key.make_user_key().unwrap();
    let keys = user_key.make_key_pair().unwrap();

    let json = serde_json::json!({
        "masterPasswordHash": master_password_hash,
        "encryptedUserKey": encrypted_user_key.to_string(),
        "publicKey": keys.public.to_string(),
        "privateKey": keys.private.to_string(),
    })
    .to_string();

    let result = CString::new(json).unwrap();

    result.into_raw()
}

/// # Safety
///
/// The `email` and `password` pointers must be valid null-terminated C strings.
/// Both pointers must be non-null and point to valid memory for the duration of the function call.
#[no_mangle]
pub unsafe extern "C" fn hash_password(
    email: *const c_char,
    password: *const c_char,
) -> *const c_char {
    let kdf = Kdf::PBKDF2 {
        iterations: NonZeroU32::new(600_000).unwrap(),
    };

    let email = CStr::from_ptr(email).to_str().unwrap();
    let password = CStr::from_ptr(password).to_str().unwrap();

    let master_key = MasterKey::derive(password, email, &kdf).unwrap();

    let res = master_key
        .derive_master_key_hash(password.as_bytes(), HashPurpose::ServerAuthorization)
        .unwrap();

    let res = CString::new(res).unwrap();

    res.into_raw()
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
