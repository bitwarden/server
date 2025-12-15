use akd::{directory::Directory, storage::StorageManager};
use akd_storage::DatabaseType;
use bitwarden_akd_configuration::BitwardenV1Configuration;
use common::VrfStorageType;
use tracing::instrument;

struct AppState {
    _directory: Directory<BitwardenV1Configuration, DatabaseType, VrfStorageType>,
}

#[instrument(skip_all, name = "publisher_start")]
pub async fn start_write_job(_db: DatabaseType, vrf: VrfStorageType) {
    let storage_manager = StorageManager::new_no_cache(_db);
    let _app_state = AppState {
        _directory: Directory::new(storage_manager, vrf).await.unwrap(),
    };
    println!("Publisher started");
}

pub async fn start_web_server(_db: DatabaseType) {
    println!("Web server started");
}
