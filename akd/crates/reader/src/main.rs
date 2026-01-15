//! The Reader crate is responsible for handling read requests to the AKD. It requires only read permissions to the
//! underlying data stores, and can be horizontally scaled as needed.

use anyhow::Context;
use anyhow::Result;
use tracing::error;
use tracing::info;
use tracing::level_filters::LevelFilter;
use tracing_subscriber::EnvFilter;

use reader::start;
use reader::ApplicationConfig;

#[tokio::main]
async fn main() -> Result<()> {
    let env_filter = EnvFilter::builder()
        .with_default_directive(LevelFilter::INFO.into())
        .from_env_lossy();

    tracing_subscriber::fmt().with_env_filter(env_filter).init();

    // Load configuration
    let config = ApplicationConfig::load().context("Failed to load configuration")?;

    // Initialize Bitwarden AKD configuration
    bitwarden_akd_configuration::BitwardenV1Configuration::init(config.installation_id);

    let (shutdown_tx, shutdown_rx) = tokio::sync::broadcast::channel(1);

    let mut web_handle = start(config, &shutdown_rx)
        .await
        .context("Failed to start reader")?;

    // wait for shutdown signal
    tokio::select! {
        _ = tokio::signal::ctrl_c() => {
            info!("Received Ctrl+C, shutting down");
            shutdown_tx.send(()).ok();
        }
        _ = &mut web_handle => {
            error!("Web service completed unexpectedly");
        }
    }

    web_handle
        .await
        .expect("Failed to join web service task")
        .expect("Web service task failed");

    Ok(())
}
