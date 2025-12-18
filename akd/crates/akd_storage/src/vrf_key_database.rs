use std::str::FromStr;

use chacha20poly1305::{
    aead::{generic_array::GenericArray, Aead},
    AeadCore, KeyInit, XChaCha20Poly1305,
};
use rsa::{
    pkcs1::{DecodeRsaPrivateKey, EncodeRsaPrivateKey},
    Pkcs1v15Encrypt,
};
use thiserror::Error;
use tracing::{error, info};

use crate::vrf_key_config::VrfKeyConfig;

/// Represents a storage-layer error
#[derive(Debug, Error)]
pub enum VrfKeyRetrievalError {
    /// No VRF key exists for the given root key
    #[error("VRF key not found for the specified root key")]
    KeyNotFound,
    /// Database/storage layer failure (connection, query execution, etc.)
    #[error("Database error during key retrieval")]
    DatabaseError,
    /// Data exists but is corrupted or invalid
    #[error("Retrieved VRF key data is corrupted")]
    CorruptedData,
}

#[derive(Debug, Error)]
#[error("VRF key creation error")]
pub struct VrfKeyCreationError;

#[derive(Debug, Error)]
#[error("Internal VRF key storage error")]
pub struct VrfKeyStorageError;

#[allow(unused)]
trait VrfKeyTable {
    async fn get_vrf_key(config: VrfKeyConfig) -> Result<VrfKeyTableData, VrfKeyRetrievalError>;
    async fn store_vrf_key(table_data: VrfKeyTableData) -> Result<(), VrfKeyStorageError>;
}

pub struct VrfKeyTableData {
    pub root_key_hash: Vec<u8>,
    pub root_key_type: RootKeyType,
    pub enc_sym_key: Option<Vec<u8>>,
    pub sym_enc_vrf_key: Vec<u8>,
    pub sym_enc_vrf_key_nonce: Vec<u8>,
}

pub enum RootKeyType {
    #[cfg(test)]
    None = 0,
    SymmetricKey = 1,
    RsaKey = 2,
}

pub struct VrfKey(pub Vec<u8>);

