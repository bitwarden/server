use serde::Deserialize;

#[derive(Debug, Clone, Deserialize)]
#[serde(tag = "type")]
pub enum PublishQueueConfig {
    /// Database-backed publish queue
    DbBacked,
}

impl PublishQueueConfig {}
