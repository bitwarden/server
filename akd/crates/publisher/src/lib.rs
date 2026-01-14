use akd_storage::{PublishQueue, PublishQueueType};
use anyhow::{Context, Result};
use axum::Router;
use bitwarden_akd_configuration::BitwardenV1Configuration;
use common::BitAkdDirectory;
use tokio::{net::TcpListener, sync::broadcast::Receiver};
use tracing::{error, info, instrument, trace};

mod config;
mod routes;

pub use crate::config::ApplicationConfig;

pub struct AppHandles {
    pub write_handle: tokio::task::JoinHandle<()>,
    pub web_handle: tokio::task::JoinHandle<()>,
}

#[instrument(skip_all, name = "publisher_start")]
pub async fn start(config: ApplicationConfig, shutdown_rx: &Receiver<()>) -> Result<AppHandles> {
    let (directory, _, publish_queue) = config
        .storage
        .initialize_directory::<BitwardenV1Configuration>()
        .await?;

    // Initialize write job
    let write_handle = {
        let shutdown_rx = shutdown_rx.resubscribe();
        let publish_queue = publish_queue.clone();
        let config = config.clone();

        tokio::spawn(async move {
            if let Err(e) = start_publisher(directory, publish_queue, &config, shutdown_rx).await {
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
    config: &ApplicationConfig,
    mut shutdown_rx: Receiver<()>,
) -> Result<()> {
    let mut next_epoch = tokio::time::Instant::now()
        + std::time::Duration::from_millis(config.publisher.epoch_duration_ms as u64);
    loop {
        trace!("Processing publish queue for epoch");

        // Pull items from publish queue
        let (ids, items) = publish_queue
            .peek(config.publisher.epoch_update_limit)
            .await?
            .into_iter()
            .fold((vec![], vec![]), |mut acc, i| {
                acc.0.push(i.0);
                acc.1.push(i.1);
                acc
            });

        let result: Result<()> = {
            // Apply items to directory
            directory
                .publish(items)
                .await
                .context("AKD publish failed")?;

            // Remove processed items from publish queue
            publish_queue
                .remove(ids)
                .await
                .context("Failed to remove processed publish queue items")?;
            Ok(())
        };

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

#[instrument(skip_all)]
async fn start_web(
    publish_queue: PublishQueueType,
    config: &ApplicationConfig,
    mut shutdown_rx: Receiver<()>,
) -> Result<()> {
    let app_state = routes::AppState { publish_queue };
    let app = Router::new()
        .merge(routes::api_routes())
        .with_state(app_state);

    let listener = TcpListener::bind(&config.socket_address())
        .await
        .context("Socket bind failed")?;
    info!(
        "Publisher web server listening on {}",
        config.socket_address()
    );
    axum::serve(listener, app.into_make_service())
        .with_graceful_shutdown(async move {
            shutdown_rx.recv().await.ok();
        })
        .await
        .context("Web server failed")?;

    Ok(())
}
