use std::time::Duration;

use akd::{directory::ReadOnlyDirectory, storage::StorageManager, Directory};
use serde::Deserialize;
use thiserror::Error;
use tracing::{error, instrument};

use crate::{
    db_config::DbConfig, publish_queue_config::PublishQueueConfig, vrf_key_config::VrfKeyConfig,
    AkdDatabase, PublishQueueType, ReadOnlyPublishQueueType, VrfKeyDatabase,
};

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
    pub publish_queue_config: PublishQueueConfig,
}

#[derive(Debug, Error)]
#[error("Invalid AkdStorageConfig")]
pub struct AkdStorageConfigError;

#[derive(Debug, Error)]
#[error("Failed to initialize storage")]
pub struct AkdStorageInitializationError;

impl AkdStorageConfig {
    pub fn validate(&self) -> Result<(), AkdStorageConfigError> {
        self.db_config
            .validate()
            .map_err(|_| AkdStorageConfigError)?;
        Ok(())
    }

    #[instrument(skip(self), level = "info")]
    pub async fn initialize_directory<TDirectoryConfig: akd::Configuration>(
        &self,
    ) -> Result<
        (
            Directory<TDirectoryConfig, AkdDatabase, VrfKeyDatabase>,
            AkdDatabase,
            PublishQueueType,
        ),
        AkdStorageInitializationError,
    > {
        let (storage_manager, db) = self.initialize_storage().await?;

        let vrf_storage = db.vrf_key_database().await.map_err(|err| {
            error!(%err, "Failed to initialize VRF key database");
            AkdStorageInitializationError
        })?;

        let publish_queue = PublishQueueType::new(&self.publish_queue_config, &db);

        let directory = Directory::new(storage_manager, vrf_storage)
            .await
            .map_err(|err| {
                error!(%err, "Failed to initialize Directory");
                AkdStorageInitializationError
            })?;

        Ok((directory, db, publish_queue))
    }

    pub async fn initialize_readonly_directory<TDirectoryConfig: akd::Configuration>(
        &self,
    ) -> Result<
        (
            ReadOnlyDirectory<TDirectoryConfig, AkdDatabase, VrfKeyDatabase>,
            AkdDatabase,
            ReadOnlyPublishQueueType,
        ),
        AkdStorageInitializationError,
    > {
        let (storage_manager, db) = self.initialize_storage().await?;

        let vrf_storage = db.vrf_key_database().await.map_err(|err| {
            error!(%err, "Failed to initialize VRF key database");
            AkdStorageInitializationError
        })?;

        let publish_queue = ReadOnlyPublishQueueType::new(&self.publish_queue_config, &db);

        let directory = ReadOnlyDirectory::new(storage_manager, vrf_storage)
            .await
            .map_err(|err| {
                error!(%err, "Failed to initialize ReadOnlyDirectory");
                AkdStorageInitializationError
            })?;

        Ok((directory, db, publish_queue))
    }

    async fn initialize_storage(
        &self,
    ) -> Result<(StorageManager<AkdDatabase>, AkdDatabase), AkdStorageInitializationError> {
        let db = self.db_config.connect().await.map_err(|err| {
            error!(%err, "Failed to connect to database");
            AkdStorageInitializationError
        })?;

        let db = AkdDatabase::new(db, self.vrf_key_config.clone());

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
                db.clone(),
                cache_item_lifetime,
                self.cache_limit_bytes,
                cache_clean_frequency,
            ),
            db,
        ))
    }
}

fn default_cache_item_lifetime_ms() -> usize {
    30_000
}

fn default_cache_clean_ms() -> usize {
    15_000
}