impl VrfKeyTableData {
    pub async fn new(config: VrfKeyConfig) -> Result<(Self, VrfKey), VrfKeyCreationError> {
        info!("Generating new VRF key and table data");
        // handle constant key case separately to avoid unnecessary key generation / parsing
        #[cfg(test)]
        if let VrfKeyConfig::ConstantVrfKey = config {
            use akd::ecvrf::{HardCodedAkdVRF, VRFKeyStorage};

            return Ok((
                VrfKeyTableData {
                    root_key_hash: vec![],
                    root_key_type: RootKeyType::None,
                    enc_sym_key: None,
                    sym_enc_vrf_key: vec![],
                    sym_enc_vrf_key_nonce: vec![],
                },
                VrfKey((HardCodedAkdVRF {}).retrieve().await.unwrap_or_default()),
            ));
        }

        let (sym, sym_key) = if let VrfKeyConfig::B64EncodedSymmetricKey { key } = &config {
            let raw_key = bitwarden_encoding::B64::from_str(key).map_err(|err| {
                error!(%err, "Invalid b64 encoding of symmetric key");
                VrfKeyCreationError
            })?;
            (
                XChaCha20Poly1305::new_from_slice(raw_key.as_bytes()).map_err(|err| {
                    error!(%err, "Invalid symmetric key length");
                    VrfKeyCreationError
                })?,
                raw_key.as_bytes().to_vec(),
            )
        } else {
            let key = XChaCha20Poly1305::generate_key(rand::thread_rng());
            (XChaCha20Poly1305::new(&key), key.to_vec())
        };
        let vrf_key = ed25519_dalek::SigningKey::generate(&mut rand::thread_rng())
            .to_bytes()
            .to_vec();
        let nonce = XChaCha20Poly1305::generate_nonce(&mut rand::thread_rng());
        let sym_enc_vrf_key = sym.encrypt(&nonce, &vrf_key[..]).map_err(|err| {
            error!(%err, "Failed to encrypt VRF key with symmetric key");
            VrfKeyCreationError
        })?;

        match config {
            #[cfg(test)]
            VrfKeyConfig::ConstantVrfKey => unreachable!(), // handled above
            VrfKeyConfig::B64EncodedSymmetricKey { key: _ } => {
                let root_key_hash = blake3::hash(&sym_key).as_bytes().to_vec();

                Ok((
                    VrfKeyTableData {
                        root_key_hash,
                        root_key_type: RootKeyType::SymmetricKey,
                        enc_sym_key: None,
                        sym_enc_vrf_key,
                        sym_enc_vrf_key_nonce: nonce.to_vec(),
                    },
                    VrfKey(vrf_key),
                ))
            }
            VrfKeyConfig::PEMEncodedRSAKey { private_key } => {
                let rsa_private_key =
                    rsa::RsaPrivateKey::from_pkcs1_pem(&private_key).map_err(|err| {
                        error!(%err, "Failed to decode RSA private key from PEM format");
                        VrfKeyCreationError
                    })?;
                let root_key_hash = blake3::hash(
                    rsa_private_key
                        .to_pkcs1_der()
                        .map_err(|err| {
                            error!(%err, "Failed to encode RSA private key to DER format");
                            VrfKeyCreationError
                        })?
                        .as_bytes(),
                )
                .as_bytes()
                .to_vec();
                let rsa_public_key = rsa_private_key.to_public_key();
                let enc_sym_key = rsa_public_key
                    .encrypt(&mut rand::thread_rng(), Pkcs1v15Encrypt, &sym_key)
                    .map_err(|err| {
                        error!(%err, "Failed to encrypt symmetric key with RSA public key");
                        VrfKeyCreationError
                    })?;

                Ok((
                    VrfKeyTableData {
                        root_key_hash,
                        root_key_type: RootKeyType::RsaKey,
                        enc_sym_key: Some(enc_sym_key),
                        sym_enc_vrf_key,
                        sym_enc_vrf_key_nonce: nonce.to_vec(),
                    },
                    VrfKey(vrf_key),
                ))
            }
        }
    }

    pub async fn to_vrf_key(&self, config: VrfKeyConfig) -> Result<VrfKey, VrfKeyCreationError> {
        info!("Decrypting VrfKeyTableData to obtain VRF key");
        // handle constant key case separately to avoid unnecessary key generation / parsing
        #[cfg(test)]
        if let VrfKeyConfig::ConstantVrfKey = config {
            use akd::ecvrf::{HardCodedAkdVRF, VRFKeyStorage};

            return Ok(VrfKey(
                (HardCodedAkdVRF {}).retrieve().await.unwrap_or_default(),
            ));
        }

        if self.sym_enc_vrf_key_nonce.len() != 24 {
            error!(
                length = self.sym_enc_vrf_key.len(),
                "Invalid nonce length for VRF key decryption"
            );
            return Err(VrfKeyCreationError);
        }
        let nonce = GenericArray::from_slice(self.sym_enc_vrf_key_nonce.as_ref());
        let vrf_key = match config {
            #[cfg(test)]
            VrfKeyConfig::ConstantVrfKey => unreachable!(), // handled above
            VrfKeyConfig::B64EncodedSymmetricKey { key } => {
                let raw_key = bitwarden_encoding::B64::from_str(&key).map_err(|err| {
                    error!(%err, "Invalid b64 encoding of symmetric key");
                    VrfKeyCreationError
                })?;
                let sym = XChaCha20Poly1305::new_from_slice(raw_key.as_bytes()).map_err(|err| {
                    error!(%err, "Invalid symmetric key length");
                    VrfKeyCreationError
                })?;
                let vrf_key = sym
                    .decrypt(nonce, &self.sym_enc_vrf_key[..])
                    .map_err(|err| {
                        error!(%err, "Failed to decrypt VRF key with symmetric key");
                        VrfKeyCreationError
                    })?;

                vrf_key
            }
            VrfKeyConfig::PEMEncodedRSAKey { private_key } => {
                let rsa_private_key =
                    rsa::RsaPrivateKey::from_pkcs1_pem(&private_key).map_err(|err| {
                        error!(%err, "Failed to decode RSA private key from PEM format");
                        VrfKeyCreationError
                    })?;
                let enc_sym_key = self
                    .enc_sym_key
                    .as_ref()
                    .ok_or(VrfKeyCreationError)
                    .map_err(|err| {
                        error!(%err, "Missing encrypted symmetric key for RSA decryption");
                        err
                    })?;
                let sym_key = rsa_private_key
                    .decrypt(Pkcs1v15Encrypt, enc_sym_key)
                    .map_err(|err| {
                        error!(%err, "Failed to decrypt symmetric key with RSA private key");
                        VrfKeyCreationError
                    })?;

                let sym = XChaCha20Poly1305::new_from_slice(&sym_key).map_err(|err| {
                    error!(%err, "Invalid symmetric key length after RSA decryption");
                    VrfKeyCreationError
                })?;
                let vrf_key = sym
                    .decrypt(&nonce, &self.sym_enc_vrf_key[..])
                    .map_err(|err| {
                        error!(%err, "Failed to decrypt VRF key with symmetric key");
                        VrfKeyCreationError
                    })?;

                vrf_key
            }
        };

        Ok(VrfKey(vrf_key))
    }
}

