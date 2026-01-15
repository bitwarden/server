use akd_storage::akd_storage_config::AkdStorageConfig;
use config::{Config, ConfigError, Environment, File};
use serde::Deserialize;
use uuid::Uuid;

#[derive(Clone, Debug, Deserialize)]
pub struct ApplicationConfig {
    pub storage: AkdStorageConfig,
    /// The address the web server will bind to. Defaults to "127.0.0.1:3001".
    #[serde(default = "default_web_server_bind_address")]
    web_server_bind_address: String,
    /// The unique Bitwarden installation ID using this AKD reader instance.
    /// This value is used to namespace AKD data to a given installation.
    pub installation_id: Uuid,
}

fn default_web_server_bind_address() -> String {
    "127.0.0.1:3001".to_string()
}

impl ApplicationConfig {
    /// Load configuration from multiple sources in order of priority:
    /// 1. Environment variables (prefixed with AKD_READER) - always applied with highest priority
    /// 2. Configuration file from AKD_READER_CONFIG_PATH environment variable (if set)
    /// 3. OR default configuration file (config.toml, config.yaml, config.json) in working directory
    ///
    /// Environment variable naming:
    /// - Uses double underscore (__) as separator
    /// - For field `epoch_duration_ms`, use `AKD_READER__EPOCH_DURATION_MS`
    /// - For nested fields like `storage.cache_clean_ms`, use `AKD_READER__STORAGE__CACHE_CLEAN_MS`
    ///
    /// Note: Only one config file source is used - either custom path OR default location
    pub fn load() -> Result<Self, ConfigError> {
        let mut builder = Config::builder();

        // Check for custom config path via environment variable
        if let Ok(config_path) = std::env::var("AKD_READER_CONFIG_PATH") {
            builder = builder.add_source(File::with_name(&config_path).required(true));
        } else {
            // Fall back to default config file locations
            builder = builder.add_source(File::with_name("config").required(false));
        }

        let config = builder
            // Add environment variables with prefix "AKD_READER_"
            .add_source(Environment::with_prefix("AKD_READER").separator("__"))
            .build()?;

        let reader_config: Self = config.try_deserialize()?;

        reader_config.validate()?;

        Ok(reader_config)
    }

    pub fn validate(&self) -> Result<(), ConfigError> {
        self.storage
            .validate()
            .map_err(|e| ConfigError::Message(format!("{e}")))?;
        Ok(())
    }

    /// Get the web server bind address as a SocketAddr
    /// Panics if the address is invalid
    pub fn socket_address(&self) -> std::net::SocketAddr {
        self.web_server_bind_address
            .parse()
            .expect("Invalid web server bind address")
    }
}
