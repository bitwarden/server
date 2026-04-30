use axum::{extract::State, http::StatusCode, Json};
use bitwarden_akd_configuration::wire_models::EpochData;
use tracing::{error, info, instrument};

use super::Response;
use crate::{error::ReaderError, AppState};

#[instrument(skip_all)]
pub async fn get_epoch_hash_handler(
    State(AppState { directory, .. }): State<AppState>,
) -> (StatusCode, Json<Response<EpochData>>) {
    info!("Handling get epoch hash request");
    let epoch_hash = directory.get_epoch_hash().await;

    match epoch_hash {
        Ok(epoch_hash) => (
            StatusCode::OK,
            Json(Response::<EpochData>::success(epoch_hash.into())),
        ),
        Err(e) => {
            let reader_error = ReaderError::Akd(e);
            let status = reader_error.status_code();
            error!(err = ?reader_error, status = %status, "Failed to get current AKD epoch hash");
            (status, Json(Response::error(reader_error.to_error_response())))
        }
    }
}
