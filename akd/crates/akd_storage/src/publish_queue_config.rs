use serde::Deserialize;

#[derive(Debug, Clone, Deserialize)]
#[serde(tag = "type")]
pub struct PublishQueueConfig {
    pub provider: PublishQueueProvider,
    #[serde(default = "default_publish_limit")]
    pub epoch_update_limit: Option<isize>,
}

fn default_publish_limit() -> Option<isize> {
    None
}

#[derive(Debug, Clone, Deserialize)]
#[serde(tag = "type")]
pub enum PublishQueueProvider {
    DbBacked,
}

impl PublishQueueConfig {}
