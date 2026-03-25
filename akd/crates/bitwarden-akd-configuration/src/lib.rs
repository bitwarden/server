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

mod bitwarden_v1_configuration;

pub use bitwarden_v1_configuration::BitwardenV1Configuration;
use serde::{Deserialize, Serialize};
use uuid::Uuid;

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(tag = "type")]
pub enum BitwardenAkdPairMaterial {
    UserRealWorldId {
        real_world_id: String,
        user_id: Uuid,
    },
}

impl BitwardenAkdPairMaterial {
    fn akd_label(&self) -> AkdLabel {
        match self {
            BitwardenAkdPairMaterial::UserRealWorldId {
                real_world_id,
                user_id: _,
            } => (&BitwardenAkdLabelMaterial::UserRealWorldId {
                real_world_id: real_world_id.clone(),
            })
                .into(),
        }
    }

    fn akd_value(&self) -> AkdValue {
        let bytes = match self {
            BitwardenAkdPairMaterial::UserRealWorldId {
                real_world_id: _,
                user_id,
            } => user_id.as_bytes().to_vec(),
        };
        AkdValue(bytes)
    }
}

impl From<&BitwardenAkdPairMaterial> for AkdLabel {
    fn from(pair: &BitwardenAkdPairMaterial) -> Self {
        pair.akd_label()
    }
}

impl From<&BitwardenAkdPairMaterial> for AkdValue {
    fn from(pair: &BitwardenAkdPairMaterial) -> Self {
        pair.akd_value()
    }
}

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(tag = "type")]
pub enum BitwardenAkdLabelMaterial {
    UserRealWorldId { real_world_id: String },
}

impl From<&BitwardenAkdLabelMaterial> for AkdLabel {
    fn from(key: &BitwardenAkdLabelMaterial) -> Self {
        let bytes = match key {
            BitwardenAkdLabelMaterial::UserRealWorldId { real_world_id } => {
                format!("User:RwId:{real_world_id}").as_bytes().to_vec()
            }
        };
        AkdLabel(bytes)
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_bitwarden_akd_pair_material_from_json() {
        let json = r#"{
            "type": "UserRealWorldId",
            "real_world_id": "user@example.com",
            "user_id": "550e8400-e29b-41d4-a716-446655440000"
        }"#;

        let pair: BitwardenAkdPairMaterial = serde_json::from_str(json).unwrap();

        match pair {
            BitwardenAkdPairMaterial::UserRealWorldId {
                real_world_id,
                user_id,
            } => {
                assert_eq!(real_world_id, "user@example.com");
                assert_eq!(
                    user_id,
                    Uuid::parse_str("550e8400-e29b-41d4-a716-446655440000").unwrap()
                );
            }
        }
    }

    #[test]
    fn test_bitwarden_akd_label_material_from_json() {
        let json = r#"{
            "type": "UserRealWorldId",
            "real_world_id": "user@example.com"
        }"#;

        let label: BitwardenAkdLabelMaterial = serde_json::from_str(json).unwrap();

        match label {
            BitwardenAkdLabelMaterial::UserRealWorldId { real_world_id } => {
                assert_eq!(real_world_id, "user@example.com");
            }
        }
    }
}
