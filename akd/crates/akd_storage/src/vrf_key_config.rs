use serde::{Deserialize, Serialize};

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(tag = "type")]
pub enum VrfKeyConfig {
    /// **WARNING**: Do not use this in production systems. This is only for testing and debugging.
    /// This is a version of VRFKeyStorage for testing purposes, which uses the example from the VRF crate.
    ///
    /// const KEY_MATERIAL: &str = "c9afa9d845ba75166b5c215767b1d6934e50c3db36e89b127b8a622b120f6721";
    #[cfg(test)]
    ConstantVrfKey,
    /// The root key is a valid and random chacha20poly1305 symmetric key directly. The provided string will be decoded
    /// from base64 to produce a key.
    ///
    /// For VRF Key generation, a random VRF private key will be sampled, encrypted with this symmetric
    /// key, and the resulting encrypted VRF key will be stored. The symmetric key will not be persisted.
    ///
    /// For VRF Key retrieval, the symmetric key will be hashed to derive a root key identifier. This will be used
    /// to lookup an associated VRF key. If none is found, the application will error if a VRF key already exists.
    /// Otherwise it goes on to generate a new VRF key. If a VRF key is found, it will be decrypted using this
    /// symmetric key
    ///
    /// Losing this key is equivalent to losing your directory's VRF key.
    B64EncodedSymmetricKey { key: String },
    /// The root key is an asymmetric RSA key. The provided string will be decoded from pkcs1 PEM to produce a private RSA key.
    ///
    /// For VRF key generation, a random VRF private key will be sampled, a random symmetric key will be sampled,
    /// the VRF key will be encrypted with the symmetric key, and the symmetric key will be encrypted with the RSA public key.
    /// The resulting encrypted VRF key and encrypted symmetric key will be stored. The RSA private key will not be persisted.
    ///
    /// For VRF key retrieval, the RSA private key will be hashed to derive a root key identifier. This will be used
    /// to lookup an associated VRF key. If None is found, the application will error if a VRF key already exists.
    /// Otherwise it goes on to generate a new VRF key. If a VRF key is found, the symmetric key will be decrypted using
    /// the RSA private key, and then the VRF key will be decrypted using the symmetric key.
    ///
    /// Losing this key is equivalent to losing your directory's VRF key.
    PEMEncodedRSAKey { private_key: String },
}
