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

use akd::AkdLabel;
use std::fmt::Display;
use thiserror::Error;

#[cfg(feature = "config")]
pub mod config;

mod bitwarden_v1_configuration;

pub use bitwarden_v1_configuration::BitwardenV1Configuration;
use uuid::Uuid;

#[derive(Debug, Copy, Clone)]
pub enum BitwardenAkdEntity {
    User,
    Organization,
}

impl Display for BitwardenAkdEntity {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        match self {
            BitwardenAkdEntity::User => write!(f, "User"),
            BitwardenAkdEntity::Organization => write!(f, "Org"),
        }
    }
}

#[derive(Debug, Copy, Clone)]
pub enum BitwardenAkdLabelType {
    /// The human-readable identifier for the BitwardenAkdEntity
    RealWorldIdentifier,
}

impl Display for BitwardenAkdLabelType {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        match self {
            BitwardenAkdLabelType::RealWorldIdentifier => write!(f, "RwId"),
        }
    }
}

#[derive(Debug, Error)]
#[error("Invalid AKD Label entity{0}/type{1} pair")]
pub struct InvalidAkdLabelError(BitwardenAkdEntity, BitwardenAkdLabelType);

pub fn akd_label_for(
    entity: BitwardenAkdEntity,
    label_type: BitwardenAkdLabelType,
    data: &Uuid,
) -> Result<akd::AkdLabel, InvalidAkdLabelError> {
    // Match the entity and label type to offer an opportunity to
    _ = match (entity, label_type) {
        (BitwardenAkdEntity::User, BitwardenAkdLabelType::RealWorldIdentifier) => Ok(()),
        (BitwardenAkdEntity::Organization, BitwardenAkdLabelType::RealWorldIdentifier) => Ok(()),
    }?;

    let label_string = format!("{entity}:{label_type}:");
    let bytes = [label_string.as_bytes(), data.as_bytes()].concat();
    Ok(AkdLabel(bytes.to_vec()))
}
