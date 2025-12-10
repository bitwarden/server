use serde::{Deserialize, Serialize};

use crate::config::ConfigError;
use crate::VrfStorageType;

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(tag = "type")]
pub enum VrfConfig {
    HardCodedAkdVRF,
    ConstantConfigurableVRF { key_material: String },
}

impl TryFrom<&VrfConfig> for VrfStorageType {
    type Error = ConfigError;
    fn try_from(config: &VrfConfig) -> Result<Self, ConfigError> {
        match config {
            VrfConfig::HardCodedAkdVRF => Ok(VrfStorageType::HardCodedAkdVRF),
            VrfConfig::ConstantConfigurableVRF { key_material } => {
                let key_material =
                    hex::decode(key_material).map_err(ConfigError::InvalidVrfKeyMaterialHex)?;

                if key_material.len() != 32 {
                    return Err(ConfigError::VrfKeyMaterialInvalidLength {
                        actual: key_material.len(),
                    });
                }

                Ok(VrfStorageType::ConstantConfigurableVRF {
                    key_material: key_material.clone(),
                })
            }
        }
    }
}
