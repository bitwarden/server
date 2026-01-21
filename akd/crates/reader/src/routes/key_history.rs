use axum::{extract::State, http::StatusCode, Json};
use serde::{Deserialize, Serialize};
use tracing::{error, info, instrument};

use crate::{
    error::ReaderError,
    routes::{get_epoch_hash::EpochData, Response},
    AppState,
};

#[derive(Debug, Serialize, Deserialize)]
pub struct KeyHistoryRequest {
    /// the label to look up encoded as an uppercase hex string
    pub label: akd::AkdLabel,
    pub history_params: HistoryParams,
}

/// The parameters that dictate how much of the history proof to return to the consumer
/// (either a complete history, or some limited form).
#[derive(Copy, Clone, Serialize, Deserialize, Debug)]
#[serde(tag = "type")]
pub enum HistoryParams {
    /// Returns a complete history for a label
    Complete,
    /// Returns up to the most recent N updates for a label
    MostRecent(usize),
}

impl From<HistoryParams> for akd::HistoryParams {
    fn from(params: HistoryParams) -> Self {
        match params {
            HistoryParams::Complete => akd::HistoryParams::Complete,
            HistoryParams::MostRecent(n) => akd::HistoryParams::MostRecent(n),
        }
    }
}

#[derive(Debug, Serialize, Deserialize)]
pub struct HistoryData {
    pub history_proof: akd::HistoryProof,
    pub epoch_data: EpochData,
}

#[instrument(skip_all)]
pub async fn key_history_handler(
    State(AppState { directory, .. }): State<AppState>,
    Json(KeyHistoryRequest {
        label,
        history_params,
    }): Json<KeyHistoryRequest>,
) -> (StatusCode, Json<Response<HistoryData>>) {
    info!("Handling get key history request");
    let history_proof = directory.key_history(&label, history_params.into()).await;

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
            (status, Json(Response::error(reader_error)))
        }
    }
}
