use axum::routing::get;

mod health;

use crate::AppState;

pub fn api_routes() -> axum::Router<AppState> {
    axum::Router::new().route("/health", get(health::health_handler))
}
