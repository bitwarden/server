use axum::{extract::State, http::StatusCode, Json};
use bitwarden_akd_configuration::{
    request_models::BitwardenAkdLabelMaterialRequest, BitwardenAkdLabelMaterial,
};
use serde::{Deserialize, Serialize};
use tracing::{error, info, instrument};

use crate::{
    error::ReaderError,
    routes::{get_epoch_hash::EpochData, Response},
    AppState,
};

#[derive(Debug, Serialize, Deserialize)]
pub struct LookupRequest {
    pub bitwarden_akd_label_material: BitwardenAkdLabelMaterialRequest,
}

#[derive(Debug, Serialize, Deserialize)]
pub struct LookupData {
    pub lookup_proof: akd::LookupProof,
    pub epoch_data: EpochData,
}

#[instrument(skip_all)]
pub async fn lookup_handler(
    State(AppState { directory, .. }): State<AppState>,
    Json(LookupRequest {
        bitwarden_akd_label_material,
    }): Json<LookupRequest>,
) -> (StatusCode, Json<Response<LookupData>>) {
    info!("Handling lookup request");
    let label: BitwardenAkdLabelMaterial = match bitwarden_akd_label_material.try_into() {
        Ok(label) => label,
        Err(e) => {
            let reader_error = ReaderError::RequestConversion(e);
            let status = reader_error.status_code();
            error!(err = ?reader_error, status = %status, "Invalid request");
            return (status, Json(Response::error(reader_error)));
        }
    };
    let lookup_proof = directory.lookup((&label).into()).await;

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
