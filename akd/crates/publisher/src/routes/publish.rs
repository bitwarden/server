use super::AppState;
use akd_storage::PublishQueue;
use axum::{extract::State, http::StatusCode, response::IntoResponse, Json};
use bitwarden_akd_configuration::{
    request_models::BitwardenAkdPairMaterialRequest, BitwardenAkdPairMaterial,
};
use serde::Serialize;
use tracing::{error, info, instrument};

#[derive(Debug, Serialize)]
pub struct PublishResponse {
    pub success: bool,
}

#[instrument(skip_all)]
pub async fn publish_handler(
    State(AppState { publish_queue, .. }): State<AppState>,
    Json(pair_request): Json<BitwardenAkdPairMaterialRequest>,
) -> impl IntoResponse {
    info!("Handling publish request");

    let bitwarden_akd_pair: BitwardenAkdPairMaterial = match pair_request.try_into() {
        Ok(pair) => pair,
        Err(e) => {
            error!("Invalid request: {:?}", e);
            return (
                StatusCode::BAD_REQUEST,
                Json(PublishResponse { success: false }),
            );
        }
    };

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
