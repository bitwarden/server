use axum::routing::{get, post};

mod audit;
mod batch_lookup;
mod get_epoch_hash;
mod get_public_key;
mod health;
mod key_history;
mod lookup;

use crate::AppState;

pub(crate) type Response<T> =
    bitwarden_akd_configuration::wire_models::Response<T, crate::error::ErrorCode>;

pub fn api_routes() -> axum::Router<AppState> {
    axum::Router::new()
        .route("/health", get(health::health_handler))
        .route("/public_key", get(get_public_key::get_public_key_handler))
        .route("/epoch_hash", get(get_epoch_hash::get_epoch_hash_handler))
        .route("/lookup", post(lookup::lookup_handler))
        .route("/key_history", post(key_history::key_history_handler))
        .route("/batch_lookup", post(batch_lookup::batch_lookup_handler))
        .route("/audit", post(audit::audit_handler))
}
