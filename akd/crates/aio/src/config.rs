use akd_storage::akd_storage_config::AkdStorageConfig;
use config::{Config, ConfigError, Environment, File};
use serde::Deserialize;
use uuid::Uuid;

const DEFAULT_EPOCH_DURATION_MS: u64 = 30000; // 30 seconds
const DEFAULT_MAX_BATCH_LOOKUP_SIZE: usize = 10;
const DEFAULT_AZKS_POLL_INTERVAL_MS: u64 = 100;

/// Application configuration for the AIO (All-in-One) AKD service
#[derive(Clone, Debug, Deserialize)]
pub struct ApplicationConfig {
    pub storage: AkdStorageConfig,
    /// The unique Bitwarden installation ID using this AKD instance.
    /// This value is used to namespace AKD data to a given installation.
    pub installation_id: Uuid,
    #[serde(default)]
    pub publisher: PublisherSettings,
    #[serde(default)]
    pub reader: ReaderSettings,
}

/// Configuration for the Publisher service
#[derive(Clone, Debug, Deserialize)]
pub struct PublisherSettings {
    /// The duration of each publishing epoch in milliseconds. Defaults to 30 seconds.
    #[serde(default = "default_epoch_duration_ms")]
    pub epoch_duration_ms: u64,
    /// The limit to the number of AKD values to update in a single epoch. Defaults to no limit.
    #[serde(default)]
    pub epoch_update_limit: Option<isize>,
    /// The address the publisher web server will bind to. Defaults to "127.0.0.1:3000".
    #[serde(default = "default_publisher_web_server_bind_address")]
    pub web_server_bind_address: String,
    /// The API key required to access the publisher web server endpoints.
    ///
    /// NOTE: constant-time comparison is used, but mismatched string length cause immediate failure.
    /// For this reason, timing attacks can be used to at least determine the valid key length and a
    /// sufficiently long key should be used to mitigate this risk.
    pub web_server_api_key: String,
}

/// Configuration for the Reader service
#[derive(Clone, Debug, Deserialize)]
pub struct ReaderSettings {
    /// The address the reader web server will bind to. Defaults to "127.0.0.1:3001".
    #[serde(default = "default_reader_web_server_bind_address")]
    pub web_server_bind_address: String,
    /// Maximum number of labels allowed in a single batch lookup request. Defaults to 10.
    #[serde(default = "default_max_batch_lookup_size")]
    pub max_batch_lookup_size: usize,
    /// Polling interval for AZKS storage in milliseconds. Should be significantly less than the epoch interval. Defaults to 100 ms.
    #[serde(default = "default_azks_poll_interval_ms")]
    pub azks_poll_interval_ms: u64,
}

fn default_epoch_duration_ms() -> u64 {
    DEFAULT_EPOCH_DURATION_MS
}

fn default_publisher_web_server_bind_address() -> String {
    "127.0.0.1:3000".to_string()
}

fn default_reader_web_server_bind_address() -> String {
    "127.0.0.1:3001".to_string()
}

fn default_max_batch_lookup_size() -> usize {
    DEFAULT_MAX_BATCH_LOOKUP_SIZE
}

fn default_azks_poll_interval_ms() -> u64 {
    DEFAULT_AZKS_POLL_INTERVAL_MS
}

impl Default for PublisherSettings {
    fn default() -> Self {
        PublisherSettings {
            epoch_duration_ms: default_epoch_duration_ms(),
            epoch_update_limit: None,
            web_server_bind_address: default_publisher_web_server_bind_address(),
            web_server_api_key: String::new(),
        }
    }
}

impl Default for ReaderSettings {
    fn default() -> Self {
        ReaderSettings {
            web_server_bind_address: default_reader_web_server_bind_address(),
            max_batch_lookup_size: default_max_batch_lookup_size(),
            azks_poll_interval_ms: default_azks_poll_interval_ms(),
        }
    }
}

