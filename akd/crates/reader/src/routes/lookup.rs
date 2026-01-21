use axum::{extract::State, http::StatusCode, Json};
use serde::{Deserialize, Serialize};
use tracing::{error, info, instrument};

use crate::{
    error::ReaderError,
    routes::{get_epoch_hash::EpochData, Response},
    AppState,
};

#[derive(Debug, Serialize, Deserialize)]
pub struct AkdLabelB64(pub(crate) bitwarden_encoding::B64);

impl From<AkdLabelB64> for akd::AkdLabel {
    fn from(label_b64: AkdLabelB64) -> Self {
        akd::AkdLabel(label_b64.0.into_bytes())
    }
}

#[derive(Debug, Serialize, Deserialize)]
pub struct LookupRequest {
    /// the label to look up encoded as base64
    pub label_b64: AkdLabelB64,
}

#[derive(Debug, Serialize, Deserialize)]
pub struct LookupData {
    pub lookup_proof: akd::LookupProof,
    pub epoch_data: EpochData,
}

#[instrument(skip_all)]
pub async fn lookup_handler(
    State(AppState { directory, .. }): State<AppState>,
    Json(LookupRequest { label_b64 }): Json<LookupRequest>,
) -> (StatusCode, Json<Response<LookupData>>) {
    info!("Handling lookup request");
    let lookup_proof = directory.lookup(label_b64.into()).await;

    match lookup_proof {
        Ok((lookup_proof, epoch_hash)) => (
            StatusCode::OK,
            Json(Response::success(LookupData {
                lookup_proof,
                epoch_data: epoch_hash.into(),
            })),
        ),
        Err(e) => {
            let reader_error = ReaderError::Akd(e);
            let status = reader_error.status_code();
            error!(err = ?reader_error, status = %status, "Failed to perform lookup");
            (status, Json(Response::error(reader_error)))
        }
    }
}