#[cfg(test)]
mod tests {

    use std::str::FromStr;

    use chacha20poly1305::{KeyInit, XChaCha20Poly1305};
    use rsa::{
        pkcs1::{DecodeRsaPrivateKey, EncodeRsaPrivateKey},
        Pkcs1v15Encrypt,
    };

    // This is a sample RSA private key for testing purposes only.
    // Please do not flag this as key leakage or use this key in
    // any production system.
    const TEST_RSA_PRIVATE_KEY: &str = r"-----BEGIN RSA PRIVATE KEY-----
MIICXAIBAAKBgQCaPQBvavQC8o/A0map70QTqGz6ETMURzHaWIEjlS89ytjj+8Zs
K9L1HCy9SOShFcSYrGb47CdMhMKHa/1YRUVA653uO4rqlO+wPhOZEzljvp9zXvDz
ybLjF2aGZg61w1rC25l36M0NUx8HN+Ws+14mcVzllUiXbk9PMXhWFKoj2wIDAQAB
AoGAU61Sph/NQCgea0r6nakMMuoGLWjVYGP7nOy1KvvNxGVfY9h9XsQr0AS4FP0N
5IKtxPKLbvKXo4DHFLc2nAQAvI8kUPZM40jyVk2yUr2k48PMkssdQKXJ/qRi6PeI
LLLSh7IHDYWdVL7pHA1a7ghH+DIATkA83/++QON1btyKSNECQQDMkKZhqjP2OAbW
5xYrmJp3Q2TlXRjwuOdZLD8uXHl15vAxGokkawxkVlW5vI99tdnqS6Kp5U0THP6H
jc+Hii85AkEAwQTxM1Nr3McluiS5kXs8FjdlgUJ+zRAZWOHQqEazQXDlXFVODHFO
+Rh2sX9eqFUc07sJyjV1xLoN5Fe8DjUXswJABy91iKyv0pA5PUc0sidUFahaXOwe
OiZkie9R8NDyuz93ZGIoOw0/jC60KCgFakb+9ondltYlFOzJy/0hMwOZkQJAc+rB
5+8LcfVvZNC1WPdHaJgwL2Z9vC0U69oBc22yLXTdaYwZaUOLB/F3JrW1ZSZoP4eu
I2/joBeUTDOcTnP4HQJBAICmnHCopJ1sSfQG3fMDobOStJBvxQwLkGeRGzI2XsMw
k7UXX8Wh7AgrK4A/MuZXJL30Cd/dgtlHzJWtlQevTII=
-----END RSA PRIVATE KEY-----";

    // This is a sample key for testing purposes only.
    // Please do not flag this as key leakage or use this key in
    // any production system.
    const TEST_SYMMETRIC_KEY_B64: &str = "4AD95tg8tfveioyS/E2jAQw06FDTUCu+VSEZxa41wuM=";

