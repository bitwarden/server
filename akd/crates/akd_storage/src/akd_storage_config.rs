use std::time::Duration;

use akd::storage::StorageManager;
use serde::Deserialize;
use thiserror::Error;
use tracing::error;

use crate::{db_config::DbConfig, vrf_key_config::VrfKeyConfig, AkdDatabase};

#[derive(Debug, Clone, Deserialize)]
pub struct AkdStorageConfig {
    pub db_config: DbConfig,
    /// Controls how long items stay in cache before being evicted (in milliseconds). Defaults to 30 seconds.
    #[serde(default = "default_cache_item_lifetime_ms")]
    pub cache_item_lifetime_ms: usize,
    /// Controls the maximum size of the cache in bytes. Defaults to no limit.
    #[serde(default)]
    pub cache_limit_bytes: Option<usize>,
    /// Controls how often the cache is cleaned (in milliseconds). Defaults to 15 seconds.
    #[serde(default = "default_cache_clean_ms")]
    pub cache_clean_ms: usize,
    pub vrf_key_config: VrfKeyConfig,
}

#[derive(Debug, Error)]
#[error("Failed to initialize storage")]
pub struct AkdStorageInitializationError;

impl AkdStorageConfig {
    pub async fn initialize_storage(
        &self,
    ) -> Result<(StorageManager<AkdDatabase>, AkdDatabase), AkdStorageInitializationError> {
        let db = self.db_config.connect().await.map_err(|err| {
            error!(%err, "Failed to connect to database");
            AkdStorageInitializationError
        })?;

        let state = AkdDatabase::new(db, self.vrf_key_config.clone());

        let cache_item_lifetime = Some(Duration::from_millis(
            self.cache_item_lifetime_ms.try_into().map_err(|err| {
                error!(%err, "Cache item lifetime out of range");
                AkdStorageInitializationError
            })?,
        ));
        let cache_clean_frequency = Some(Duration::from_millis(
            self.cache_clean_ms.try_into().map_err(|err| {
                error!(%err, "Cache clean interval out of range");
                AkdStorageInitializationError
            })?,
        ));

        Ok((
            StorageManager::new(
                state.clone(),
                cache_item_lifetime,
                self.cache_limit_bytes,
                cache_clean_frequency,
            ),
            state,
        ))
    }
}

fn default_cache_item_lifetime_ms() -> usize {
    30_000
}

fn default_cache_clean_ms() -> usize {
    15_000
}
