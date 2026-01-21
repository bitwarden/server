use axum::{extract::State, http::StatusCode, Json};
use tracing::{error, info, instrument};

use crate::{error::ReaderError, routes::Response, AppState};

/// Public key encoded as a base64 string
pub type PublicKeyData = bitwarden_encoding::B64;

#[instrument(skip_all)]
pub async fn get_public_key_handler(
    State(AppState { directory, .. }): State<AppState>,
) -> (StatusCode, Json<Response<PublicKeyData>>) {
    info!("Handling get public key request");
    let public_key = directory.get_public_key().await;

    match public_key {
        Ok(public_key) => (
            StatusCode::OK,
            Json(Response::success(public_key.as_ref().into())),
        ),
        Err(e) => {
            let reader_error = ReaderError::Akd(e);
            let status = reader_error.status_code();
            error!(err = ?reader_error, status = %status, "Failed to get AKD public key");
            (status, Json(Response::error(reader_error)))
        }
    }
}
