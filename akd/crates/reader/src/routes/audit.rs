use axum::{extract::State, http::StatusCode, Json};
use serde::{Deserialize, Serialize};
use tracing::{error, info, instrument};

use crate::{error::ReaderError, routes::Response, AppState};

#[derive(Debug, Serialize, Deserialize)]
pub struct AuditRequest {
    pub start_epoch: u64,
    pub end_epoch: u64,
}

pub type AuditData = akd::AppendOnlyProof;

#[instrument(skip_all)]
pub async fn audit_handler(
    State(AppState { directory, .. }): State<AppState>,
    Json(AuditRequest {
        start_epoch,
        end_epoch,
    }): Json<AuditRequest>,
) -> (StatusCode, Json<Response<AuditData>>) {
    info!(
        start_epoch,
        end_epoch, "Handling epoch audit request request"
    );

    // Validate epoch range
    if start_epoch > end_epoch {
        error!(
            start_epoch,
            end_epoch, "Invalid epoch range: start_epoch must be <= end_epoch"
        );
        return (
            StatusCode::BAD_REQUEST,
            Json(Response::error(ReaderError::InvalidEpochRange {
                start_epoch,
                end_epoch,
            })),
        );
    }

    let audit_proof = directory.audit(start_epoch, end_epoch).await;

    match audit_proof {
        Ok(audit_proof) => (StatusCode::OK, Json(Response::success(audit_proof))),
        Err(e) => {
            let reader_error = ReaderError::Akd(e);
            let status = reader_error.status_code();
            error!(err = ?reader_error, status = %status, "Failed to perform epoch audit");
            (status, Json(Response::error(reader_error)))
        }
    }
}
