use std::str::FromStr;

use bitwarden_encoding::NotB64EncodedError;
use chacha20poly1305::{
    aead::{generic_array::GenericArray, Aead},
    AeadCore, KeyInit, XChaCha20Poly1305,
};
use rsa::{
    pkcs1::{DecodeRsaPrivateKey, EncodeRsaPrivateKey},
    signature::digest::crypto_common,
    Pkcs1v15Encrypt,
};
use thiserror::Error;

use crate::vrf_key_config::VrfKeyConfig;

/// Represents a storage-layer error
#[derive(Debug, Error)]
pub enum VrfKeyStorageError {
    /// No VRF key exists for the given root key
    #[error("VRF key not found for the specified root key")]
    KeyNotFound,
    /// A VRF key already exists, but for a different root key
    #[error("A VRF key already exists for a different root key")]
    KeyExistsForDifferentRootKey,
    /// A transaction error
    #[error("Database transaction failed: {0}")]
    Transaction(String),
    /// Some kind of storage connection error occurred
    #[error("Storage connection error: {0}")]
    Connection(String),
    /// Base64 decoding error
    #[error("Failed to decode base64 data: {0}")]
    B64DecodingError(#[from] NotB64EncodedError),
    /// ChaCha20Poly1305 length error
    #[error("Invalid key length error: {0}")]
    KeyLengthError(#[from] crypto_common::InvalidLength),
    /// Symmetric encryption/decryption error
    #[error("Symmetric encryption/decryption error")]
    SymmetricEncryptionError,
    /// RSA error
    #[error("RSA key encoding error: {0}")]
    RsaKeyEncodingError(#[from] rsa::pkcs1::Error),
    #[error("RSA encryption/decryption error: {0}")]
    RsaEncryptionError(#[from] rsa::Error),
    /// Some other storage-layer error occurred
    #[error("Storage error: {0}")]
    Other(&'static str),
}

#[allow(unused)]
trait VrfKeyTable {
    async fn get_vrf_key(root_key: &[u8]) -> Result<VrfKeyTableData, VrfKeyStorageError>;
    async fn store_vrf_key(root_key: &[u8]) -> Result<(), VrfKeyStorageError>;
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
    pub async fn new(config: VrfKeyConfig) -> Result<(Self, VrfKey), VrfKeyStorageError> {
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
            let raw_key = bitwarden_encoding::B64::from_str(key)?;
            (
                XChaCha20Poly1305::new_from_slice(raw_key.as_bytes())?,
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
        let sym_enc_vrf_key = sym
            .encrypt(&nonce, &vrf_key[..])
            .map_err(|_| VrfKeyStorageError::SymmetricEncryptionError)?;

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
                let rsa_private_key = rsa::RsaPrivateKey::from_pkcs1_pem(&private_key)?;
                let root_key_hash = blake3::hash(rsa_private_key.to_pkcs1_der()?.as_bytes())
                    .as_bytes()
                    .to_vec();
                let rsa_public_key = rsa_private_key.to_public_key();
                let enc_sym_key =
                    rsa_public_key.encrypt(&mut rand::thread_rng(), Pkcs1v15Encrypt, &sym_key)?;

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

    pub async fn to_vrf_key(&self, config: VrfKeyConfig) -> Result<VrfKey, VrfKeyStorageError> {
        // handle constant key case separately to avoid unnecessary key generation / parsing
        #[cfg(test)]
        if let VrfKeyConfig::ConstantVrfKey = config {
            use akd::ecvrf::{HardCodedAkdVRF, VRFKeyStorage};

            return Ok(VrfKey(
                (HardCodedAkdVRF {}).retrieve().await.unwrap_or_default(),
            ));
        }

        let nonce = GenericArray::from_slice(self.sym_enc_vrf_key_nonce.as_ref());
        let vrf_key = match config {
            #[cfg(test)]
            VrfKeyConfig::ConstantVrfKey => unreachable!(), // handled above
            VrfKeyConfig::B64EncodedSymmetricKey { key } => {
                let raw_key = bitwarden_encoding::B64::from_str(&key)?;
                let sym = XChaCha20Poly1305::new_from_slice(raw_key.as_bytes())?;
                let vrf_key = sym
                    .decrypt(nonce, &self.sym_enc_vrf_key[..])
                    .map_err(|_| VrfKeyStorageError::SymmetricEncryptionError)?;

                vrf_key
            }
            VrfKeyConfig::PEMEncodedRSAKey { private_key } => {
                let rsa_private_key = rsa::RsaPrivateKey::from_pkcs1_pem(&private_key)?;
                let enc_sym_key = self.enc_sym_key.as_ref().ok_or(VrfKeyStorageError::Other(
                    "missing encrypted symmetric key for RSA root key",
                ))?;
                let sym_key = rsa_private_key.decrypt(Pkcs1v15Encrypt, enc_sym_key)?;

                let sym = XChaCha20Poly1305::new_from_slice(&sym_key)?;
                let vrf_key = sym
                    .decrypt(&nonce, &self.sym_enc_vrf_key[..])
                    .map_err(|_| VrfKeyStorageError::SymmetricEncryptionError)?;

                vrf_key
            }
        };

        Ok(VrfKey(vrf_key))
    }
}

#[cfg(test)]
mod tests {

    use rsa::{pkcs1::DecodeRsaPrivateKey, Pkcs1v15Encrypt};

    #[tokio::test]
    pub async fn test_generation_from_symmetric_key() {
        // This is a sample key for testing purposes only.
        // Please do not flag this as key leakage or use this key in
        // any production system.
        let symmetric_key_b64 = "4AD95tg8tfveioyS/E2jAQw06FDTUCu+VSEZxa41wuM=";
        let config = super::VrfKeyConfig::B64EncodedSymmetricKey {
            key: symmetric_key_b64.to_string(),
        };
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
        // This is a sample RSA private key for testing purposes only.
        // Please do not flag this as key leakage or use this key in
        // any production system.
        let rsa_private_key_pem = r"-----BEGIN RSA PRIVATE KEY-----
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
        let rsa_private_key = rsa::RsaPrivateKey::from_pkcs1_pem(rsa_private_key_pem).unwrap();
        let config = super::VrfKeyConfig::PEMEncodedRSAKey {
            private_key: rsa_private_key_pem.to_string(),
        };
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
}
