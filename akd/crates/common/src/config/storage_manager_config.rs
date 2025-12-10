use std::time::Duration;

use crate::config::ConfigError;
use akd::storage::StorageManager;
use akd_storage::{db_config::DbConfig, DatabaseType};
use serde::{Deserialize, Serialize};

/// items live for 30s by default
pub const DEFAULT_ITEM_LIFETIME_MS: usize = 30_000;
/// clean the cache every 15s by default
pub const DEFAULT_CACHE_CLEAN_FREQUENCY_MS: usize = 15_000;

#[derive(Debug, Serialize, Deserialize, Clone)]
pub struct StorageManagerConfig {
    pub db_config: DbConfig,
    pub cache_limit_bytes: Option<usize>,
    #[serde(default = "default_cache_item_lifetime_ms")]
    pub cache_item_lifetime_ms: usize,
    #[serde(default = "default_cache_clean_frequency_ms")]
    pub cache_clean_frequency_ms: usize,
}

fn default_cache_item_lifetime_ms() -> usize {
    DEFAULT_ITEM_LIFETIME_MS
}

fn default_cache_clean_frequency_ms() -> usize {
    DEFAULT_CACHE_CLEAN_FREQUENCY_MS
}

impl StorageManagerConfig {
    pub async fn create(&self) -> Result<StorageManager<DatabaseType>, ConfigError> {
        Ok(StorageManager::new(
            self.db_config
                .connect()
                .await
                .map_err(ConfigError::DatabaseConnection)?,
            Some(Duration::from_millis(
                self.cache_item_lifetime_ms.try_into().map_err(|source| {
                    ConfigError::CacheLifetimeOutOfRange {
                        value: self.cache_item_lifetime_ms,
                        max: u64::MAX,
                        source,
                    }
                })?,
            )),
            self.cache_limit_bytes,
            Some(Duration::from_millis(
                self.cache_clean_frequency_ms.try_into().map_err(|source| {
                    ConfigError::CacheCleanFrequencyOutOfRange {
                        value: self.cache_clean_frequency_ms,
                        max: u64::MAX,
                        source,
                    }
                })?,
            )),
        ))
    }
}
