//! The Publisher crate is responsible for tracking insert requests into the AKD and performing them as batches on a schedule.
//! Write permissions are needed to the underlying data stores.
//! There should only be one instance of this running at a time for a given AKD.

use tracing::info;
use tracing::level_filters::LevelFilter;
use tracing_subscriber::EnvFilter;

use publisher::start;
use publisher::ApplicationConfig;

#[tokio::main]
async fn main() -> anyhow::Result<()> {
    let env_filter = EnvFilter::builder()
        .with_default_directive(LevelFilter::INFO.into())
        .from_env_lossy();

    tracing_subscriber::fmt().with_env_filter(env_filter).init();

    // Load configuration
    let config = ApplicationConfig::load()
        .map_err(|e| anyhow::anyhow!("Failed to load configuration: {e}"))?;

    // Initialize Bitwarden AKD configuration
    bitwarden_akd_configuration::BitwardenV1Configuration::init(config.installation_id);

    let (shutdown_tx, shutdown_rx) = tokio::sync::broadcast::channel(1);

    let mut handles = start(config, &shutdown_rx)
        .await
        .map_err(|e| anyhow::anyhow!("Failed to start publisher: {e}"))?;

    // Wait for shutdown signal
    tokio::select! {
        _ = tokio::signal::ctrl_c() => {
            info!("Received Ctrl+C, shutting down");
            shutdown_tx.send(()).ok();
        }
        _ = &mut handles.write_handle => {
            info!("Publisher service completed unexpectedly");
        }
        _ = &mut handles.web_handle => {
            info!("Web service completed unexpectedly");
        }
    }

    // Wait for both services to complete
    handles.write_handle.await.ok();
    handles.web_handle.await.ok();

    Ok(())
}
