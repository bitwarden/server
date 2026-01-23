use akd::directory::ReadOnlyDirectory;
use akd_storage::{AkdDatabase, VrfKeyDatabase};
use anyhow::{Context, Result};
use axum::Router;
use bitwarden_akd_configuration::BitwardenV1Configuration;
use tokio::{net::TcpListener, sync::broadcast::Receiver};
use tracing::{info, instrument};

mod config;
mod epoch_tracker;
pub mod error;
mod routes;

use epoch_tracker::EpochTracker;

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
    epoch_tracker: EpochTracker,
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
    let epoch_tracker = EpochTracker::new(config.expected_epoch_duration_ms);
    let axum_handle = tokio::spawn(async move {
        let app_state = AppState {
            directory: directory.clone(),
            // publish_queue: publish_queue,
            max_batch_lookup_size,
            epoch_tracker: epoch_tracker.clone(),
        };

        let app = Router::new()
            .merge(crate::routes::api_routes())
            .with_state(app_state);

        let socket_addr = config.socket_address().context("Failed to parse socket address")?;
        let listener = TcpListener::bind(&socket_addr)
            .await
            .context("Socket bind failed")?;
        info!(
            socket_address = %socket_addr,
            "Reader web server listening"
        );

        // polls azks storage for epoch changes. This is necessary to pick up newly published updates.
        let epoch_tracker_for_poll = epoch_tracker.clone();
        let directory_for_poll = directory.clone();
        let poll_interval = config.azks_poll_interval_ms;

        let _poll_handle = tokio::spawn(async move {
            let (change_tx, mut change_rx) = tokio::sync::mpsc::channel::<()>(100);

            // Detector task: listens for changes and records to tracker
            let detector_handle = tokio::spawn(async move {
                let mut last_epoch = match directory_for_poll.get_epoch_hash().await {
                    Ok(epoch_hash) => {
                        tracing::info!(epoch = epoch_hash.0, "Initial epoch detected");
                        epoch_hash.0
                    }
                    Err(e) => {
                        tracing::error!("Failed to get initial epoch: {:?}", e);
                        0
                    }
                };

                while change_rx.recv().await.is_some() {
                    match directory_for_poll.get_epoch_hash().await {
                        Ok(epoch_hash) => {
                            let current_epoch = epoch_hash.0;
                            if current_epoch != last_epoch {
                                let published_at = chrono::Utc::now();
                                tracing::info!(
                                    previous_epoch = last_epoch,
                                    new_epoch = current_epoch,
                                    published_at = %published_at.to_rfc3339(),
                                    "Epoch publish detected"
                                );
                                epoch_tracker_for_poll.record_publish(published_at).await;
                                last_epoch = current_epoch;
                            }
                        }
                        Err(e) => {
                            tracing::error!("Failed to get epoch hash: {:?}", e);
                        }
                    }
                }
            });

            let result = directory
                .poll_for_azks_changes(
                    tokio::time::Duration::from_millis(poll_interval),
                    Some(change_tx),
                )
                .await;

            if let Err(e) = result {
                tracing::error!("Error polling for AZKS changes: {:?}", e);
            }

            detector_handle.abort();
        });

        axum::serve(listener, app.into_make_service())
            .with_graceful_shutdown(async move {
                shutdown_rx.recv().await.ok();
            })
            .await
            .context("Web server failed")?;

        Ok(())
    });

    Ok(axum_handle)
}
