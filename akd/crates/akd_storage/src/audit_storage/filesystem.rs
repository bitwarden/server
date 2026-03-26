use akd::local_auditing::{AuditBlob, AuditBlobName};
use async_trait::async_trait;
use std::{convert::TryFrom, path::PathBuf};
use tokio::fs;
use tracing::instrument;

use super::{AuditStorage, AuditStorageError};

#[derive(Debug, Clone)]
pub struct FilesystemAuditStorage {
    data_directory: String,
}

impl FilesystemAuditStorage {
    pub fn new(data_directory: String) -> Self {
        FilesystemAuditStorage { data_directory }
    }

    /// File path for a blob: `{data_directory}/{epoch}/{prev_hash_hex}/{curr_hash_hex}`
    fn blob_path(&self, name: &AuditBlobName) -> PathBuf {
        PathBuf::from(&self.data_directory).join(name.to_string())
    }

    /// Epoch directory: `{data_directory}/{epoch}`
    fn epoch_dir(&self, epoch: u64) -> PathBuf {
        PathBuf::from(&self.data_directory).join(epoch.to_string())
    }
}

#[async_trait]
impl AuditStorage for FilesystemAuditStorage {
    #[instrument(skip(self, blob), fields(epoch = blob.name.epoch))]
    async fn store_blob(&self, blob: &AuditBlob) -> Result<(), AuditStorageError> {
        let path = self.blob_path(&blob.name);
        let parent = path.parent().ok_or_else(|| {
            AuditStorageError::Io(format!("Invalid blob path: {}", path.display()))
        })?;

        fs::create_dir_all(parent).await.map_err(|e| {
            AuditStorageError::Io(format!("Failed to create directory {}: {e}", parent.display()))
        })?;

        fs::write(&path, &blob.data).await.map_err(|e| {
            AuditStorageError::Io(format!("Failed to write blob to {}: {e}", path.display()))
        })?;

        Ok(())
    }

    #[instrument(skip(self), fields(epoch))]
    async fn get_blob(&self, epoch: u64) -> Result<AuditBlob, AuditStorageError> {
        let epoch_dir = self.epoch_dir(epoch);

        // Hierarchy: {epoch}/{prev_hash_hex}/{curr_hash_hex}
        let prev_hash_entry = first_dir_entry(&epoch_dir).await.map_err(|e| {
            if e.kind() == std::io::ErrorKind::NotFound {
                AuditStorageError::NotFound { epoch }
            } else {
                AuditStorageError::Io(format!("Failed to read epoch dir: {e}"))
            }
        })?;

        let prev_hash_dir = prev_hash_entry.path();
        let curr_hash_entry = first_dir_entry(&prev_hash_dir)
            .await
            .map_err(|e| AuditStorageError::Io(format!("Failed to read prev_hash dir: {e}")))?;

        let curr_hash_path = curr_hash_entry.path();
        let data = fs::read(&curr_hash_path)
            .await
            .map_err(|e| AuditStorageError::Io(format!("Failed to read blob file: {e}")))?;

        let name_str = format!(
            "{}/{}/{}",
            epoch,
            prev_hash_entry.file_name().to_string_lossy(),
            curr_hash_entry.file_name().to_string_lossy(),
        );
        let name = AuditBlobName::try_from(name_str.as_str()).map_err(|e| {
            AuditStorageError::Decode(format!(
                "Failed to parse blob name from path '{name_str}': {e:?}"
            ))
        })?;

        Ok(AuditBlob { name, data })
    }

    #[instrument(skip(self), fields(epoch))]
    async fn has_blob(&self, epoch: u64) -> Result<bool, AuditStorageError> {
        fs::try_exists(self.epoch_dir(epoch))
            .await
            .map_err(|e| AuditStorageError::Io(format!("Failed to check epoch dir: {e}")))
    }
}

/// Returns the first entry in a directory, or an IO error.
async fn first_dir_entry(dir: &PathBuf) -> Result<fs::DirEntry, std::io::Error> {
    let mut read_dir = fs::read_dir(dir).await?;
    read_dir
        .next_entry()
        .await?
        .ok_or_else(|| std::io::Error::new(std::io::ErrorKind::NotFound, "Empty directory"))
}

#[cfg(test)]
mod tests {
    use super::*;
    use akd::local_auditing::AuditBlobName;

    fn make_test_blob(epoch: u64) -> AuditBlob {
        let name = AuditBlobName {
            epoch,
            previous_hash: [1u8; 32],
            current_hash: [2u8; 32],
        };
        AuditBlob {
            name,
            // Minimal valid protobuf for SingleAppendOnlyProof with empty fields
            data: vec![],
        }
    }

    #[tokio::test]
    async fn test_store_and_get_blob() {
        let dir = tempfile::tempdir().expect("tempdir");
        let storage = FilesystemAuditStorage::new(dir.path().to_string_lossy().into_owned());
        let blob = make_test_blob(42);

        storage.store_blob(&blob).await.expect("store_blob");

        let retrieved = storage.get_blob(42).await.expect("get_blob");
        assert_eq!(retrieved.name.epoch, blob.name.epoch);
        assert_eq!(retrieved.name.previous_hash, blob.name.previous_hash);
        assert_eq!(retrieved.name.current_hash, blob.name.current_hash);
        assert_eq!(retrieved.data, blob.data);
    }

    #[tokio::test]
    async fn test_store_blob_and_has_blob() {
        let dir = tempfile::tempdir().expect("tempdir");
        let storage = FilesystemAuditStorage::new(dir.path().to_string_lossy().into_owned());
        let blob = make_test_blob(42);

        storage.store_blob(&blob).await.expect("store_blob");

        assert!(storage.has_blob(42).await.expect("has_blob"));
    }

    #[tokio::test]
    async fn test_get_blob_not_found() {
        let dir = tempfile::tempdir().expect("tempdir");
        let storage = FilesystemAuditStorage::new(dir.path().to_string_lossy().into_owned());

        let result = storage.get_blob(99).await;
        assert!(matches!(result, Err(AuditStorageError::NotFound { epoch: 99 })));
    }

    #[tokio::test]
    async fn test_has_blob_false() {
        let dir = tempfile::tempdir().expect("tempdir");
        let storage = FilesystemAuditStorage::new(dir.path().to_string_lossy().into_owned());

        assert!(!storage.has_blob(1).await.expect("has_blob"));
    }
}
