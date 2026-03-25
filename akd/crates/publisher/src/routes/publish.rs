use super::AppState;
use akd_storage::PublishQueue;
use axum::{extract::State, http::StatusCode, response::IntoResponse, Json};
use bitwarden_akd_configuration::BitwardenAkdPairMaterial;
use serde::Serialize;
use tracing::{error, info, instrument};

#[derive(Debug, Serialize)]
pub struct PublishResponse {
    pub success: bool,
}

#[instrument(skip_all)]
pub async fn publish_handler(
    State(AppState { publish_queue, .. }): State<AppState>,
    Json(bitwarden_akd_pair): Json<BitwardenAkdPairMaterial>,
) -> impl IntoResponse {
    info!("Handling publish request");

    let label = (&bitwarden_akd_pair).into();
    let value = (&bitwarden_akd_pair).into();
    if let Err(e) = publish_queue.enqueue(label, value).await {
        error!("Failed to enqueue publish request: {:?}", e);
        return (
            StatusCode::INTERNAL_SERVER_ERROR,
            Json(PublishResponse { success: false }),
        );
    }

    (StatusCode::OK, Json(PublishResponse { success: true }))
}
