//! AIO process for an AKD. Spins up multiple async tasks to handle publisher and reader roles.
//! Requires both read and write permissions to the underlying data stores.
//! There should only be one instance of this running at a time for a given AKD.

use akd_storage::db_config::DbConfig;
use common::VrfStorageType;

#[tokio::main]
#[allow(unreachable_code)]
async fn main() {
    tracing_subscriber::fmt()
        .with_max_level(tracing::Level::TRACE)
        .init();

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

    // Start the publisher write job
    let _write_job_handle = {
        let db = db.clone();
        let vrf = vrf.clone();
        tokio::spawn(async move {
            publisher::start_write_job(db, vrf).await;
        })
    };

    // Create a router with both publisher and reader routes
    todo!();

    // Start the web server
    todo!();
}
