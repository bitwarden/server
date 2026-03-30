//! # Bitwarden AKD Configuration
//!
//! This crate provides configuration settings and utilities specific to Bitwarden's
//! use of the AKD (Authenticated Key Directory) system. It includes definitions for
//! Bitwarden-specific AKD configurations, which are namespaced to each Bitwarden
//! installation.
//!
//! It also provides helper functions to generate AkdLabels from Bitwarden
//! Concepts. For example, we need to provide real-world identity binding for multiple
//! Bitwarden entities, such as Users and Organizations. Label helpers ensure these are
//! created consistently across the system.

use akd::{AkdLabel, AkdValue};

#[cfg(feature = "config")]
pub mod config;

#[cfg(feature = "request_models")]
pub mod request_models;

mod bitwarden_v1_configuration;

pub use bitwarden_v1_configuration::BitwardenV1Configuration;
use uuid::Uuid;

#[derive(Debug, Clone)]
pub enum BitwardenAkdPairMaterial {
    UserRealWorldId {
        real_world_id: String,
        user_id: Uuid,
    },
    UserPublicKey {
        user_id: Uuid,
        public_key_der: Vec<u8>,
    },
}

impl BitwardenAkdPairMaterial {
    fn to_label_material(&self) -> BitwardenAkdLabelMaterial {
        match self {
            BitwardenAkdPairMaterial::UserRealWorldId {
                real_world_id,
                user_id: _,
            } => BitwardenAkdLabelMaterial::UserRealWorldId {
                real_world_id: real_world_id.clone(),
            },
            BitwardenAkdPairMaterial::UserPublicKey {
                user_id,
                public_key_der: _,
            } => BitwardenAkdLabelMaterial::UserPublicKey {
                user_id: user_id.clone(),
            },
        }
    }

    fn value_bytes(&self) -> Vec<u8> {
        match self {
            BitwardenAkdPairMaterial::UserRealWorldId {
                real_world_id: _,
                user_id,
            } => user_id.as_bytes().to_vec(),
            BitwardenAkdPairMaterial::UserPublicKey {
                user_id: _,
                public_key_der,
            } => public_key_der.clone(),
        }
    }

    fn akd_label(&self) -> AkdLabel {
        self.to_label_material().into()
    }

    fn akd_value(&self) -> AkdValue {
        AkdValue(self.value_bytes())
    }
}

impl From<BitwardenAkdPairMaterial> for AkdLabel {
    fn from(pair: BitwardenAkdPairMaterial) -> Self {
        (&pair).into()
    }
}

impl From<&BitwardenAkdPairMaterial> for AkdLabel {
    fn from(pair: &BitwardenAkdPairMaterial) -> Self {
        pair.akd_label()
    }
}

impl From<BitwardenAkdPairMaterial> for AkdValue {
    fn from(pair: BitwardenAkdPairMaterial) -> Self {
        (&pair).into()
    }
}

impl From<&BitwardenAkdPairMaterial> for AkdValue {
    fn from(pair: &BitwardenAkdPairMaterial) -> Self {
        pair.akd_value()
    }
}

#[derive(Debug, Clone)]
pub enum BitwardenAkdLabelMaterial {
    UserRealWorldId { real_world_id: String },
    UserPublicKey { user_id: Uuid },
}

impl From<BitwardenAkdLabelMaterial> for AkdLabel {
    fn from(key: BitwardenAkdLabelMaterial) -> Self {
        (&key).into()
    }
}

impl From<&BitwardenAkdLabelMaterial> for AkdLabel {
    fn from(key: &BitwardenAkdLabelMaterial) -> Self {
        let bytes = match key {
            BitwardenAkdLabelMaterial::UserRealWorldId { real_world_id } => {
                user_label("RwId", real_world_id)
            }
            BitwardenAkdLabelMaterial::UserPublicKey { user_id } => user_label("PubKey", user_id),
        };
        AkdLabel(bytes)
    }
}

fn user_label(short_hand: &str, data: &impl std::fmt::Display) -> Vec<u8> {
    format!("User:{short_hand}:{data}").as_bytes().to_vec()
}

#[cfg(test)]
mod tests {
    use std::str::FromStr;

    use bitwarden_encoding::B64;

    use super::*;

    fn public_key() -> Vec<u8> {
        B64::from_str("MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEAwFbeDLUwqpRgUh5P+4GQNwkUotEhl/YNYkBKBBPubzlilbHvaivhfqpl3uljoTMpe6m2bmtTPt8M/gcwn/ngFLEt7uo0/I5/59X7ZqcdZEOAOCVqyNzqf1MsuFDnckK1sfEWs2PsprdzOP50JOneoFezSinT73UtM4ym9gAE9JBGyvZo2p3FvmCyNhXgrF7czxMrJmN7+ageu05xxXSKaBL33aHmXbhLQ+qlZ0sJ19e0NtSECVG9aDGMVhy7zdyHWHSjDZiii/G1PZ0QiBUbzcjNugGfF3S7OG3gGHgkSF8xy10/LUuae2bRjr5Zciio0kuQXUXYqN4Yq4sf4Lc4RwIDAQAB").expect("valid b64").into()
    }

    #[test]
    fn test_user_label_prepends_user_namespace() {
        let r = user_label("test", &"test_data".to_owned());

        assert_eq!(r, "User:test:test_data".as_bytes().to_vec())
    }

    #[test]
    fn test_user_real_world_id_label() {
        let label: AkdLabel = BitwardenAkdLabelMaterial::UserRealWorldId {
            real_world_id: "test@example.com".to_owned(),
        }
        .into();
        let pair: AkdLabel = BitwardenAkdPairMaterial::UserRealWorldId {
            real_world_id: "test@example.com".to_owned(),
            user_id: Uuid::nil(),
        }
        .into();

        let expected = AkdLabel("User:RwId:test@example.com".as_bytes().to_vec());
        assert_eq!(label, expected);
        assert_eq!(pair, expected);
    }

    #[test]
    fn test_user_public_key_label() {
        let label: AkdLabel = BitwardenAkdLabelMaterial::UserPublicKey {
            user_id: Uuid::nil(),
        }
        .into();
        let pair: AkdLabel = BitwardenAkdPairMaterial::UserPublicKey {
            user_id: Uuid::nil(),
            public_key_der: public_key(),
        }
        .into();

        assert_eq!(
            label,
            AkdLabel(format!("User:PubKey:{}", Uuid::nil()).as_bytes().to_vec())
        );
        assert_eq!(
            pair,
            AkdLabel(format!("User:PubKey:{}", Uuid::nil()).as_bytes().to_vec())
        );
    }

    #[test]
    fn test_user_real_world_id_value() {
        let pair: AkdValue = BitwardenAkdPairMaterial::UserRealWorldId {
            real_world_id: "test@example.com".to_owned(),
            user_id: Uuid::nil(),
        }
        .into();

        assert_eq!(pair, AkdValue(Uuid::nil().into()));
    }

    #[test]
    fn test_user_public_key_value() {
        let pair: AkdValue = BitwardenAkdPairMaterial::UserPublicKey {
            user_id: Uuid::nil(),
            public_key_der: public_key(),
        }
        .into();

        assert_eq!(pair, AkdValue(public_key()));
    }
}
