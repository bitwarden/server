use axum::Json;
use serde::{Deserialize, Serialize};
use tracing::{error, info, instrument};

#[derive(Debug, Serialize, Deserialize)]
pub struct ServerHealth {
    time: String,
}

#[instrument(skip_all)]
pub async fn health_handler() -> Json<ServerHealth> {
    info!("Handling server health request");

    let time = chrono::Utc::now().to_rfc3339();

    Json(ServerHealth { time })
}
