use axum::{extract::State, http::StatusCode, Json};
use bitwarden_akd_configuration::{
    wire_models::{LookupData, LookupRequest},
    BitwardenAkdLabelMaterial,
};
use tracing::{error, info, instrument};

use super::Response;
use crate::{error::ReaderError, AppState};

#[instrument(skip_all)]
pub async fn lookup_handler(
    State(AppState { directory, .. }): State<AppState>,
    Json(LookupRequest {
        bitwarden_akd_label_material,
    }): Json<LookupRequest>,
) -> (StatusCode, Json<Response<LookupData>>) {
    info!("Handling lookup request");
    let label: BitwardenAkdLabelMaterial = bitwarden_akd_label_material.into();
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
            (status, Json(Response::error(reader_error.to_error_response())))
        }
    }
}
