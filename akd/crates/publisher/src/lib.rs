use akd_storage::{AuditStorage, AuditStorageType, PublishQueue, PublishQueueType};
use anyhow::{Context, Result};
use axum::{middleware::from_fn_with_state, Router};
use bitwarden_akd_configuration::BitwardenV1Configuration;
use common::BitAkdDirectory;
use tokio::{net::TcpListener, sync::broadcast::Receiver};
use tracing::{error, info, instrument, trace};

mod config;
mod routes;

pub use crate::config::{ApplicationConfig, PublisherConfig};
use crate::routes::auth;

pub struct AppHandles {
    pub write_handle: tokio::task::JoinHandle<()>,
    pub web_handle: tokio::task::JoinHandle<()>,
}

#[instrument(skip_all, name = "publisher_start")]
pub async fn start(config: ApplicationConfig, shutdown_rx: &Receiver<()>) -> Result<AppHandles> {
    let (directory, _, publish_queue, audit_storage) = config
        .storage
        .initialize_directory::<BitwardenV1Configuration>()
        .await?;

    // Initialize write job
    let write_handle = {
        let shutdown_rx = shutdown_rx.resubscribe();
        let publish_queue = publish_queue.clone();
        let config = config.clone();

        tokio::spawn(async move {
            if let Err(e) =
                start_publisher(directory, publish_queue, audit_storage, &config, shutdown_rx).await
            {
                error!(err = %e, "Publisher write job failed");
            }
        })
    };

    // Initialize web server
    let web_handle = {
        let shutdown_rx = shutdown_rx.resubscribe();
        let publish_queue = publish_queue.clone();

        tokio::spawn(async move {
            if let Err(e) = start_web(publish_queue, &config, shutdown_rx).await {
                error!(err = %e, "Web server failed");
            }
        })
    };

    Ok(AppHandles {
        write_handle,
        web_handle,
    })
}

#[instrument(skip_all)]
async fn start_publisher(
    directory: BitAkdDirectory,
    publish_queue: PublishQueueType,
    audit_storage: Option<AuditStorageType>,
    config: &ApplicationConfig,
    mut shutdown_rx: Receiver<()>,
) -> Result<()> {
    let mut next_epoch = tokio::time::Instant::now()
        + std::time::Duration::from_millis(config.publisher.epoch_duration_ms as u64);
    loop {
        let result =
            check_publish_queue(&directory, &publish_queue, audit_storage.as_ref(), config).await;

        if let Err(e) = result {
            error!(%e, "Error processing publish queue items");
            //TODO: What actions to take to recover?
            return Err(anyhow::anyhow!("Error processing publish queue items"));
        };

        info!(
            approx_wait_in_sec = next_epoch
                .duration_since(tokio::time::Instant::now())
                .as_secs_f64(),
            "Waiting for next epoch or shutdown signal"
        );
        tokio::select! {
            _ = shutdown_rx.recv() => {
                info!("Shutting down publisher job");
                break;
            }
            // Sleep until next epoch
            _ = tokio::time::sleep_until(next_epoch) => {
                // Continue to process publish queue
                next_epoch = tokio::time::Instant::now()
                    + std::time::Duration::from_millis(config.publisher.epoch_duration_ms as u64);
            }
        };
    }

    Ok(())
}

#[instrument(skip_all, level = "info")]
async fn check_publish_queue(
    directory: &BitAkdDirectory,
    publish_queue: &PublishQueueType,
    audit_storage: Option<&AuditStorageType>,
    config: &ApplicationConfig,
) -> Result<()> {
    trace!("Processing publish queue for epoch");

    // Pull items from publish queue
    let items = publish_queue
        .peek(config.publisher.epoch_update_limit)
        .await
        .context("Failed to peek publish queue")?;

    if items.is_empty() {
        info!("No items in publish queue to process");
        return Ok(());
    }

    trace!(num_items = items.len(), "Publishing items to AKD");
    let (ids, items) = items.into_iter().fold((vec![], vec![]), |mut acc, i| {
        acc.0.push(i.0);
        acc.1.push(i.1);
        acc
    });

    // Capture epoch hash before publish so we can generate the audit proof afterward.
    // This is safe because there is only one publisher loop (no concurrent publishers).
    let akd::helper_structs::EpochHash(prev_epoch, prev_hash) = directory
        .get_epoch_hash()
        .await
        .context("Failed to get epoch hash before publish")?;

    // Apply items to directory
    directory
        .publish(items)
        .await
        .context("AKD publish failed")?;

    let akd::helper_structs::EpochHash(new_epoch, new_hash) = directory
        .get_epoch_hash()
        .await
        .context("Failed to get epoch hash after publish")?;

    // Store audit proof to blob storage.
    // Failures are logged and swallowed — the AKD data is already committed and
    // proofs can be recomputed from the database if needed.
    if let Some(audit_storage) = audit_storage {
        store_audit_blobs(
            directory,
            audit_storage,
            prev_epoch,
            prev_hash,
            new_epoch,
            new_hash,
        )
        .await;
    }

    // Remove processed items from publish queue
    publish_queue
        .remove(ids)
        .await
        .context("Failed to remove processed publish queue items")?;
    Ok(())
}

#[instrument(skip_all, fields(prev_epoch, new_epoch))]
async fn store_audit_blobs(
    directory: &BitAkdDirectory,
    audit_storage: &AuditStorageType,
    prev_epoch: u64,
    prev_hash: akd::Digest,
    new_epoch: u64,
    new_hash: akd::Digest,
) {
    let proof = match directory.audit(prev_epoch, new_epoch).await {
        Ok(proof) => proof,
        Err(e) => {
            error!(err = ?e, prev_epoch, new_epoch, "Failed to compute audit proof after publish");
            return;
        }
    };

    let blobs = match akd::local_auditing::generate_audit_blobs(vec![prev_hash, new_hash], proof) {
        Ok(blobs) => blobs,
        Err(e) => {
            error!(err = ?e, prev_epoch, new_epoch, "Failed to generate audit blobs");
            return;
        }
    };

    for blob in &blobs {
        match audit_storage.store_blob(blob).await {
            Ok(()) => info!(epoch = blob.name.epoch, "Stored audit blob"),
            Err(e) => error!(%e, epoch = blob.name.epoch, "Failed to store audit blob"),
        }
    }
}

#[instrument(skip_all)]
async fn start_web(
    publish_queue: PublishQueueType,
    config: &ApplicationConfig,
    mut shutdown_rx: Receiver<()>,
) -> Result<()> {
    let app_state = routes::AppState {
        publish_queue,
        app_config: config.clone(),
    };
    let app = Router::new()
        .merge(routes::api_routes())
        .route_layer(from_fn_with_state(app_state.clone(), auth))
        .with_state(app_state);

    let socket_addr = config
        .socket_address()
        .context("Failed to parse socket address")?;
    let listener = TcpListener::bind(&socket_addr)
        .await
        .context("Socket bind failed")?;
    info!("Publisher web server listening on {}", socket_addr);
    axum::serve(listener, app.into_make_service())
        .with_graceful_shutdown(async move {
            shutdown_rx.recv().await.ok();
        })
        .await
        .context("Web server failed")?;

    Ok(())
}