impl ApplicationConfig {
    /// Load configuration from multiple sources in order of priority:
    /// 1. Environment variables (prefixed with AKD_AIO) - always applied with highest priority
    /// 2. Configuration file from AKD_AIO_CONFIG_PATH environment variable (if set)
    /// 3. OR default configuration file (config.toml, config.yaml, config.json) in working directory
    ///
    /// Environment variable naming:
    /// - Uses double underscore (__) as separator
    /// - For field `installation_id`, use `AKD_AIO__INSTALLATION_ID`
    /// - For nested fields like `storage.cache_clean_ms`, use `AKD_AIO__STORAGE__CACHE_CLEAN_MS`
    /// - For publisher fields like `publisher.epoch_duration_ms`, use `AKD_AIO__PUBLISHER__EPOCH_DURATION_MS`
    ///
    /// Note: Only one config file source is used - either custom path OR default location
    pub fn load() -> Result<Self, ConfigError> {
        let mut builder = Config::builder();

        // Check for custom config path via environment variable
        if let Ok(config_path) = std::env::var("AKD_AIO_CONFIG_PATH") {
            builder = builder.add_source(File::with_name(&config_path).required(true));
        } else {
            // Fall back to default config file locations
            builder = builder.add_source(File::with_name("config").required(false));
        }

        let config = builder
            // Add environment variables with prefix "AKD_AIO_"
            .add_source(Environment::with_prefix("AKD_AIO").separator("__"))
            .build()?;

        let aio_config: Self = config.try_deserialize()?;

        aio_config.validate()?;

        Ok(aio_config)
    }

    pub fn validate(&self) -> Result<(), ConfigError> {
        self.storage
            .validate()
            .map_err(|e| ConfigError::Message(format!("{e}")))?;
        self.publisher.validate()?;
        self.reader.validate()?;
        Ok(())
    }
}

impl PublisherSettings {
    pub fn validate(&self) -> Result<(), ConfigError> {
        if self.epoch_duration_ms == 0 {
            return Err(ConfigError::Message(
                "epoch_duration_ms must be greater than 0".to_string(),
            ));
        }
        if self.web_server_api_key.is_empty() {
            return Err(ConfigError::Message(
                "web_server_api_key is required".to_string(),
            ));
        }
        Ok(())
    }
}

impl ReaderSettings {
    pub fn validate(&self) -> Result<(), ConfigError> {
        if self.max_batch_lookup_size == 0 {
            return Err(ConfigError::Message(
                "max_batch_lookup_size must be greater than 0".to_string(),
            ));
        }
        if self.azks_poll_interval_ms == 0 {
            return Err(ConfigError::Message(
                "azks_poll_interval_ms must be greater than 0".to_string(),
            ));
        }
        Ok(())
    }
}

impl From<&ApplicationConfig> for publisher::ApplicationConfig {
    fn from(config: &ApplicationConfig) -> Self {
        publisher::ApplicationConfig {
            storage: config.storage.clone(),
            publisher: publisher::PublisherConfig {
                epoch_duration_ms: config.publisher.epoch_duration_ms,
                epoch_update_limit: config.publisher.epoch_update_limit,
            },
            installation_id: config.installation_id,
            web_server_bind_address: config.publisher.web_server_bind_address.clone(),
            web_server_api_key: config.publisher.web_server_api_key.clone(),
        }
    }
}

impl From<&ApplicationConfig> for reader::ApplicationConfig {
    fn from(config: &ApplicationConfig) -> Self {
        reader::ApplicationConfig {
            storage: config.storage.clone(),
            web_server_bind_address: config.reader.web_server_bind_address.clone(),
            installation_id: config.installation_id,
            max_batch_lookup_size: config.reader.max_batch_lookup_size,
            azks_poll_interval_ms: config.reader.azks_poll_interval_ms,
            expected_epoch_duration_ms: config.publisher.epoch_duration_ms,
        }
    }
}
