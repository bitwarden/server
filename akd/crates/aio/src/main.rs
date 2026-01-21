//! AIO process for an AKD. Spins up multiple async tasks to handle publisher and reader roles.
//! Requires both read and write permissions to the underlying data stores.
//! There should only be one instance of this running at a time for a given AKD.

use anyhow::{Context, Result};
use tracing::{error, info};
use tracing_subscriber::EnvFilter;

mod config;

use config::ApplicationConfig;

#[tokio::main]
async fn main() -> Result<()> {
    let env_filter = EnvFilter::builder()
        .with_default_directive(tracing::level_filters::LevelFilter::INFO.into())
        .from_env_lossy();

    tracing_subscriber::fmt().with_env_filter(env_filter).init();

    // Load configuration
    let config = ApplicationConfig::load()
        .map_err(|e| anyhow::anyhow!("Failed to load configuration: {e}"))?;

    // Initialize Bitwarden AKD configuration (must happen before starting services)
    bitwarden_akd_configuration::BitwardenV1Configuration::init(config.installation_id);

    // Create shutdown channel for coordinated shutdown
    let (shutdown_tx, shutdown_rx) = tokio::sync::broadcast::channel(1);

    // Convert unified config to service-specific configs
    let publisher_config = publisher::ApplicationConfig::from(&config);
    let reader_config = reader::ApplicationConfig::from(&config);

    // Start publisher service
    let mut publisher_handles = publisher::start(publisher_config, &shutdown_rx)
        .await
        .context("Failed to start publisher")?;

    // Start reader service
    let mut reader_handle = reader::start(reader_config, &shutdown_rx)
        .await
        .context("Failed to start reader")?;

    // Wait for shutdown signal or service completion
    tokio::select! {
        _ = tokio::signal::ctrl_c() => {
            info!("Received Ctrl+C, shutting down");
            shutdown_tx.send(()).ok();
        }
        _ = &mut publisher_handles.write_handle => {
            error!("Publisher write service completed unexpectedly");
        }
        _ = &mut publisher_handles.web_handle => {
            error!("Publisher web service completed unexpectedly");
        }
        _ = &mut reader_handle => {
            error!("Reader service completed unexpectedly");
        }
    }

    // Wait for all services to complete
    info!("Waiting for services to shut down...");
    publisher_handles.write_handle.await.ok();
    publisher_handles.web_handle.await.ok();

    // Reader handle returns a Result, so we need to handle it properly
    match reader_handle.await {
        Ok(Ok(())) => info!("Reader service shut down successfully"),
        Ok(Err(e)) => error!("Reader service failed: {e}"),
        Err(e) => error!("Failed to join reader service task: {e}"),
    }

    info!("All services shut down");
    Ok(())
}
