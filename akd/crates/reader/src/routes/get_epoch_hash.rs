use akd::EpochHash;
use axum::{extract::State, http::StatusCode, Json};
use serde::{Deserialize, Serialize};
use tracing::{error, info, instrument};

use crate::{error::ReaderError, routes::Response, AppState};

#[derive(Debug, Serialize, Deserialize)]
pub struct EpochData {
    pub epoch: u64,
    pub epoch_hash_b64: bitwarden_encoding::B64,
}

impl From<EpochHash> for EpochData {
    fn from(epoch_hash: EpochHash) -> Self {
        EpochData {
            epoch: epoch_hash.0,
            epoch_hash_b64: bitwarden_encoding::B64::from(epoch_hash.1.as_ref()),
        }
    }
}

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
            (status, Json(Response::error(reader_error)))
        }
    }
}
