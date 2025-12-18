use thiserror::Error;

mod storage_manager_config;

pub use akd_storage::vrf_key_config::VrfKeyConfig;
pub use storage_manager_config::*;

#[derive(Error, Debug)]
pub enum ConfigError {
    #[error("Failed to connect to database")]
    DatabaseConnection(#[source] akd::errors::StorageError),

    #[error("Configuration value 'cache_item_lifetime_ms' is invalid: value {value} exceeds maximum allowed ({max})")]
    CacheLifetimeOutOfRange {
        value: usize,
        max: u64,
        #[source]
        source: std::num::TryFromIntError,
    },

    #[error("Configuration value 'cache_clean_frequency_ms' is invalid: value {value} exceeds maximum allowed ({max})")]
    CacheCleanFrequencyOutOfRange {
        value: usize,
        max: u64,
        #[source]
        source: std::num::TryFromIntError,
    },

    #[error("{0}")]
    Custom(String),

    #[error("Invalid hex string for VRF key material")]
    InvalidVrfKeyMaterialHex(#[source] hex::FromHexError),

    #[error("VRF key material must be exactly 32 bytes, got {actual} bytes")]
    VrfKeyMaterialInvalidLength { actual: usize },
}

impl ConfigError {
    pub fn new(message: impl Into<String>) -> Self {
        Self::Custom(message.into())
    }
}
