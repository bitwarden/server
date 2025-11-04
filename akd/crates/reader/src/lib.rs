use std::sync::Arc;

use akd::{directory::ReadOnlyDirectory, storage::StorageManager};
use tracing::instrument;
use bitwarden_akd_configuration::BitwardenV1Configuration;
use akd_storage::DatabaseType;
use common::VrfStorageType;

struct AppState {
    // Add any shared state here, e.g., database connections
    directory: ReadOnlyDirectory<BitwardenV1Configuration, DatabaseType, VrfStorageType>,
}

#[instrument(skip_all, name = "reader_start")]
pub async fn start(db: DatabaseType, vrf: VrfStorageType) {
    let storage_manager = StorageManager::new_no_cache(db);
    let _app = AppState {
        directory: ReadOnlyDirectory::new(storage_manager, vrf).await.unwrap(),
    };
    println!("Reader started");
}
