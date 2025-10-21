use tracing::{info, trace};

#[tokio::main]
async fn main() {
    tracing_subscriber::fmt()
    .with_max_level(tracing::Level::TRACE)
    .init();

    // Read connection string from env var
    let connection_string = std::env::var("AKD_MSSQL_CONNECTION_STRING").expect("AKD_MSSQL_CONNECTION_STRING must be set.");

    let mssql = akd_storage::ms_sql::MsSql::builder(connection_string)
        .pool_size(10)
        .build()
        .await
        .expect("Failed to create MsSql instance");

    mssql.migrate().await.expect("Failed to run migrations");
}