    fn create_test_symmetric_config() -> super::VrfKeyConfig {
        super::VrfKeyConfig::B64EncodedSymmetricKey {
            key: TEST_SYMMETRIC_KEY_B64.to_string(),
        }
    }

    fn create_test_rsa_config() -> super::VrfKeyConfig {
        super::VrfKeyConfig::PEMEncodedRSAKey {
            private_key: TEST_RSA_PRIVATE_KEY.to_string(),
        }
    }

    fn generate_random_symmetric_key_b64() -> String {
        let key = XChaCha20Poly1305::generate_key(rand::thread_rng());
        bitwarden_encoding::B64::from(key.to_vec()).to_string()
    }

    fn generate_random_rsa_key_pem() -> String {
        let rsa_private_key = rsa::RsaPrivateKey::new(&mut rand::thread_rng(), 1024).unwrap();
        rsa_private_key
            .to_pkcs1_pem(Default::default())
            .unwrap()
            .to_string()
    }

    #[tokio::test]
    pub async fn test_generation_from_symmetric_key() {
        let config = create_test_symmetric_config();
        let (table_data, vrf_key) = super::VrfKeyTableData::new(config.clone()).await.unwrap();
        let retrieved_vrf_key = table_data.to_vrf_key(config).await.unwrap();

        assert_eq!(table_data.enc_sym_key, None);
        assert_eq!(
            table_data.root_key_hash,
            [
                130, 153, 58, 122, 202, 166, 92, 56, 249, 28, 57, 171, 206, 187, 12, 81, 44, 166,
                61, 41, 188, 84, 20, 43, 108, 211, 146, 152, 243, 155, 49, 66
            ]
        );

        assert_eq!(vrf_key.0, retrieved_vrf_key.0);
    }

    #[tokio::test]
    pub async fn test_generation_from_rsa_key() {
        let rsa_private_key = rsa::RsaPrivateKey::from_pkcs1_pem(TEST_RSA_PRIVATE_KEY).unwrap();
        let config = create_test_rsa_config();
        let (table_data, vrf_key) = super::VrfKeyTableData::new(config.clone()).await.unwrap();
        let retrieved_vrf_key = table_data.to_vrf_key(config).await.unwrap();
        assert_eq!(
            table_data.root_key_hash,
            [
                124, 52, 131, 164, 108, 28, 127, 165, 58, 31, 40, 199, 182, 120, 247, 152, 191,
                169, 215, 215, 230, 71, 154, 182, 30, 62, 209, 234, 2, 112, 150, 128
            ]
        );

        assert!(table_data.enc_sym_key.is_some());
        let _ = rsa_private_key
            .decrypt(Pkcs1v15Encrypt, table_data.enc_sym_key.as_ref().unwrap())
            .unwrap();

        assert_eq!(vrf_key.0, retrieved_vrf_key.0);
    }

    #[tokio::test]
    pub async fn test_generation_from_constant_key() {
        let config = super::VrfKeyConfig::ConstantVrfKey;
        let (table_data, vrf_key) = super::VrfKeyTableData::new(config.clone()).await.unwrap();
        let retrieved_vrf_key = table_data.to_vrf_key(config).await.unwrap();

        assert_eq!(table_data.root_key_hash, vec![]);
        assert_eq!(table_data.enc_sym_key, None);
        assert_eq!(table_data.sym_enc_vrf_key, vec![]);
        assert_eq!(table_data.sym_enc_vrf_key_nonce, vec![]);
        assert_eq!(vrf_key.0, retrieved_vrf_key.0);
        assert!(!vrf_key.0.is_empty());
    }

    #[tokio::test]
    pub async fn test_invalid_base64_symmetric_key() {
        let config = super::VrfKeyConfig::B64EncodedSymmetricKey {
            key: "not!valid@base64#".to_string(),
        };

        let result = super::VrfKeyTableData::new(config.clone()).await;
        assert!(matches!(result, Err(super::VrfKeyCreationError)));
    }

