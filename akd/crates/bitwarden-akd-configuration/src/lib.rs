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
use uuid::Uuid;

#[derive(Debug, Clone)]
pub enum BitwardenAkdPair {
    UserRealWorldId {
        real_world_id: String,
        user_id: Uuid,
    },
}

impl BitwardenAkdPair {
    fn akd_label(&self) -> AkdLabel {
        let bytes = match self {
            BitwardenAkdPair::UserRealWorldId {
                real_world_id,
                user_id: _,
            } => format!("User:RwId:{real_world_id}").as_bytes().to_vec(),
        };
        AkdLabel(bytes)
    }

    fn akd_value(&self) -> AkdValue {
        let bytes = match self {
            BitwardenAkdPair::UserRealWorldId {
                real_world_id: _,
                user_id,
            } => user_id.as_bytes().to_vec(),
        };
        AkdValue(bytes)
    }
}

impl From<BitwardenAkdPair> for AkdPair {
    fn from(pair: BitwardenAkdPair) -> Self {
        AkdPair {
            label: pair.akd_label(),
            value: pair.akd_value(),
        }
    }
}

pub struct AkdPair {
    label: AkdLabel,
    value: AkdValue,
}

pub fn akd_label_value_for(pair: BitwardenAkdPair) -> AkdPair {
    pair.into()
}
