use axum::{extract::State, http::StatusCode, Json};
use bitwarden_akd_configuration::{
    wire_models::{BatchLookupData, BatchLookupRequest},
    BitwardenAkdLabelMaterial,
};

use super::Response;
use tracing::{error, info, instrument};

use crate::{error::ReaderError, AppState};

#[instrument(skip_all)]
pub async fn batch_lookup_handler(
    State(AppState {
        directory,
        max_batch_lookup_size,
        ..
    }): State<AppState>,
    Json(BatchLookupRequest {
        bitwarden_akd_labels,
    }): Json<BatchLookupRequest>,
) -> (StatusCode, Json<Response<BatchLookupData>>) {
    info!("Handling batch lookup request");

    // Validate batch not empty
    if bitwarden_akd_labels.is_empty() {
        error!("Empty batch request received");
        return (
            StatusCode::BAD_REQUEST,
            Json(Response::error(ReaderError::EmptyBatch.to_error_response())),
        );
    }

    // Validate batch size
    if bitwarden_akd_labels.len() > max_batch_lookup_size {
        error!(
            batch_size = bitwarden_akd_labels.len(),
            max_size = max_batch_lookup_size,
            "Batch size exceeds limit"
        );
        return (
            StatusCode::BAD_REQUEST,
            Json(Response::error(
                ReaderError::BatchTooLarge {
                    limit: max_batch_lookup_size,
                }
                .to_error_response(),
            )),
        );
    }

    let labels: Vec<akd::AkdLabel> = bitwarden_akd_labels
        .into_iter()
        .map(|req| {
            let label: BitwardenAkdLabelMaterial = req.into();
            (&label).into()
        })
        .collect();
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
            (
                status,
                Json(Response::error(reader_error.to_error_response())),
            )
        }
    }
}
