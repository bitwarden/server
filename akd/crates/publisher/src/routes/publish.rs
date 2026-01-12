use super::AppState;
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
    State(AppState { directory, .. }): State<AppState>,
    Json(request): Json<PublishRequest>,
) -> impl IntoResponse {
    info!("Handling publish request");

    let akd_label: Vec<u8> = request.akd_label_b64.into_bytes();
    let akd_value: Vec<u8> = request.akd_value_b64.into_bytes();

    //TODO: enqueue publish operation to to_publish queue

    Json(PublishResponse { success: true })
}
