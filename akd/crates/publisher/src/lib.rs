use anyhow::Result;
use bitwarden_akd_configuration::BitwardenV1Configuration;
use tokio::sync::broadcast::Receiver;
use tracing::{info, instrument};

mod config;
mod routes;

pub use crate::config::ApplicationConfig;

pub struct AppHandles {
    pub write_handle: tokio::task::JoinHandle<()>,
    pub web_handle: tokio::task::JoinHandle<()>,
}

#[instrument(skip_all, name = "publisher_start")]
pub async fn start(config: ApplicationConfig, shutdown_rx: &Receiver<()>) -> Result<AppHandles> {
    let (directory, db, publish_queue) = config
        .storage
        .initialize_directory::<BitwardenV1Configuration>()
        .await?;

    // Initialize write job
    let write_handle = {
        let mut shutdown_rx = shutdown_rx.resubscribe();
        tokio::spawn(async move {
            // wait until shutdown signal is received
            shutdown_rx.recv().await.ok();
            info!("Shutting down publisher write job");
        })
    };

    // Initialize web server
    let web_handle = {
        let mut shutdown_rx = shutdown_rx.resubscribe();
        tokio::spawn(async move {
            // wait forever until shutdown signal is received
            shutdown_rx.recv().await.ok();
            info!("Shutting down publisher web server");
        })
    };

    Ok(AppHandles {
        write_handle,
        web_handle,
    })
}
