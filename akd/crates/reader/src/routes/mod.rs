use axum::routing::get;
use serde::{Deserialize, Serialize};

use crate::error::{ErrorResponse, ReaderError};

mod audit;
mod batch_lookup;
mod get_epoch_hash;
mod get_public_key;
mod health;
mod key_history;
mod lookup;
pub mod response_types;

use crate::AppState;

pub fn api_routes() -> axum::Router<AppState> {
    axum::Router::new()
        .route("/health", get(health::health_handler))
        .route("/public_key", get(get_public_key::get_public_key_handler))
        .route("/epoch_hash", get(get_epoch_hash::get_epoch_hash_handler))
        .route("/lookup", get(lookup::lookup_handler))
        .route("/key_history", get(key_history::key_history_handler))
        .route("/batch_lookup", get(batch_lookup::batch_lookup_handler))
        .route("/audit", get(audit::audit_handler))
}

#[derive(Debug, Serialize, Deserialize)]
pub struct Response<T> {
    pub success: bool,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub data: Option<T>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub error: Option<ErrorResponse>,
}

impl<T: Serialize> Response<T> {
    pub fn success(data: T) -> Self {
        Self {
            success: true,
            data: Some(data),
            error: None,
        }
    }

    pub fn error(err: ReaderError) -> Self {
        Self {
            success: false,
            data: None,
            error: Some(err.to_error_response()),
        }
    }
}
