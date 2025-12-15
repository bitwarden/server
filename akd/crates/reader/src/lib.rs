use akd::{directory::ReadOnlyDirectory, storage::StorageManager};
use akd_storage::DatabaseType;
use bitwarden_akd_configuration::BitwardenV1Configuration;
use common::VrfStorageType;
use tracing::instrument;

struct AppState {
    // Add any shared state here, e.g., database connections
    _directory: ReadOnlyDirectory<BitwardenV1Configuration, DatabaseType, VrfStorageType>,
}

#[instrument(skip_all, name = "reader_start")]
pub async fn start(db: DatabaseType, vrf: VrfStorageType) {
    let storage_manager = StorageManager::new_no_cache(db);
    let _app = AppState {
        _directory: ReadOnlyDirectory::new(storage_manager, vrf)
            .await
            .expect("Failed to create ReadOnlyDirectory"),
    };
    println!("Reader started");
}
