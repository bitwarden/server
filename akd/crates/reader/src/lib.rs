use akd::directory::ReadOnlyDirectory;
use akd_storage::{AkdDatabase, VrfKeyDatabase};
use anyhow::{Context, Result};
use axum::Router;
use bitwarden_akd_configuration::BitwardenV1Configuration;
use tokio::{net::TcpListener, sync::broadcast::Receiver};
use tracing::{info, instrument};

mod config;
pub mod error;
mod routes;

pub use crate::config::ApplicationConfig;
pub use error::{ErrorCode, ErrorResponse, ReaderError};
pub use routes::response_types;

#[derive(Clone)]
struct AppState {
    // Add any shared state here, e.g., database connections
    directory: ReadOnlyDirectory<BitwardenV1Configuration, AkdDatabase, VrfKeyDatabase>,
    // TODO: use this to allow for unique failures for lookup and key history requests that have pending updates
    // publish_queue: ReadOnlyPublishQueueType,
    max_batch_lookup_size: usize,
}

#[instrument(skip_all, name = "reader_start")]
pub async fn start(
    config: ApplicationConfig,
    shutdown_rx: &Receiver<()>,
) -> Result<tokio::task::JoinHandle<Result<()>>> {
    let (directory, _, _) = config
        .storage
        .initialize_readonly_directory::<BitwardenV1Configuration>()
        .await
        .context("Failed to initialize ReadOnlyDirectory")?;

    let mut shutdown_rx = shutdown_rx.resubscribe();

    let max_batch_lookup_size = config.max_batch_lookup_size;
    let handle = tokio::spawn(async move {
        let app_state = AppState {
            directory: directory,
            // publish_queue: publish_queue,
            max_batch_lookup_size,
        };

        let app = Router::new()
            .merge(crate::routes::api_routes())
            .with_state(app_state);

        let listener = TcpListener::bind(&config.socket_address())
            .await
            .context("Socket bind failed")?;
        info!(
            socket_address = %config.socket_address(),
            "Reader web server listening"
        );

        axum::serve(listener, app.into_make_service())
            .with_graceful_shutdown(async move {
                shutdown_rx.recv().await.ok();
            })
            .await
            .context("Web server failed")?;

        Ok(())
    });

    Ok(handle)
}
