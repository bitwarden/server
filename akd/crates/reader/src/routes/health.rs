use axum::{extract::State, http::StatusCode, Json};
use serde::{Deserialize, Serialize};
use tracing::{info, instrument};

use crate::{routes::Response, AppState};

#[derive(Debug, Serialize, Deserialize)]
pub struct HealthData {
    time: String,
    #[serde(skip_serializing_if = "Option::is_none")]
    predicted_next_epoch_datetime: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    predicted_seconds_until_next_epoch: Option<f64>,
}

#[instrument(skip_all)]
pub async fn health_handler(
    State(AppState { epoch_tracker, .. }): State<AppState>,
) -> (StatusCode, Json<Response<HealthData>>) {
    info!("Handling server health request");

    let now = chrono::Utc::now();
    let time = now.to_rfc3339();

    let (predicted_seconds_until_next_epoch, predicted_next_epoch_datetime) = epoch_tracker
        .predict_next_epoch(now)
        .await
        .map(|(seconds, datetime)| (Some(seconds), Some(datetime.to_rfc3339())))
        .unwrap_or((None, None));

    (
        StatusCode::OK,
        Json(Response::success(HealthData {
            time,
            predicted_next_epoch_datetime,
            predicted_seconds_until_next_epoch,
        })),
    )
}
