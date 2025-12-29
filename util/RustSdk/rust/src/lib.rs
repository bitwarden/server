#![allow(clippy::missing_safety_doc)]
use std::{
    ffi::{c_char, CStr, CString},
    num::NonZeroU32,
};

use base64::{engine::general_purpose::STANDARD, Engine};

use bitwarden_crypto::{
    AsymmetricCryptoKey, AsymmetricPublicCryptoKey, BitwardenLegacyKeyBytes, HashPurpose, Kdf,
    KeyEncryptable, MasterKey, RsaKeyPair, SpkiPublicKeyBytes, SymmetricCryptoKey,
    UnsignedSharedKey, UserKey,
};

#[no_mangle]
pub unsafe extern "C" fn generate_user_keys(
    email: *const c_char,
    password: *const c_char,
) -> *const c_char {
    let email = CStr::from_ptr(email).to_str().unwrap();
    let password = CStr::from_ptr(password).to_str().unwrap();

    println!("Generating keys for {email}");
    println!("Password: {password}");

    let kdf = Kdf::PBKDF2 {
        iterations: NonZeroU32::new(5_000).unwrap(),
    };

    let master_key = MasterKey::derive(password, email, &kdf).unwrap();

    let master_password_hash =
        master_key.derive_master_key_hash(password.as_bytes(), HashPurpose::ServerAuthorization);

    println!("Master password hash: {}", master_password_hash);

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
