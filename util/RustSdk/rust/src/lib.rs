#![allow(clippy::missing_safety_doc)]

mod cipher;
mod rsa_keys;

use std::{
    ffi::{c_char, CStr, CString},
    num::NonZeroU32,
    sync::LazyLock,
};

use base64::{engine::general_purpose::STANDARD, Engine};

use bitwarden_crypto::{
    BitwardenLegacyKeyBytes, HashPurpose, Kdf, KeyEncryptable, MasterKey, Pkcs8PrivateKeyBytes,
    PrivateKey, PublicKey, RsaKeyPair, SpkiPublicKeyBytes, SymmetricCryptoKey, UnsignedSharedKey,
    UserKey,
};

#[no_mangle]
pub unsafe extern "C" fn generate_user_keys(
    email: *const c_char,
    password: *const c_char,
    kdf_iterations: u32,
    pool_index: u32,
) -> *const c_char {
    let email = CStr::from_ptr(email).to_str().unwrap();
    let password = CStr::from_ptr(password).to_str().unwrap();

    let iterations = match NonZeroU32::new(kdf_iterations) {
        Some(iter) => iter,
        None => return error_response("kdf_iterations must be non-zero"),
    };

    let kdf = Kdf::PBKDF2 { iterations };

    let master_key = MasterKey::derive(password, email, &kdf).unwrap();

    let master_password_hash =
        master_key.derive_master_key_hash(password.as_bytes(), HashPurpose::ServerAuthorization);

    let (user_key, encrypted_user_key) = master_key.make_user_key().unwrap();

    let keypair = keypair(&user_key.0, pool_index);

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

fn error_response(message: &str) -> *const c_char {
    let json = serde_json::json!({ "error": message }).to_string();
    CString::new(json).unwrap().into_raw()
}

struct CachedRsaMaterial {
    private_der: Pkcs8PrivateKeyBytes,
    public_der: SpkiPublicKeyBytes,
}

/// Pre-parsed DER keypairs from `rsa_keys.rs`, lazily initialized on first access.
static RSA_POOL: LazyLock<Vec<CachedRsaMaterial>> = LazyLock::new(|| {
    rsa_keys::TEST_FAKE_RSA_KEYS
        .iter()
        .map(|pem| {
            let pk = PrivateKey::from_pem(pem).expect("seeded RSA PEM must be valid");
            CachedRsaMaterial {
                public_der: pk
                    .to_public_key()
                    .to_der()
                    .expect("public DER conversion failed"),
                private_der: pk.to_der().expect("private DER conversion failed"),
            }
        })
        .collect()
});

fn keypair(key: &SymmetricCryptoKey, pool_index: u32) -> RsaKeyPair {
    let pool = &*RSA_POOL;
    let material = &pool[pool_index as usize % pool.len()];

    RsaKeyPair {
        private: material.private_der.encrypt_with_key(key).unwrap(),
        public: material.public_der.clone().into(),
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
        PublicKey::from_der(&SpkiPublicKeyBytes::from(user_public_key)).unwrap();

    // The Seeder uses unsigned key encapsulation for test data generation.
    // When the SDK removes this deprecated API, migrate to signed encapsulation.
    #[allow(deprecated)]
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

#[cfg(test)]
mod tests {
    use std::collections::HashSet;

    use bitwarden_crypto::SymmetricCryptoKey;

    use super::RSA_POOL;
    use crate::keypair;

    #[test]
    fn rsa_pool_initializes_all_entries() {
        let pool = &*RSA_POOL;
        assert_eq!(pool.len(), 100, "pool should contain exactly 100 keypairs");
    }

    #[test]
    fn rsa_pool_keys_are_unique() {
        let pool = &*RSA_POOL;
        let distinct: HashSet<Vec<u8>> = pool
            .iter()
            .map(|m| m.public_der.as_ref().to_vec())
            .collect();
        assert_eq!(
            distinct.len(),
            pool.len(),
            "every pool entry should have a distinct public key"
        );
    }

    #[test]
    fn keypair_different_indices_produce_different_public_keys() {
        let key = SymmetricCryptoKey::make_aes256_cbc_hmac_key();
        let kp0 = keypair(&key, 0);
        let kp1 = keypair(&key, 1);
        assert_ne!(
            kp0.public.to_string(),
            kp1.public.to_string(),
            "different pool indices should yield different public keys"
        );
    }

    #[test]
    fn keypair_same_index_produces_same_public_key() {
        let key = SymmetricCryptoKey::make_aes256_cbc_hmac_key();
        let kp_a = keypair(&key, 42);
        let kp_b = keypair(&key, 42);
        assert_eq!(
            kp_a.public.to_string(),
            kp_b.public.to_string(),
            "same pool index should always produce the same public key"
        );
    }

    #[test]
    fn keypair_index_wraps_at_pool_boundary() {
        let key = SymmetricCryptoKey::make_aes256_cbc_hmac_key();
        let kp_zero = keypair(&key, 0);
        let kp_wrapped = keypair(&key, 100);
        assert_eq!(
            kp_zero.public.to_string(),
            kp_wrapped.public.to_string(),
            "index 100 should wrap to index 0"
        );
    }
}
