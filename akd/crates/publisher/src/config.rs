use akd_storage::akd_storage_config::AkdStorageConfig;
use config::{Config, ConfigError, Environment, File};
use serde::Deserialize;
use subtle::ConstantTimeEq;
use uuid::Uuid;

const DEFAULT_EPOCH_DURATION_MS: u64 = 30000; // 30 seconds

/// Application configuration for the AKD Publisher
#[derive(Clone, Debug, Deserialize)]
pub struct ApplicationConfig {
    pub storage: AkdStorageConfig,
    #[serde(default)]
    pub publisher: PublisherConfig,
    /// The unique Bitwarden installation ID using this AKD publisher instance.
    /// This value is used to namespace AKD data to a given installation.
    pub installation_id: Uuid,
    /// The address the web server will bind to. Defaults to "127.0.0.1:3000".
    #[serde(default = "default_web_server_bind_address")]
    pub web_server_bind_address: String,
    /// The API key required to access the web server endpoints.
    ///
    /// NOTE: constant-time comparison is used, but mismatched string length cause immediate failure.
    /// For this reason, timing attacks can be used to at least determine the valid key length and a
    /// sufficiently long key should be used to mitigate this risk.
    pub web_server_api_key: String,
    // web_server: WebServerConfig,
}

fn default_web_server_bind_address() -> String {
    "127.0.0.1:3000".to_string()
}

/// Configuration for how the AKD updates
#[derive(Clone, Debug, Deserialize)]
pub struct PublisherConfig {
    /// The duration of each publishing epoch in milliseconds. Defaults to 30 seconds.
    #[serde(default = "default_epoch_duration_ms")]
    pub epoch_duration_ms: u64,
    /// The limit to the number of AKD values to update in a single epoch. Defaults to no limit.
    #[serde(default = "default_epoch_update_limit")]
    pub epoch_update_limit: Option<isize>,
}

impl Default for PublisherConfig {
    fn default() -> Self {
        PublisherConfig {
            epoch_duration_ms: default_epoch_duration_ms(),
            epoch_update_limit: default_epoch_update_limit(),
        }
    }
}

fn default_epoch_duration_ms() -> u64 {
    DEFAULT_EPOCH_DURATION_MS
}

fn default_epoch_update_limit() -> Option<isize> {
    None
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

    /// Get the web server bind address as a SocketAddr
    /// Panics if the address is invalid
    pub fn socket_address(&self) -> std::net::SocketAddr {
        self.web_server_bind_address
            .parse()
            .expect("Invalid web server bind address")
    }

    pub fn api_key_valid(&self, api_key: &str) -> bool {
        self.web_server_api_key
            .as_bytes()
            .ct_eq(api_key.as_bytes())
            .into()
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
