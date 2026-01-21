use axum::{extract::State, http::StatusCode, Json};
use serde::{Deserialize, Serialize};
use tracing::{error, info, instrument};

use crate::{
    error::ReaderError,
    routes::{get_epoch_hash::EpochData, lookup::AkdLabelB64, Response},
    AppState,
};

#[derive(Debug, Serialize, Deserialize)]
pub struct BatchLookupRequest {
    /// An array of labels to look up. Each label is encoded as base64.
    pub labels_b64: Vec<AkdLabelB64>,
}

#[derive(Debug, Serialize, Deserialize)]
pub struct BatchLookupData {
    pub lookup_proofs: Vec<akd::LookupProof>,
    pub epoch_data: EpochData,
}

#[instrument(skip_all)]
pub async fn batch_lookup_handler(
    State(AppState { directory, .. }): State<AppState>,
    Json(BatchLookupRequest { labels_b64 }): Json<BatchLookupRequest>,
) -> (StatusCode, Json<Response<BatchLookupData>>) {
    info!("Handling batch lookup request");

    // Validate batch not empty
    if labels_b64.is_empty() {
        error!("Empty batch request received");
        return (
            StatusCode::BAD_REQUEST,
            Json(Response::error(ReaderError::EmptyBatch)),
        );
    }

    // Validate batch size
    // TODO: make this configurable
    const MAX_BATCH_SIZE: usize = 1000;
    if labels_b64.len() > MAX_BATCH_SIZE {
        error!(
            batch_size = labels_b64.len(),
            max_size = MAX_BATCH_SIZE,
            "Batch size exceeds limit"
        );
        return (
            StatusCode::BAD_REQUEST,
            Json(Response::error(ReaderError::BatchTooLarge {
                limit: MAX_BATCH_SIZE,
            })),
        );
    }

    let labels = labels_b64
        .into_iter()
        .map(|label_b64| label_b64.into())
        .collect::<Vec<akd::AkdLabel>>();
    let lookup_proofs = directory.batch_lookup(&labels).await;

    match lookup_proofs {
        Ok((lookup_proofs, epoch_hash)) => (
            StatusCode::OK,
            Json(Response::success(BatchLookupData {
                lookup_proofs,
                epoch_data: epoch_hash.into(),
            })),
        ),
        Err(e) => {
            let reader_error = ReaderError::Akd(e);
            let status = reader_error.status_code();
            error!(err = ?reader_error, status = %status, "Failed to perform batch lookup");
            (status, Json(Response::error(reader_error)))
        }
    }
}
