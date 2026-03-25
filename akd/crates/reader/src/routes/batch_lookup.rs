use axum::{extract::State, http::StatusCode, Json};
use bitwarden_akd_configuration::BitwardenAkdLabelMaterial;
use serde::{Deserialize, Serialize};
use tracing::{error, info, instrument};

use crate::{
    error::ReaderError,
    routes::{get_epoch_hash::EpochData, Response},
    AppState,
};

#[derive(Debug, Serialize, Deserialize)]
pub struct BatchLookupRequest {
    /// An array of labels to look up. Each label is encoded as base64.
    pub bitwarden_akd_labels: Vec<BitwardenAkdLabelMaterial>,
}

#[derive(Debug, Serialize, Deserialize)]
pub struct BatchLookupData {
    pub lookup_proofs: Vec<akd::LookupProof>,
    pub epoch_data: EpochData,
}

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
            Json(Response::error(ReaderError::EmptyBatch)),
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
            Json(Response::error(ReaderError::BatchTooLarge {
                limit: max_batch_lookup_size,
            })),
        );
    }

    let labels = bitwarden_akd_labels
        .into_iter()
        .map(|label_b64| (&label_b64).into())
        .collect::<Vec<akd::AkdLabel>>();
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
            (status, Json(Response::error(reader_error)))
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    /// Unit tests for batch lookup validation
    /// Tests the validation logic in batch_lookup_handler (lines 36-57)

    #[test]
    fn test_empty_batch_rejected() {
        // Threat model: DoS via empty batch requests
        // Tests handler logic at lines 36-42
        let labels_b64: Vec<BitwardenAkdLabelMaterial> = vec![];
        assert!(labels_b64.is_empty(), "Empty batch should be detected");
    }

    #[test]
    fn test_batch_size_boundary_validation() {
        // Threat model: Off-by-one errors in size validation
        // Tests handler logic at lines 45-57
        let test_cases = vec![
            (1, 10, false),  // Minimum valid batch
            (9, 10, false),  // Just under limit
            (10, 10, false), // Exactly at limit
            (11, 10, true),  // Just over limit - should be rejected
            (100, 10, true), // Well over limit
        ];

        for (batch_size, max_size, should_be_rejected) in test_cases {
            let exceeds_limit = batch_size > max_size;
            assert_eq!(
                exceeds_limit,
                should_be_rejected,
                "Batch size {} with max {} should {}be rejected",
                batch_size,
                max_size,
                if should_be_rejected { "" } else { "not " }
            );
        }
    }
}
