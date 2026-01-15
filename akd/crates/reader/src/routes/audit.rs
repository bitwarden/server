use axum::{extract::State, http::StatusCode, Json};
use serde::{Deserialize, Serialize};
use tracing::{error, info, instrument};

use crate::{routes::Response, AppState};

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
    let audit_proof = directory.audit(start_epoch, end_epoch).await;

    match audit_proof {
        Ok(audit_proof) => (StatusCode::OK, Json(Response::success(audit_proof))),
        Err(e) => {
            error!(err = ?e, "Failed to perform epoch audit");
            (StatusCode::INTERNAL_SERVER_ERROR, Json(Response::fail(e)))
        }
    }
}
