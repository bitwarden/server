//! The Reader crate is responsible for handling read requests to the AKD. It requires only read permissions to the
//! underlying data stores, and can be horizontally scaled as needed.

use akd::ecvrf::VRFKeyStorage;
use akd_storage::db_config::DbConfig;
use common::VrfStorageType;
use reader::start;
use tracing::info;

#[tokio::main]
async fn main() {
    tracing_subscriber::fmt()
        .with_max_level(tracing::Level::TRACE)
        .init();

    // Read connection string from env var
    let connection_string = std::env::var("AKD_MSSQL_CONNECTION_STRING")
        .expect("AKD_MSSQL_CONNECTION_STRING must be set.");
    let db_config = DbConfig::MsSql {
        connection_string,
        pool_size: 10,
    };

    let db = db_config
        .connect()
        .await
        .expect("Failed to connect to database");
    let vrf = VrfStorageType::HardCodedAkdVRF;

    let web_server_handle = tokio::spawn(async move {
        start(db, vrf).await;
    });

    // Wait for both services to complete
    tokio::select! {
        _ = web_server_handle => {
            info!("Web service completed");
        }
    }
}
