use akd::ecvrf::{HardCodedAkdVRF, VRFKeyStorage, VrfError};
use async_trait::async_trait;
use serde::{Deserialize};

#[derive(Debug, Clone, Deserialize)]
pub enum VrfConfig {
    HardCodedAkdVRF,
}

impl From<&VrfConfig> for VrfStorageType {
    fn from(config: &VrfConfig) -> Self {
        match config {
            VrfConfig::HardCodedAkdVRF => VrfStorageType::HardCodedAkdVRF,
        }
    }
}

#[derive(Debug, Clone)]
pub enum VrfStorageType {
    /// **WARNING**: Do not use this in production systems. This is only for testing and debugging.
    /// This is a version of VRFKeyStorage for testing purposes, which uses the example from the VRF crate.
    ///
    /// const KEY_MATERIAL: &str = "c9afa9d845ba75166b5c215767b1d6934e50c3db36e89b127b8a622b120f6721";
    HardCodedAkdVRF,
}

#[async_trait]
impl VRFKeyStorage for VrfStorageType {
    async fn retrieve(&self) -> Result<Vec<u8>, VrfError> {
        match self {
            VrfStorageType::HardCodedAkdVRF => HardCodedAkdVRF.retrieve().await,
        }
    }
}
