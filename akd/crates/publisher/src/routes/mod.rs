use akd_storage::{AkdDatabase, PublishQueueType};
use axum::routing::{get, post};
use common::BitAkdDirectory;

mod health;
mod publish;

#[derive(Clone)]
pub struct AppState {
    pub directory: BitAkdDirectory,
    pub db: AkdDatabase,
    pub publish_queue: PublishQueueType,
}

pub fn api_routes() -> axum::Router<AppState> {
    axum::Router::new()
        .route("/health", get(health::health_handler))
        .route("/publish", post(publish::publish_handler))
}
