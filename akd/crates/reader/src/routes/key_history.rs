use axum::{extract::State, http::StatusCode, Json};
use bitwarden_akd_configuration::{
    wire_models::{HistoryData, KeyHistoryRequest},
    BitwardenAkdLabelMaterial,
};
use tracing::{error, info, instrument};

use super::Response;
use crate::{error::ReaderError, AppState};

#[instrument(skip_all)]
pub async fn key_history_handler(
    State(AppState { directory, .. }): State<AppState>,
    Json(KeyHistoryRequest {
        bitwarden_akd_label_material,
        history_params,
    }): Json<KeyHistoryRequest>,
) -> (StatusCode, Json<Response<HistoryData>>) {
    info!("Handling get key history request");
    let label: BitwardenAkdLabelMaterial = bitwarden_akd_label_material.into();
    let history_proof = directory
        .key_history(&(&label).into(), history_params.into())
        .await;

    match history_proof {
        Ok((history_proof, epoch_hash)) => (
            StatusCode::OK,
            Json(Response::success(HistoryData {
                history_proof,
                epoch_data: epoch_hash.into(),
            })),
        ),
        Err(e) => {
            let reader_error = ReaderError::Akd(e);
            let status = reader_error.status_code();
            error!(err = ?reader_error, status = %status, "Failed to get key history");
            (status, Json(Response::error(reader_error.to_error_response())))
        }
    }
}
