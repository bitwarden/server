use akd_storage::PublishQueueType;
use axum::routing::{get, post};

mod health;
mod publish;

#[derive(Clone)]
pub(crate) struct AppState {
    pub publish_queue: PublishQueueType,
}

pub fn api_routes() -> axum::Router<AppState> {
    axum::Router::new()
        .route("/health", get(health::health_handler))
        .route("/publish", post(publish::publish_handler))
}
