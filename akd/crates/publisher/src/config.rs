use akd_storage::akd_storage_config::AkdStorageConfig;
use config::{Config, ConfigError, Environment, File};
use serde::Deserialize;
use uuid::Uuid;

const DEFAULT_EPOCH_DURATION_MS: u64 = 30000; // 30 seconds

#[derive(Clone, Debug, Deserialize)]
pub struct ApplicationConfig {
    pub storage: AkdStorageConfig,
    pub publisher: PublisherConfig,
    pub installation_id: Uuid,
    // web_server: WebServerConfig,
}

#[derive(Clone, Debug, Deserialize)]
pub struct PublisherConfig {
    #[serde(default = "default_epoch_duration_ms")]
    epoch_duration_ms: u64,
}

fn default_epoch_duration_ms() -> u64 {
    DEFAULT_EPOCH_DURATION_MS
}

impl ApplicationConfig {
    /// Load configuration from multiple sources in order of priority:
    /// 1. Environment variables (prefixed with AKD_PUBLISHER) - always applied with highest priority
    /// 2. Configuration file from AKD_PUBLISHER_CONFIG_PATH environment variable (if set)
    /// 3. OR default configuration file (config.toml, config.yaml, config.json) in working directory
    ///
    /// Environment variable naming:
    /// - Uses double underscore (__) as separator
    /// - For field `epoch_duration_ms`, use `AKD_PUBLISHER__EPOCH_DURATION_MS`
    /// - For nested fields like `storage.cache_clean_ms`, use `AKD_PUBLISHER__STORAGE__CACHE_CLEAN_MS`
    ///
    /// Note: Only one config file source is used - either custom path OR default location
    pub fn load() -> Result<Self, ConfigError> {
        let mut builder = Config::builder();

        // Check for custom config path via environment variable
        if let Ok(config_path) = std::env::var("AKD_PUBLISHER_CONFIG_PATH") {
            builder = builder.add_source(File::with_name(&config_path).required(true));
        } else {
            // Fall back to default config file locations
            builder = builder.add_source(File::with_name("config").required(false));
        }

        let config = builder
            // Add environment variables with prefix "AKD_PUBLISHER_"
            .add_source(Environment::with_prefix("AKD_PUBLISHER").separator("__"))
            .build()?;

        let publisher_config: Self = config.try_deserialize()?;

        publisher_config.validate()?;

        Ok(publisher_config)
    }

    pub fn validate(&self) -> Result<(), ConfigError> {
        self.storage
            .validate()
            .map_err(|e| ConfigError::Message(format!("{e}")))?;
        self.publisher.validate()?;
        Ok(())
    }
}

impl PublisherConfig {
    pub fn validate(&self) -> Result<(), ConfigError> {
        if self.epoch_duration_ms <= 0 {
            return Err(ConfigError::Message(
                "epoch_duration_ms must be greater than 0".to_string(),
            ));
        }
        Ok(())
    }
}
