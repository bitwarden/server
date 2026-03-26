use akd::local_auditing::AuditBlob;
use async_trait::async_trait;
use thiserror::Error;

pub use self::config::AuditStorageConfig;
pub use self::filesystem::FilesystemAuditStorage;

mod config;
mod filesystem;

#[derive(Debug, Error)]
pub enum AuditStorageError {
    #[error("Audit blob not found for epoch {epoch}")]
    NotFound { epoch: u64 },
    #[error("Configuration error: {0}")]
    Config(String),
    #[error("IO error: {0}")]
    Io(String),
    #[error("Decode error: {0}")]
    Decode(String),
}

#[async_trait]
pub trait AuditStorage {
    /// Store an audit blob (protobuf-serialized `SingleAppendOnlyProof` with epoch hashes).
    async fn store_blob(&self, blob: &AuditBlob) -> Result<(), AuditStorageError>;

    /// Retrieve the audit blob for the transition `start_epoch → start_epoch + 1`.
    async fn get_blob(&self, epoch: u64) -> Result<AuditBlob, AuditStorageError>;

    /// Returns true if a blob exists for the given start epoch.
    async fn has_blob(&self, epoch: u64) -> Result<bool, AuditStorageError>;
}

#[derive(Debug, Clone)]
pub enum AuditStorageType {
    Filesystem(FilesystemAuditStorage),
}

impl AuditStorageType {
    pub fn new(config: &AuditStorageConfig) -> Result<AuditStorageType, AuditStorageError> {
        match config {
            AuditStorageConfig::Filesystem { data_directory } => {
                if data_directory.is_empty() {
                    return Err(AuditStorageError::Config(
                        "data_directory cannot be empty".to_string(),
                    ));
                }
                Ok(AuditStorageType::Filesystem(FilesystemAuditStorage::new(
                    data_directory.clone(),
                )))
            }
        }
    }
}

#[async_trait]
impl AuditStorage for AuditStorageType {
    async fn store_blob(&self, blob: &AuditBlob) -> Result<(), AuditStorageError> {
        match self {
            AuditStorageType::Filesystem(fs) => fs.store_blob(blob).await,
        }
    }

    async fn get_blob(&self, epoch: u64) -> Result<AuditBlob, AuditStorageError> {
        match self {
            AuditStorageType::Filesystem(fs) => fs.get_blob(epoch).await,
        }
    }

    async fn has_blob(&self, epoch: u64) -> Result<bool, AuditStorageError> {
        match self {
            AuditStorageType::Filesystem(fs) => fs.has_blob(epoch).await,
        }
    }
}
