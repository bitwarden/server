use serde::Deserialize;

use crate::db_config::DbConfig;

#[derive(Debug, Clone, Deserialize)]
pub struct AkdStorageConfig {
    db_config: DbConfig,
    /// Controls how long items stay in cache before being evicted (in milliseconds). Defaults to 30 seconds.
    #[serde(default = "default_cache_item_lifetime_ms")]
    cache_item_lifetime_ms: usize,
    /// Controls the maximum size of the cache in bytes. Defaults to no limit.
    #[serde(default)]
    cache_limit_bytes: Option<usize>,
    /// Controls how often the cache is cleaned (in milliseconds). Defaults to 15 seconds.
    #[serde(default = "default_cache_clean_ms")]
    cache_clean_ms: usize,
}

fn default_cache_item_lifetime_ms() -> usize {
    30_000
}

fn default_cache_clean_ms() -> usize {
    15_000
}
