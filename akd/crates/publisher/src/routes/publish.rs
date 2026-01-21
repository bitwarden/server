use super::AppState;
use akd_storage::PublishQueue;
use axum::{extract::State, http::StatusCode, response::IntoResponse, Json};
use common::{AkdLabelB64, AkdValueB64};
use serde::{Deserialize, Serialize};
use tracing::{error, info, instrument};

#[derive(Debug, Serialize, Deserialize)]
pub struct PublishRequest {
    pub label_b64: AkdLabelB64,
    pub value_b64: AkdValueB64,
}

#[derive(Debug, Serialize)]
pub struct PublishResponse {
    pub success: bool,
}

#[instrument(skip_all)]
pub async fn publish_handler(
    State(AppState { publish_queue, .. }): State<AppState>,
    Json(PublishRequest {
        label_b64,
        value_b64,
    }): Json<PublishRequest>,
) -> impl IntoResponse {
    info!("Handling publish request");

    if let Err(e) = publish_queue
        .enqueue(label_b64.into(), value_b64.into())
        .await
    {
        error!("Failed to enqueue publish request: {:?}", e);
        return (
            StatusCode::INTERNAL_SERVER_ERROR,
            Json(PublishResponse { success: false }),
        );
    }

    (StatusCode::OK, Json(PublishResponse { success: true }))
}