    #[tokio::test]
    pub async fn test_invalid_base64_during_retrieval() {
        let config_valid = create_test_symmetric_config();
        let (table_data, _) = super::VrfKeyTableData::new(config_valid).await.unwrap();

        let config_invalid = super::VrfKeyConfig::B64EncodedSymmetricKey {
            key: "not!valid@base64#".to_string(),
        };
        let result = table_data.to_vrf_key(config_invalid).await;
        assert!(matches!(result, Err(super::VrfKeyCreationError)));
    }

    #[tokio::test]
    pub async fn test_invalid_symmetric_key_length() {
        let short_key = bitwarden_encoding::B64::from(vec![0u8; 16]).to_string();
        let config = super::VrfKeyConfig::B64EncodedSymmetricKey { key: short_key };

        let result = super::VrfKeyTableData::new(config).await;
        assert!(matches!(result, Err(super::VrfKeyCreationError)));
    }

    #[tokio::test]
    pub async fn test_invalid_rsa_pem_format_malformed() {
        let malformed_pem =
            "-----BEGIN RSA PRIVATE KEY-----\nINVALID\n-----END RSA PRIVATE KEY-----";
        let config = super::VrfKeyConfig::PEMEncodedRSAKey {
            private_key: malformed_pem.to_string(),
        };

        let result = super::VrfKeyTableData::new(config).await;
        assert!(matches!(result, Err(super::VrfKeyCreationError)));
    }

    #[tokio::test]
    pub async fn test_invalid_rsa_pem_format_missing_headers() {
        let missing_headers = TEST_RSA_PRIVATE_KEY
            .replace("-----BEGIN RSA PRIVATE KEY-----\n", "")
            .replace("-----END RSA PRIVATE KEY-----", "");
        let config = super::VrfKeyConfig::PEMEncodedRSAKey {
            private_key: missing_headers.to_string(),
        };

        let result = super::VrfKeyTableData::new(config).await;
        assert!(matches!(result, Err(super::VrfKeyCreationError)));
    }

    #[tokio::test]
    pub async fn test_wrong_symmetric_key_decryption() {
        let config1 = create_test_symmetric_config();
        let (table_data, _) = super::VrfKeyTableData::new(config1).await.unwrap();

        let config2 = super::VrfKeyConfig::B64EncodedSymmetricKey {
            key: generate_random_symmetric_key_b64(),
        };

        let result = table_data.to_vrf_key(config2).await;
        assert!(matches!(result, Err(super::VrfKeyCreationError)));
    }

    #[tokio::test]
    pub async fn test_wrong_rsa_key_decryption() {
        let config1 = create_test_rsa_config();
        let (table_data, _) = super::VrfKeyTableData::new(config1).await.unwrap();

        let config2 = super::VrfKeyConfig::PEMEncodedRSAKey {
            private_key: generate_random_rsa_key_pem(),
        };

        let result = table_data.to_vrf_key(config2).await;
        assert!(result.is_err());
    }

    #[tokio::test]
    pub async fn test_rsa_missing_enc_sym_key() {
        let config = create_test_rsa_config();
        let (mut table_data, _) = super::VrfKeyTableData::new(config.clone()).await.unwrap();

        table_data.enc_sym_key = None;

        let result = table_data.to_vrf_key(config).await;
        assert!(matches!(result, Err(super::VrfKeyCreationError)));
    }

    #[tokio::test]
    pub async fn test_wrong_nonce() {
        let config = create_test_symmetric_config();
        let (mut table_data, _) = super::VrfKeyTableData::new(config.clone()).await.unwrap();

        table_data.sym_enc_vrf_key_nonce.truncate(10);

        let result = table_data.to_vrf_key(config).await;
        assert!(result.is_err());
    }

    #[tokio::test]
    pub async fn test_nonce_size_validation() {
        let config = create_test_symmetric_config();
        let (table_data, _) = super::VrfKeyTableData::new(config).await.unwrap();

        assert_eq!(table_data.sym_enc_vrf_key_nonce.len(), 24);
    }

    #[tokio::test]
    pub async fn test_empty_symmetric_key() {
        let config = super::VrfKeyConfig::B64EncodedSymmetricKey { key: String::new() };

        let result = super::VrfKeyTableData::new(config).await;
        assert!(result.is_err());
    }

