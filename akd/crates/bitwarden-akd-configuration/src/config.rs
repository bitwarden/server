use serde::{Deserialize, Serialize};

use crate::INSTALLATION_CONTEXT;

#[derive(Debug, Serialize, Deserialize, Clone)]
pub struct BitwardenAkdConfiguration {
    pub installation_id: uuid::Uuid,
}

impl BitwardenAkdConfiguration {
    /// Initialize the global installation context for Bitwarden AKD.
    /// Must be called once before any use.
    ///
    /// # Errors
    /// Returns an error like [`OnceLock<Vec<u8>>`](std::sync::OnceLock) if called more than once.
    pub fn init(&self) -> Result<(), Vec<u8>> {
        INSTALLATION_CONTEXT.set(self.installation_id.into_bytes().into())
    }
}
