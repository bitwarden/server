use akd_storage::{AuditStorage, AuditStorageError};
use axum::{extract::State, http::StatusCode, Json};
use serde::{Deserialize, Serialize};
use tracing::{error, info, instrument};

use crate::{error::ReaderError, routes::Response, AppState};

#[derive(Debug, Serialize, Deserialize)]
pub struct AuditRequest {
    /// The epoch to audit. Proves the transition from epoch-1 to this epoch.
    pub epoch: u64,
}

pub type AuditData = akd::SingleAppendOnlyProof;

#[instrument(skip_all)]
pub async fn audit_handler(
    State(AppState {
        directory,
        audit_storage,
        ..
    }): State<AppState>,
    Json(AuditRequest { epoch }): Json<AuditRequest>,
) -> (StatusCode, Json<Response<AuditData>>) {
    info!(epoch, "Handling epoch audit request");

    if epoch == 0 {
        error!(epoch, "Cannot audit epoch 0: no predecessor epoch exists");
        return (
            StatusCode::BAD_REQUEST,
            Json(Response::error(ReaderError::InvalidEpoch { epoch })),
        );
    }

    let start_epoch = epoch - 1;

    let get_single_proof = |a: akd::AppendOnlyProof| {
        a.proofs
            .into_iter()
            .next()
            .ok_or_else(|| ReaderError::InternalError("audit returned no proof".to_string()))
    };

    // Attempt to serve from blob storage first (fast path for single-step transitions).
    // Falls back to directory computation on cache miss or storage error.
    let audit_proof: Result<AuditData, ReaderError> = if let Some(ref storage) = audit_storage {
        match storage.get_blob(start_epoch).await {
            Ok(blob) => {
                info!(epoch, "Serving audit proof from blob storage");
                match blob.decode() {
                    Ok((_, _, _, single_proof)) => Ok(single_proof),
                    Err(e) => {
                        error!(err = ?e, epoch, "Failed to decode audit blob, falling back to directory");
                        directory
                            .audit(start_epoch, epoch)
                            .await
                            .map_err(ReaderError::Akd)
                            .and_then(get_single_proof)
                    }
                }
            }
            Err(AuditStorageError::NotFound { .. }) => {
                info!(
                    epoch,
                    "Audit blob not in storage, falling back to directory"
                );
                directory
                    .audit(start_epoch, epoch)
                    .await
                    .map_err(ReaderError::Akd)
                    .and_then(get_single_proof)
            }
            Err(e) => {
                error!(%e, epoch, "Blob storage error, falling back to directory");
                directory
                    .audit(start_epoch, epoch)
                    .await
                    .map_err(ReaderError::Akd)
                    .and_then(get_single_proof)
            }
        }
    } else {
        directory
            .audit(start_epoch, epoch)
            .await
            .map_err(ReaderError::Akd)
            .and_then(get_single_proof)
    };

    match audit_proof {
        Ok(audit_proof) => (StatusCode::OK, Json(Response::success(audit_proof))),
        Err(reader_error) => {
            let status = reader_error.status_code();
            error!(err = ?reader_error, status = %status, "Failed to perform epoch audit");
            (status, Json(Response::error(reader_error)))
        }
    }
}
