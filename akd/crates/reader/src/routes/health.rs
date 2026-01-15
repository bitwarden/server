use axum::{http::StatusCode, Json};
use serde::{Deserialize, Serialize};
use tracing::{info, instrument};

use crate::routes::Response;

#[derive(Debug, Serialize, Deserialize)]
pub struct HealthData {
    time: String,
}

#[instrument(skip_all)]
pub async fn health_handler() -> (StatusCode, Json<Response<HealthData>>) {
    info!("Handling server health request");

    let time = chrono::Utc::now().to_rfc3339();

    (StatusCode::OK, Json(Response::success(HealthData { time })))
}
