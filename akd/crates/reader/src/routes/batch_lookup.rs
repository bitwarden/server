use axum::{extract::State, http::StatusCode, Json};
use serde::{Deserialize, Serialize};
use tracing::{error, info, instrument};

use crate::{
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
    info!("Handling get public key request");
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
            error!(err = ?e, "Failed to get AKD public key");
            (StatusCode::INTERNAL_SERVER_ERROR, Json(Response::fail(e)))
        }
    }
}
