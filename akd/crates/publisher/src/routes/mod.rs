use akd_storage::PublishQueueType;
use axum::{
    extract::{Request, State},
    http::StatusCode,
    middleware::Next,
    response::Response,
    routing::{get, post},
};

use crate::ApplicationConfig;

mod health;
mod publish;

#[derive(Clone)]
pub(crate) struct AppState {
    pub app_config: ApplicationConfig,
    pub publish_queue: PublishQueueType,
}

pub fn api_routes() -> axum::Router<AppState> {
    axum::Router::new()
        .route("/health", get(health::health_handler))
        .route("/publish", post(publish::publish_handler))
}

pub async fn auth(
    State(AppState { app_config, .. }): State<AppState>,
    req: Request,
    next: Next,
) -> Result<Response, StatusCode> {
    let auth_header = req
        .headers()
        .get("x-api-key")
        .and_then(|header| header.to_str().ok());

    let auth_header = if let Some(auth_header) = auth_header {
        auth_header
    } else {
        return Err(StatusCode::UNAUTHORIZED);
    };

    tracing::trace!(
        auth_header,
        key = app_config.web_server_api_key,
        "Authenticating request with provided API key"
    );
    if app_config.api_key_valid(auth_header) {
        // API key matches, proceed to the next handler
        Ok(next.run(req).await)
    } else {
        Err(StatusCode::UNAUTHORIZED)
    }
}
