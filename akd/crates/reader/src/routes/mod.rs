use akd::errors::AkdError;
use axum::routing::get;
use serde::{Deserialize, Serialize};

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
    success: bool,
    data: Option<T>,
    error: Option<ResponseError>,
}

#[derive(Debug, Serialize, Deserialize)]
pub struct ResponseError {
    pub akd_error_type: String,
    pub message: String,
}

impl From<AkdError> for ResponseError {
    fn from(err: AkdError) -> Self {
        ResponseError {
            akd_error_type: match &err {
                AkdError::TreeNode(_) => "TreeNode".to_string(),
                AkdError::Directory(err) => match err {
                    akd::errors::DirectoryError::Verification(_) => "VerificationError".to_string(),
                    akd::errors::DirectoryError::InvalidEpoch(_) => "InvalidEpoch".to_string(),
                    akd::errors::DirectoryError::ReadOnlyDirectory(_) => {
                        "ReadOnlyDirectory".to_string()
                    }
                    akd::errors::DirectoryError::Publish(_) => "Publish".to_string(),
                },
                AkdError::AzksErr(_) => "AzksErr".to_string(),
                AkdError::Vrf(_) => "Vrf".to_string(),
                AkdError::Storage(err) => match err {
                    akd::errors::StorageError::NotFound(_) => "NotFound".to_string(),
                    akd::errors::StorageError::Transaction(_) => "Transaction".to_string(),
                    akd::errors::StorageError::Connection(_) => "Connection".to_string(),
                    akd::errors::StorageError::Other(_) => "Other".to_string(),
                },
                AkdError::AuditErr(_) => "AuditErr".to_string(),
                AkdError::Parallelism(_) => "Parallelism".to_string(),
                AkdError::TestErr(_) => "TestErr".to_string(),
            },
            message: err.to_string(),
        }
    }
}

impl<'a, T: Serialize + Deserialize<'a>> Response<T> {
    fn success(data: T) -> Self {
        Self {
            success: true,
            data: Some(data),
            error: None,
        }
    }

    fn fail(err: AkdError) -> Self {
        Self {
            success: false,
            data: None,
            error: Some(err.into()),
        }
    }
}