    #[tokio::test]
    pub async fn test_empty_rsa_key() {
        let config = super::VrfKeyConfig::PEMEncodedRSAKey {
            private_key: String::new(),
        };

        let result = super::VrfKeyTableData::new(config).await;
        assert!(matches!(result, Err(super::VrfKeyCreationError)));
    }

    #[tokio::test]
    pub async fn test_vrf_key_randomness() {
        let config1 = create_test_symmetric_config();
        let config2 = create_test_symmetric_config();

        let (table_data1, vrf_key1) = super::VrfKeyTableData::new(config1).await.unwrap();
        let (table_data2, vrf_key2) = super::VrfKeyTableData::new(config2).await.unwrap();

        assert_ne!(vrf_key1.0, vrf_key2.0);
        assert_ne!(
            table_data1.sym_enc_vrf_key_nonce,
            table_data2.sym_enc_vrf_key_nonce
        );
    }

    #[tokio::test]
    pub async fn test_symmetric_key_not_persisted() {
        let config = create_test_symmetric_config();
        let (table_data, _) = super::VrfKeyTableData::new(config).await.unwrap();

        let symmetric_key_bytes =
            bitwarden_encoding::B64::from_str(TEST_SYMMETRIC_KEY_B64).unwrap();

        assert!(!table_data
            .sym_enc_vrf_key
            .contains(&symmetric_key_bytes.as_bytes()[0]));
        assert_eq!(table_data.enc_sym_key, None);
    }

    #[tokio::test]
    pub async fn test_rsa_private_key_not_persisted() {
        let config = create_test_rsa_config();
        let (table_data, _) = super::VrfKeyTableData::new(config).await.unwrap();

        let rsa_key = rsa::RsaPrivateKey::from_pkcs1_pem(TEST_RSA_PRIVATE_KEY).unwrap();
        let rsa_der = rsa_key.to_pkcs1_der().unwrap();

        assert!(!table_data
            .sym_enc_vrf_key
            .windows(4)
            .any(|w| rsa_der.as_bytes().windows(4).any(|rw| w == rw)));

        assert!(table_data.enc_sym_key.is_some());
    }

    #[tokio::test]
    pub async fn test_vrf_key_is_encrypted_at_rest() {
        let config = create_test_symmetric_config();
        let (table_data, vrf_key) = super::VrfKeyTableData::new(config).await.unwrap();

        assert!(!table_data
            .sym_enc_vrf_key
            .windows(8)
            .any(|w| vrf_key.0.windows(8).any(|vw| w == vw)));
    }

    #[tokio::test]
    pub async fn test_symmetric_key_encryption_in_rsa_mode() {
        let config = create_test_rsa_config();
        let (table_data, _) = super::VrfKeyTableData::new(config).await.unwrap();

        let enc_sym_key = table_data.enc_sym_key.as_ref().unwrap();
        let rsa_key = rsa::RsaPrivateKey::from_pkcs1_pem(TEST_RSA_PRIVATE_KEY).unwrap();
        let decrypted_sym_key = rsa_key.decrypt(Pkcs1v15Encrypt, enc_sym_key).unwrap();

        assert_ne!(enc_sym_key, &decrypted_sym_key);
        assert_eq!(decrypted_sym_key.len(), 32);
    }

    #[tokio::test]
    pub async fn test_cannot_decrypt_symmetric_with_rsa_config() {
        let sym_config = create_test_symmetric_config();
        let (table_data, _) = super::VrfKeyTableData::new(sym_config).await.unwrap();

        let rsa_config = create_test_rsa_config();

        let result = table_data.to_vrf_key(rsa_config).await;
        assert!(matches!(result, Err(super::VrfKeyCreationError)));
    }

    #[tokio::test]
    pub async fn test_cannot_decrypt_rsa_with_symmetric_config() {
        let rsa_config = create_test_rsa_config();
        let (table_data, _) = super::VrfKeyTableData::new(rsa_config).await.unwrap();

        let sym_config = create_test_symmetric_config();

        let result = table_data.to_vrf_key(sym_config).await;
        assert!(matches!(result, Err(super::VrfKeyCreationError)));
    }
}
