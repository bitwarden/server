use super::AppState;
use akd::{AkdLabel, AkdValue};
use akd_storage::PublishQueue;
use axum::{extract::State, http::StatusCode, response::IntoResponse, Json};
use serde::{Deserialize, Serialize};
use tracing::{error, info, instrument};

#[derive(Debug, Serialize, Deserialize)]
pub struct PublishRequest {
    pub akd_label_b64: bitwarden_encoding::B64,
    pub akd_value_b64: bitwarden_encoding::B64,
}

#[derive(Debug, Serialize)]
pub struct PublishResponse {
    pub success: bool,
}

#[instrument(skip_all)]
pub async fn publish_handler(
    State(AppState { publish_queue, .. }): State<AppState>,
    Json(request): Json<PublishRequest>,
) -> impl IntoResponse {
    info!("Handling publish request");

    let akd_label: AkdLabel = AkdLabel(request.akd_label_b64.into_bytes());
    let akd_value: AkdValue = AkdValue(request.akd_value_b64.into_bytes());

    //TODO: enqueue publish operation to to_publish queue
    if let Err(e) = publish_queue.enqueue(akd_label, akd_value).await {
        error!("Failed to enqueue publish request: {:?}", e);
        return (
            StatusCode::INTERNAL_SERVER_ERROR,
            Json(PublishResponse { success: false }),
        );
    }

    (StatusCode::OK, Json(PublishResponse { success: true }))
}
